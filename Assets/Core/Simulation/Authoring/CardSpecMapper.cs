using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Card-level assembly only. Effect mapping lives on each EffectSpec subclass
    /// (no central effect switch — AGENTS.md rule 9).</summary>
    public static class CardSpecMapper
    {
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec.Category == CardCategory.Intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(
                        spec.Intervention.ToKey(), spec.EnergyCost, spec.InterventionEffectValue,
                        ToTargetSide(spec.InterventionTargetSide), spec.InterventionRequireAdjacent)
                };
            }

            var effects = (spec.Effects ?? Array.Empty<EffectSpec>())
                .Select(e => e.ToEffectData())
                .ToArray();
            return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution
            };
        }

        private static Side? ToTargetSide(InterventionTargetSideRef side)
        {
            switch (side)
            {
                case InterventionTargetSideRef.Player: return Side.Player;
                case InterventionTargetSideRef.Enemy: return Side.Enemy;
                default: return null;
            }
        }
    }
}
