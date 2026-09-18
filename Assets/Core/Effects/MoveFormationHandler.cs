using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>Moves the acting card's living owner (its Self target) within its own side formation.
    /// Negative values move toward that side's front (index 0), positive values toward its back, clamped
    /// to bounds. A missing or dead owner has no target, so the effect is not applied.</summary>
    public sealed class MoveFormationHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.MoveFormation;

        public void Apply(EffectContext ctx)
        {
            foreach (var owner in ctx.RequireTargets().Party)
            {
                Move(ctx, ctx.State.Party, owner, owner.Id, Side.Player);
            }

            foreach (var owner in ctx.RequireTargets().Enemies)
            {
                Move(ctx, ctx.State.Enemies, owner, owner.Id, Side.Enemy);
            }
        }

        private static void Move<T>(EffectContext ctx, List<T> formation, T owner, string ownerId, Side side)
        {
            var currentIndex = formation.IndexOf(owner);
            var destinationIndex = ClampDestination(currentIndex, ctx.EffectValue, formation.Count);
            formation.RemoveAt(currentIndex);
            formation.Insert(destinationIndex, owner);
            if (destinationIndex != currentIndex)
            {
                ctx.ExtraEvents.Add(new Events.FormationMoved(ownerId, side, currentIndex, destinationIndex));
            }
        }

        private static int ClampDestination(int currentIndex, int distance, int formationCount)
        {
            var destination = currentIndex + (long)distance;
            return (int)Math.Max(0L, Math.Min(formationCount - 1L, destination));
        }
    }
}
