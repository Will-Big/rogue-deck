using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Combat
{
    /// <summary>효과(또는 턴 시점 처리) 전후의 생존 차분으로 사망을 한 번 정리한다(전투 실행 계약 스펙 §3·§7):
    /// 사망 이벤트, 죽은 주인의 아직 차례가 오지 않은 카드를 실행선에서 빼기, 그 주인의 카드를 덱에서 빼기.
    /// 사망 능력은 부르지 않고
    /// HolderDied 사건으로 돌려준다 — 반응으로 처리할지는 효과 기원이 정한다.</summary>
    public sealed class DeathProcessor
    {
        private readonly System.Action<string> _removeOwnedCards;

        /// <param name="removeOwnedCards">죽은 주인의 카드를 덱에서 빼는 동작. 덱은 세션이 가지므로 참조 대신 동작을
        /// 받는다. 덱이 없는 전투는 빈 동작을 넘긴다.</param>
        public DeathProcessor(System.Action<string> removeOwnedCards)
        {
            _removeOwnedCards = removeOwnedCards ?? throw new System.ArgumentNullException(nameof(removeOwnedCards));
        }

        /// <summary>처리 직전의 생존 여부.</summary>
        public sealed class Snapshot
        {
            internal readonly Dictionary<string, bool> Party = new Dictionary<string, bool>();
            internal readonly Dictionary<string, bool> Enemies = new Dictionary<string, bool>();
        }

        public static Snapshot Capture(CombatState state)
        {
            var snapshot = new Snapshot();
            foreach (var member in state.Party)
            {
                snapshot.Party[member.Id] = member.IsAlive;
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id != null)
                {
                    snapshot.Enemies[enemy.Id] = enemy.Hp > 0;
                }
            }

            return snapshot;
        }

        /// <summary>새로 죽은 개체마다 사망 이벤트(파티 대형 순 → 적 대형 순)를 쓰고, 그 주인의 대기 카드를
        /// 실행선에서 뺀다(CardRemoved). 새 사망의 HolderDied 사건을 같은 순서로 돌려준다.</summary>
        public IReadOnlyList<CombatSignal> Process(CombatState state, Snapshot before, List<ResolutionEvent> events)
        {
            var signals = new List<CombatSignal>();
            foreach (var member in state.Party)
            {
                if (before.Party.TryGetValue(member.Id, out var wasAlive) && wasAlive && !member.IsAlive)
                {
                    events.Add(new PartyMemberDied(member.Id));
                    signals.Add(new CombatSignal(CombatSignalKeys.HolderDied, null, member.Id, 0));
                }
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id != null
                    && before.Enemies.TryGetValue(enemy.Id, out var wasAlive) && wasAlive && enemy.Hp <= 0)
                {
                    events.Add(new EnemyDied(enemy.Id));
                    signals.Add(new CombatSignal(CombatSignalKeys.HolderDied, null, enemy.Id, 0));
                }
            }

            // 소유자를 모르는 카드(OwnerId가 비어 있는 단일 적 호환 경로)를 잘못 지목하지 않도록 빈 id는 제외한다.
            foreach (var died in signals)
            {
                if (string.IsNullOrEmpty(died.TargetId))
                {
                    continue;
                }

                foreach (var removed in state.Zone.RemovePendingOwnedBy(died.TargetId))
                {
                    events.Add(new CardRemoved(removed.InstanceId, removed.Def.Id, removed.OwnerId));
                }

                _removeOwnedCards(died.TargetId);
            }

            return signals;
        }
    }
}
