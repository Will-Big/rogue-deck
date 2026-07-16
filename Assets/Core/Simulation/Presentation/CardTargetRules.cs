using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Presentation
{
    /// <summary>Number of execution-rail cards that must be selected before a card can be played.
    /// Explicit party-member targeting remains the responsibility of PartyTargetRules.</summary>
    public static class CardTargetRules
    {
        public static int RequiredRailTargets(CardDefinition definition)
        {
            if (definition == null
                || definition.Category != CardCategory.Intervention
                || definition.InterventionAction == null)
            {
                return 0;
            }

            return definition.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder ? 2 : 1;
        }
    }
}
