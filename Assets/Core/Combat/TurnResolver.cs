using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>실행선의 카드를 차례대로 실행하고 이벤트 타임라인을 낸다. 실행선은 고정 사본이 아니다 —
    /// 매번 아직 차례가 오지 않은 다음 카드를 묻는다(전투 실행 계약 스펙 §6).
    /// 카드 하나의 진행은 CardExecutor가 맡고, 이 클래스는 턴 단계의 호출 순서만 정한다: 카드마다 실행 → 승패 판정
    /// (결판이 나면 그 자리에서 종료) → 턴 끝에는 상태 틱 → 사망 정리 → 직접 반응 → 수명 만료 → 승패.</summary>
    public sealed class TurnResolver
    {
        private readonly StatusRegistry _statuses;
        private readonly EffectExecutor _executor;
        private readonly CardExecutor _cards;

        /// <param name="removeOwnedCards">주인이 죽는 즉시 그 주인의 카드를 덱(뽑을 더미·손·버린 더미)에서 빼는
        /// 동작. 덱은 세션이 가지므로 동작으로 받는다. 덱이 없는 전투(러너·코어 테스트)는 생략한다.</param>
        public TurnResolver(
            EffectRegistry effects,
            StatusRegistry statuses = null,
            ReactionRegistry reactions = null,
            System.Action<string> removeOwnedCards = null)
        {
            _statuses = statuses;
            _executor = new EffectExecutor(effects, statuses, reactions, removeOwnedCards);
            _cards = new CardExecutor(_executor, statuses);
        }

        public List<ResolutionEvent> Resolve(CombatState state, int turnIndex)
        {
            var events = new List<ResolutionEvent> { new TurnStarted(turnIndex) };
            var resolutionContext = ResolutionContext.From(state);

            for (var card = state.Zone.NextPending(); card != null; card = state.Zone.NextPending())
            {
                _cards.Execute(state, resolutionContext, card, events);

                // 승패는 카드 하나(직접 반응 포함)가 끝났을 때만 판정한다. 결판이 나면 다음 카드와 턴 종료 상태
                // 처리를 하지 않고 전투 종료를 알린다(스펙 §2).
                var outcome = CombatOutcomeEvaluator.Evaluate(state);
                if (outcome != Outcome.Ongoing)
                {
                    events.Add(new TurnEnded(turnIndex, outcome));
                    return events;
                }
            }

            EndOfTurnMaintenance(state, resolutionContext, events);
            events.Add(new TurnEnded(turnIndex, CombatOutcomeEvaluator.Evaluate(state)));
            return events;
        }

        /// <summary>턴 종료 시점: 상태 틱 → 사망 정리 → 직접 반응(턴 시점 처리는 Primary 기원, 계획 D7) → 수명 만료.</summary>
        private void EndOfTurnMaintenance(
            CombatState state, ResolutionContext resolutionContext, List<ResolutionEvent> events)
        {
            var before = DeathProcessor.Capture(state);
            var signals = new List<CombatSignal>();

            RunTurnEndTicks(state, events, signals);

            foreach (var died in _executor.Deaths.Process(state, before, events))
            {
                signals.Add(died);
            }

            _executor.Reactions.Dispatch(state, resolutionContext, Numbered(signals), events);

            foreach (var member in state.Party)
            {
                foreach (var key in member.Statuses.EndOfTurn())
                {
                    events.Add(new StatusExpired(member.Id, key.Id));
                }
            }

            foreach (var enemy in state.Enemies)
            {
                foreach (var key in enemy.Statuses.EndOfTurn())
                {
                    events.Add(new StatusExpired(enemy.Id, key.Id));
                }
            }
        }

        /// <summary>턴 시점 사건에는 효과 대상 목록이 없으므로 발생 순서만 붙인다(틱은 대형 순으로 일어난다).</summary>
        private static List<CombatSignal> Numbered(List<CombatSignal> signals)
        {
            var numbered = new List<CombatSignal>(signals.Count);
            for (var i = 0; i < signals.Count; i++)
            {
                numbered.Add(signals[i] with { Origin = EffectOrigin.Primary, TargetOrdinal = 0, Sequence = i });
            }

            return numbered;
        }

        /// <summary>행동 턴 종료 틱: 파티 대형 순 → 적 대형 순. 보유자별로 발동 직전에 생존을 확인하므로
        /// 앞선 틱으로 이미 사망한 대상은 제외된다(카드풀 스펙 §3.2). 틱 피해는 공통 피해 경로를 지난다.</summary>
        private void RunTurnEndTicks(CombatState state, List<ResolutionEvent> events, List<CombatSignal> signals)
        {
            if (_statuses == null)
            {
                return;
            }

            var sink = new DamageSink(events, signals);
            foreach (var member in state.Party)
            {
                if (!member.IsAlive) continue;
                var target = member;
                TickHolder(target.Statuses, target.Id, events, state.StatusContent,
                    key => damage => _executor.Damage.Deal(
                        state, target, StatusDamage(state, key, damage), sink));
            }

            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp <= 0) continue;
                var target = enemy;
                TickHolder(target.Statuses, target.Id, events, state.StatusContent,
                    key => damage => _executor.Damage.Deal(
                        state, target, StatusDamage(state, key, damage), sink));
            }
        }

        /// <summary>상태 피해 요청. 관통·배율 미적용은 그 상태의 저작 데이터가 정한다.</summary>
        private static DamageRequest StatusDamage(CombatState state, StatusKey key, int damage)
            => DamageRequest.StatusTick(damage, key.Id, state.StatusContent.DamageTraitsOf(key));

        private void TickHolder(
            StatusBag bag, string holderId, List<ResolutionEvent> events,
            Authoring.Statuses.StatusContentCatalog content, Func<StatusKey, Action<int>> dealDamageFor)
        {
            // Snapshot: a hook may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior))
                {
                    behavior.OnTurnEnd(new StatusTickContext
                    {
                        Instance = status,
                        HolderBag = bag,
                        HolderId = holderId,
                        DealDamage = dealDamageFor(status.Key),
                        Events = events,
                        Content = content
                    });
                }
            }
        }

    }
}
