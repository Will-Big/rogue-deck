using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>Moves the acting card's living owner within its own side formation. Negative values
    /// move toward that side's front (index 0), positive values toward its back, clamped to bounds.
    /// Missing or dead owners cancel instead of falling back to the front entity.</summary>
    public sealed class MoveFormationHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.MoveFormation;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (ctx.Card.Def.Side == Side.Player)
            {
                MovePartyOwner(ctx);
                return;
            }

            MoveEnemyOwner(ctx);
        }

        private static void MovePartyOwner(EffectContext ctx)
        {
            var owner = PartyTargeting.LivingById(ctx.State, ctx.Card.OwnerId);
            if (owner == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var currentIndex = ctx.State.Party.IndexOf(owner);
            var destinationIndex = ClampDestination(currentIndex, ctx.EffectValue, ctx.State.Party.Count);
            ctx.State.Party.RemoveAt(currentIndex);
            ctx.State.Party.Insert(destinationIndex, owner);
        }

        private static void MoveEnemyOwner(EffectContext ctx)
        {
            if (string.IsNullOrEmpty(ctx.Card.OwnerId))
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            Enemy owner = null;
            foreach (var enemy in ctx.State.Enemies)
            {
                if (enemy.Id == ctx.Card.OwnerId && enemy.Hp > 0)
                {
                    owner = enemy;
                    break;
                }
            }

            if (owner == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var currentIndex = ctx.State.Enemies.IndexOf(owner);
            var destinationIndex = ClampDestination(currentIndex, ctx.EffectValue, ctx.State.Enemies.Count);
            ctx.State.Enemies.RemoveAt(currentIndex);
            ctx.State.Enemies.Insert(destinationIndex, owner);
        }

        private static int ClampDestination(int currentIndex, int distance, int formationCount)
        {
            var destination = currentIndex + (long)distance;
            return (int)Math.Max(0L, Math.Min(formationCount - 1L, destination));
        }
    }
}
