using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Card-level assembly only. Effect mapping lives on each EffectSpec subclass
    /// (no central effect switch — AGENTS.md rule 9).</summary>
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
                Category = CardCategory.Execution
            };
        }
    }
}
