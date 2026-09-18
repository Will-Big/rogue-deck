using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>효과 하나가 시작할 때 고른 대상 목록(전투 실행 계약 스펙 §2). 효과마다 새로 고르므로 앞 효과로
    /// 죽거나 이동한 결과가 반영되고, 광역 효과는 이 목록을 끝까지 쓴다 — 적용 도중 다시 고르지 않는다.
    /// 만드는 쪽은 EffectTargetResolver다.</summary>
    public sealed class EffectTargetSnapshot
    {
        private static readonly IReadOnlyList<PartyMember> NoPartyTargets = Array.Empty<PartyMember>();
        private static readonly IReadOnlyList<Enemy> NoEnemyTargets = Array.Empty<Enemy>();

        internal EffectTargetSnapshot(
            CardTargetKey key, IReadOnlyList<PartyMember> party, IReadOnlyList<Enemy> enemies)
        {
            Key = key;
            Party = party ?? NoPartyTargets;
            Enemies = enemies ?? NoEnemyTargets;
        }

        /// <summary>이 목록을 고른 위치 규칙.</summary>
        public CardTargetKey Key { get; }

        /// <summary>Key가 아군 축일 때의 대상(아니면 빈 목록).</summary>
        public IReadOnlyList<PartyMember> Party { get; }

        /// <summary>Key가 적 축일 때의 대상(아니면 빈 목록).</summary>
        public IReadOnlyList<Enemy> Enemies { get; }

        public bool IsEmpty => Party.Count == 0 && Enemies.Count == 0;

        /// <summary>고른 대상의 id, 목록 순서대로.</summary>
        public IReadOnlyList<string> Ids
            => Party.Select(member => member.Id).Concat(Enemies.Select(enemy => enemy.Id)).ToArray();

        public IReadOnlyList<PartyMember> PartyTargets(CardTargetKey key)
        {
            if (key.Faction != CardTargetFaction.Ally)
            {
                throw new ArgumentException("Party targets require the Ally faction.", nameof(key));
            }

            return key.Equals(Key) ? Party : NoPartyTargets;
        }

        public IReadOnlyList<Enemy> EnemyTargets(CardTargetKey key)
        {
            if (key.Faction != CardTargetFaction.Enemy)
            {
                throw new ArgumentException("Enemy targets require the Enemy faction.", nameof(key));
            }

            return key.Equals(Key) ? Enemies : NoEnemyTargets;
        }
    }
}
