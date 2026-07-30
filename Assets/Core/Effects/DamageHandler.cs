using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Player cards hit an enemy target resolved by the effect's TargetSelector (position in
    /// the living formation, or All for every living enemy); with no selector, falls back to the
    /// legacy path (explicit id, else the raw first enemy) for pre-selector content. Enemy cards hit a
    /// party member chosen the same way — by the effect's TargetSelector (null defaults to FrontMost,
    /// for pre-party compat). Incoming damage is folded through the target's entity-scoped statuses
    /// (e.g. Vulnerable, Block) when a StatusRegistry is present; with no registry it applies raw. If no
    /// target can be resolved the card is cancelled (NoValidTarget) and nothing is mutated.</summary>
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
                    // Deliberate rule: a pending damage bonus (GrantNextPlayerDamageCardBonus) raises
                    // the CARD's damage value, not a fixed pool split across targets — so with an
                    // All-target card it applies to EVERY target independently ("다음 플레이어 피해
                    // 카드가 주는 피해 +X" reads per hit dealt, not a one-time budget). E.g. +3 bonus
                    // on a base-2 All-target card deals 5 to each enemy, not 2 to one and 3 total spread.
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

        /// <summary>Folds the target's entity-scoped statuses into incoming damage: the multiplier
        /// layer first, then the absorb layer (see StatusDamageFold). An UntilConsumed status that
        /// actually changed the damage spends a charge (auto-consume).</summary>
        private static int FoldIncoming(EffectContext ctx, StatusBag bag, int damage)
            => StatusDamageFold.Incoming(bag, ctx.StatusRegistry, damage);
    }
}
