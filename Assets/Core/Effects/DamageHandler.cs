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
                if (ctx.Effect?.TargetSelector == Cards.TargetSelector.All)
                {
                    var targets = EnemyTargeting.SelectAll(ctx.State);
                    if (targets.Count == 0)
                    {
                        ctx.Cancel(CardCancellationReason.NoValidTarget);
                        return;
                    }

                    var total = 0;
                    foreach (var each in targets)
                    {
                        var dealt = FoldIncoming(ctx, each.Statuses, amount);
                        each.Hp -= dealt;
                        total += dealt;
                    }

                    ctx.DamageDealt = total;
                    return;
                }

                var target = ctx.Effect?.TargetSelector is Cards.TargetSelector selector
                    ? EnemyTargeting.Select(ctx.State, selector)
                    : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
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
                if (ctx.Effect?.TargetSelector == Cards.TargetSelector.All)
                {
                    var targets = AllLivingParty(ctx.State);
                    if (targets.Count == 0)
                    {
                        ctx.Cancel(CardCancellationReason.NoValidTarget);
                        return;
                    }

                    var total = 0;
                    foreach (var each in targets)
                    {
                        var dealt = FoldIncoming(ctx, each.Statuses, amount);
                        // Routed through PartyMember.TakeDamage (not a raw Hp -=) so a lethal hit can
                        // be absorbed by a SurviveCharges charge (DeathsDoor); TurnResolver's death
                        // sweep reads the resulting Hp/SurviveCharges state.
                        each.TakeDamage(dealt);
                        total += dealt;
                    }

                    ctx.DamageDealt = total;
                    return;
                }

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

        /// <summary>Picks the party member an enemy attack hits, via the effect's position selector
        /// (defaulting to FrontMost) evaluated against the living party formation at execution time.</summary>
        private static PartyMember SelectPartyTarget(EffectContext ctx)
        {
            var selector = ctx.Effect?.TargetSelector ?? Cards.TargetSelector.FrontMost;
            return PartyTargeting.Select(ctx.State, selector);
        }

        /// <summary>Every currently-living party member (a snapshot taken at resolution time, so
        /// mid-loop deaths from earlier hits in the same All sweep can't change who's hit next).</summary>
        private static List<PartyMember> AllLivingParty(CombatState state)
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
