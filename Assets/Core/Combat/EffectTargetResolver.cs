using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>위치 규칙 하나를 지금의 대형에 적용해 효과의 대상 목록을 고른다(전투 실행 계약 스펙 §2·§4).
    /// 살아 있는 개체만 고른다. 고를 수 없으면 빈 목록이다 — 카드를 취소하지 않는다(효과 미적용은
    /// EffectExecutor가 EffectResult.Applied=false로 기록한다).</summary>
    public sealed class EffectTargetResolver
    {
        public EffectTargetSnapshot Resolve(CombatState state, ExecutionCardInstance card, CardTargetKey key)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (card == null) throw new ArgumentNullException(nameof(card));

            return key.Faction == CardTargetFaction.Ally
                ? new EffectTargetSnapshot(key, SelectParty(state, card, key.Range), null)
                : new EffectTargetSnapshot(key, null, SelectEnemies(state, card, key.Range));
        }

        /// <summary>효과의 위치 선택자를 위치 범위로. 선택자가 없는 효과는 처리기가 FrontOne을 기본으로 준다.</summary>
        public static CardTargetRange RangeFor(TargetSelector selector)
        {
            switch (selector)
            {
                case TargetSelector.FrontOne: return CardTargetRange.FrontOne;
                case TargetSelector.FrontTwo: return CardTargetRange.FrontTwo;
                case TargetSelector.BackOne: return CardTargetRange.BackOne;
                case TargetSelector.BackTwo: return CardTargetRange.BackTwo;
                case TargetSelector.All: return CardTargetRange.All;
                default: throw new ArgumentOutOfRangeException(nameof(selector));
            }
        }

        private static IReadOnlyList<PartyMember> SelectParty(
            CombatState state, ExecutionCardInstance card, CardTargetRange range)
        {
            if (range == CardTargetRange.Self)
            {
                var self = PartyOwner(state, card.OwnerId);
                return self == null ? null : new[] { self };
            }

            return PartyTargeting.SelectRange(state, ToSelector(range)).AsReadOnly();
        }

        private static IReadOnlyList<Enemy> SelectEnemies(
            CombatState state, ExecutionCardInstance card, CardTargetRange range)
        {
            if (range == CardTargetRange.Self)
            {
                var self = EnemyOwner(state, card.OwnerId);
                return self == null ? null : new[] { self };
            }

            return EnemyTargeting.SelectRange(state, ToSelector(range)).AsReadOnly();
        }

        /// <summary>카드 주인인 살아 있는 파티원. 주인이 없으면 살아 있는 파티원이 하나일 때만 확정한다 —
        /// 여럿이면 대형 앞으로 대신 고르지 않는다.</summary>
        private static PartyMember PartyOwner(CombatState state, string ownerId)
        {
            PartyMember resolved = null;
            foreach (var member in state.Party)
            {
                if (member.IsAlive && (string.IsNullOrEmpty(ownerId) || member.Id == ownerId))
                {
                    if (resolved != null)
                    {
                        return null;
                    }

                    resolved = member;
                }
            }

            return resolved;
        }

        private static Enemy EnemyOwner(CombatState state, string ownerId)
        {
            Enemy resolved = null;
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp > 0 && (string.IsNullOrEmpty(ownerId) || enemy.Id == ownerId))
                {
                    if (resolved != null)
                    {
                        return null;
                    }

                    resolved = enemy;
                }
            }

            return resolved;
        }

        private static TargetSelector ToSelector(CardTargetRange range)
        {
            switch (range)
            {
                case CardTargetRange.FrontOne: return TargetSelector.FrontOne;
                case CardTargetRange.FrontTwo: return TargetSelector.FrontTwo;
                case CardTargetRange.BackOne: return TargetSelector.BackOne;
                case CardTargetRange.BackTwo: return TargetSelector.BackTwo;
                case CardTargetRange.All: return TargetSelector.All;
                default: throw new ArgumentOutOfRangeException(nameof(range));
            }
        }
    }
}
