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

        public CardTargetKey? TargetFor(CardDefinition card, EffectData effect)
            => new CardTargetKey(
                card.Side == Side.Player ? CardTargetFaction.Ally : CardTargetFaction.Enemy,
                CardTargetRange.Self);

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (ctx.Card.Def.Side == Side.Player)
            {
                if (ctx.Targets != null)
                {
                    MoveSnapshotPartyOwner(ctx, TargetFor(ctx.Card.Def, ctx.Effect).Value);
                    return;
                }

                MovePartyOwner(ctx);
                return;
            }

            if (ctx.Targets != null)
            {
                MoveSnapshotEnemyOwner(ctx, TargetFor(ctx.Card.Def, ctx.Effect).Value);
                return;
            }

            MoveEnemyOwner(ctx);
        }

        private static void MoveSnapshotPartyOwner(EffectContext ctx, CardTargetKey key)
        {
            var targets = ctx.Targets.PartyTargets(key);
            if (targets.Count != 1 || !targets[0].IsAlive)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var owner = targets[0];
            var currentIndex = ctx.State.Party.IndexOf(owner);
            var destinationIndex = ClampDestination(currentIndex, ctx.EffectValue, ctx.State.Party.Count);
            ctx.State.Party.RemoveAt(currentIndex);
            ctx.State.Party.Insert(destinationIndex, owner);
            ctx.TargetId = owner.Id;
        }

        private static void MoveSnapshotEnemyOwner(EffectContext ctx, CardTargetKey key)
        {
            var targets = ctx.Targets.EnemyTargets(key);
            if (targets.Count != 1 || targets[0].Hp <= 0)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var owner = targets[0];
            var currentIndex = ctx.State.Enemies.IndexOf(owner);
            var destinationIndex = ClampDestination(currentIndex, ctx.EffectValue, ctx.State.Enemies.Count);
            ctx.State.Enemies.RemoveAt(currentIndex);
            ctx.State.Enemies.Insert(destinationIndex, owner);
            ctx.TargetId = owner.Id;
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
