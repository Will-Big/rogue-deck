using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Card-level assembly only. Effect mapping lives on each EffectSpec subclass
    /// (no central effect switch — AGENTS.md rule 9). 카드는 효과마다 자기 진영 축의 위치를 골라 준다.
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
                .Select(e => e.ToEffectData(spec.Side, TargetOf(execution, e)))
                .ToArray();
            return new CardDefinition(
                spec.Id, spec.Name, spec.Side, execution.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution,
                StartCondition = execution.StartCondition?.ToCondition()
            };
        }

        /// <summary>효과의 진영 축에서 고른 위치. 대상 효과가 아니면 null.</summary>
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
