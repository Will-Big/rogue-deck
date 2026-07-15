using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Player cards hit their target enemy (by id, else the first enemy); enemy cards hit a
    /// party member chosen by the effect's TargetSelector (null defaults to FrontMost, for pre-party
    /// compat). Incoming damage is folded through the target's entity-scoped statuses (e.g. Vulnerable,
    /// Block) when a StatusRegistry is present; with no registry it applies raw. If no target can be
    /// resolved the card is cancelled (NoValidTarget) and nothing is mutated.</summary>
    public sealed class DamageHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            var amount = ctx.EffectValue + ctx.Card.ConsumePendingDamageBonus();
            if (ctx.Card.Def.Side == Side.Player)
            {
                var target = SelectEnemy(ctx.State, ctx.Card.TargetId);
                if (target == null)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                var damage = FoldIncoming(ctx, target.Statuses, amount);
                target.Hp -= damage;
                ctx.DamageDealt = damage;
                ctx.TargetId = target.Id;
            }
            else
            {
                var target = SelectPartyTarget(ctx);
                if (target == null)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                var damage = FoldIncoming(ctx, target.Statuses, amount);
                // Routed through PartyMember.TakeDamage (not a raw Hp -=) so a lethal hit can be
                // absorbed by a SurviveCharges charge (DeathsDoor); TurnResolver's death sweep reads
                // the resulting Hp/SurviveCharges state to emit DeathsDoorSurvived/PartyMemberDied.
                target.TakeDamage(damage);
                ctx.DamageDealt = damage;
                ctx.TargetId = target.Id;
            }
        }

        /// <summary>Picks the card's intended target enemy by id, else the first enemy. An explicit id
        /// that no longer matches any enemy resolves to no target (the caller cancels) rather than
        /// silently falling back to the front.</summary>
        private static Enemy SelectEnemy(CombatState state, string targetId)
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

        /// <summary>Picks the party member an enemy attack hits, via the effect's position selector
        /// (defaulting to FrontMost) evaluated against the living party formation at execution time.</summary>
        private static PartyMember SelectPartyTarget(EffectContext ctx)
        {
            var selector = ctx.Effect?.TargetSelector ?? Cards.TargetSelector.FrontMost;
            return PartyTargeting.Select(ctx.State, selector);
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
