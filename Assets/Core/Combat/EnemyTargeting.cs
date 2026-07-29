using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Resolves enemy targets by living-formation position (Enemies index 0 = front, dead
    /// skipped, never reindexed) — the enemy-side mirror of PartyTargeting. ByIdOrFront preserves the
    /// legacy player-card selection (explicit id, else raw first enemy) so pre-selector content and
    /// timelines stay identical.</summary>
    public static class EnemyTargeting
    {
        public static Enemy Select(CombatState state, TargetSelector selector)
        {
            var living = SelectAll(state);
            switch (selector)
            {
                case TargetSelector.FrontMost: return living.Count > 0 ? living[0] : null;
                case TargetSelector.SecondFromFront: return living.Count > 1 ? living[1] : null;
                case TargetSelector.BackMost: return living.Count > 0 ? living[living.Count - 1] : null;
                case TargetSelector.Random:
                    return living.Count > 0 ? living[state.Rng.Next(living.Count)] : null;
                default: return null; // All은 다중 대상 — SelectAll을 쓴다.
            }
        }

        public static List<Enemy> SelectAll(CombatState state)
        {
            var living = new List<Enemy>();
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp > 0)
                {
                    living.Add(enemy);
                }
            }

            return living;
        }

        /// <summary>Legacy selection: explicit id (missing id = no target), else the first enemy
        /// regardless of HP — exactly the pre-selector behavior of DamageHandler.SelectEnemy.</summary>
        public static Enemy ByIdOrFront(CombatState state, string targetId)
        {
            if (!string.IsNullOrEmpty(targetId))
            {
                foreach (var enemy in state.Enemies)
                {
                    if (enemy.Id == targetId)
                    {
                        return enemy;
                    }
                }

                return null;
            }

            return state.Enemies.Count > 0 ? state.Enemies[0] : null;
        }
    }
}
