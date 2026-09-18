using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Resolves enemy targets by living-formation position (Enemies index 0 = front, dead
    /// skipped, never reindexed) — the enemy-side mirror of PartyTargeting.</summary>
    public static class EnemyTargeting
    {
        public static Enemy Select(CombatState state, CardTargetRange range)
        {
            var living = SelectAll(state);
            switch (range)
            {
                case CardTargetRange.FrontOne: return living.Count > 0 ? living[0] : null;
                case CardTargetRange.BackOne: return living.Count > 0 ? living[living.Count - 1] : null;
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

        public static List<Enemy> SelectRange(CombatState state, CardTargetRange range)
        {
            var living = SelectAll(state);
            var take = TakeCount(range, living.Count);
            if (range == CardTargetRange.BackOne || range == CardTargetRange.BackTwo)
            {
                return living.GetRange(living.Count - take, take);
            }

            return living.GetRange(0, take);
        }

        private static int TakeCount(CardTargetRange range, int livingCount)
        {
            switch (range)
            {
                case CardTargetRange.FrontOne:
                case CardTargetRange.BackOne: return Math.Min(1, livingCount);
                case CardTargetRange.FrontTwo:
                case CardTargetRange.BackTwo: return Math.Min(2, livingCount);
                case CardTargetRange.All: return livingCount;
                default: throw new ArgumentOutOfRangeException(nameof(range), range, "Self is not a formation position.");
            }
        }
    }
}
