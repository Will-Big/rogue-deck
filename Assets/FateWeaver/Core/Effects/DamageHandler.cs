using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Player cards hit their target enemy (by id, else the first enemy); enemy cards hit the
    /// player. Incoming damage is folded through the target's entity-scoped statuses (e.g. Vulnerable)
    /// when a StatusRegistry is present; with no registry it applies raw.</summary>
    public sealed class DamageHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public void Apply(EffectContext ctx)
        {
            var amount = ctx.EffectValue + ctx.Card.ConsumePendingDamageBonus();
            if (ctx.Card.Def.Side == Side.Player)
            {
                var target = SelectEnemy(ctx.State, ctx.Card.TargetId);
                var damage = FoldIncoming(ctx, target.Statuses, amount);
                target.Hp -= damage;
                ctx.DamageDealt = damage;
                ctx.TargetId = target.Id;
            }
            else
            {
                var damage = FoldIncoming(ctx, ctx.State.PlayerStatuses, amount);
                ctx.State.PlayerHp -= damage;
                ctx.DamageDealt = damage;
                ctx.TargetId = "player";
            }
        }

        /// <summary>Picks the card's intended target enemy by id; falls back to the first enemy.</summary>
        private static Combat.Enemy SelectEnemy(Combat.CombatState state, string targetId)
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
            }

            return state.Enemies[0];
        }

        /// <summary>Folds the target's entity-scoped statuses into incoming damage. An UntilConsumed
        /// status that actually changed the damage spends a charge (auto-consume).</summary>
        private static int FoldIncoming(EffectContext ctx, StatusBag bag, int damage)
        {
            if (ctx.StatusRegistry == null || bag == null)
            {
                return damage;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (ctx.StatusRegistry.TryResolve(status.Key, out var behavior))
                {
                    var after = behavior.ModifyIncomingDamage(damage, new StatusContext { Instance = status });
                    if (after != damage)
                    {
                        bag.Consume(status);
                    }

                    damage = after;
                }
            }

            return damage;
        }
    }
}
