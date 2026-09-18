using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Card-level assembly only. Effect mapping lives on each EffectSpec subclass
    /// (no central effect switch — AGENTS.md rule 9). 카드의 두 위치 축은 CardDefinition으로 그대로 옮기고,
    /// 효과는 자기 진영만 갖는다.
    /// 검증(AuthoringValidator)을 통과한 스펙만 받는다.</summary>
    public static class CardSpecMapper
    {
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec is InterventionCardSpec intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        intervention.Intervention.Key,
                        spec.EnergyCost,
                        intervention.Intervention.ToPayload())
                };
            }

            var execution = (ExecutionCardSpec)spec;
            var effects = (execution.Effects ?? Array.Empty<EffectSpec>())
                .Select(e => e.ToEffectData())
                .ToArray();
            return new CardDefinition(
                spec.Id, spec.Name, spec.Side, execution.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution,
                StartCondition = execution.StartCondition?.ToCondition(),
                AllyTarget = execution.Targets?.Ally,
                EnemyTarget = execution.Targets?.Enemy
            };
        }

        /// <summary>효과의 진영 축에서 고른 위치(로딩 검증용). 대상 효과가 아니면 null.</summary>
        internal static CardTargetKey? TargetOf(ExecutionCardSpec card, EffectSpec effect)
        {
            if (!effect.IsTargeted || !effect.TargetFaction.HasValue)
            {
                return null;
            }

            var faction = effect.TargetFaction.Value;
            var range = card.Targets?.RangeOf(faction);
            return range.HasValue ? new CardTargetKey(faction, range.Value) : (CardTargetKey?)null;
        }
    }
}
