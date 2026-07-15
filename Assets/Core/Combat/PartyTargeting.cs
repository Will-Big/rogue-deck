using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Resolves party-member targets: by living formation position, or by id. Position
    /// selection walks the living members in their existing Party order (dead members are skipped,
    /// never reindexed around), and never touches CombatState.Enemies — Party and Enemies are
    /// independent formations (see PartyMember.cs).</summary>
    public static class PartyTargeting
    {
        public static PartyMember Select(CombatState state, TargetSelector selector)
        {
            var living = LivingInFormationOrder(state);
            return selector switch
            {
                TargetSelector.FrontMost => living.Count > 0 ? living[0] : null,
                TargetSelector.SecondFromFront => living.Count > 1 ? living[1] : null,
                TargetSelector.BackMost => living.Count > 0 ? living[^1] : null,
                TargetSelector.Random => living.Count > 0 ? living[state.Rng.Next(living.Count)] : null,
                _ => null
            };
        }

        /// <summary>Finds a party member by id regardless of alive/dead state.</summary>
        public static PartyMember ById(CombatState state, string memberId)
        {
            if (string.IsNullOrEmpty(memberId))
            {
                return null;
            }

            foreach (var member in state.Party)
            {
                if (member.Id == memberId)
                {
                    return member;
                }
            }

            return null;
        }

        /// <summary>Finds a party member by id, but only if they are currently alive.</summary>
        public static PartyMember LivingById(CombatState state, string memberId)
        {
            var member = ById(state, memberId);
            return member != null && member.IsAlive ? member : null;
        }

        private static List<PartyMember> LivingInFormationOrder(CombatState state)
        {
            var living = new List<PartyMember>();
            foreach (var member in state.Party)
            {
                if (member.IsAlive)
                {
                    living.Add(member);
                }
            }

            return living;
        }
    }
}
