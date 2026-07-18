using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Combat
{
    /// <summary>Whether a card needs the player to explicitly click an ally target, versus resolving
    /// its target automatically (self, all party members, a position selector, or a random pick).</summary>
    public static class PartyTargetRules
    {
        public static bool IsValidBaseExecutionDefinition(CardDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return definition.Side != Side.Player
                || definition.Category != CardCategory.Execution
                || !RequiresExplicitAllyTarget(definition);
        }

        public static bool RequiresExplicitAllyTarget(CardDefinition definition)
        {
            foreach (var effect in definition.Effects)
            {
                if (effect.Key == EffectKeys.ApplyStatus && effect.StatusTarget == StatusApplyTarget.PartyMember)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsValidExplicitAllyTarget(CombatState state, string targetId)
            => PartyTargeting.LivingById(state, targetId) != null;
    }
}
