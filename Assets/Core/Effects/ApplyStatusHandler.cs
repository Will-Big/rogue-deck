using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Where an ApplyStatus effect puts the status, from the acting card's perspective.</summary>
    public enum StatusApplyTarget
    {
        Self,            // the card's own side entity: player card -> its OwnerId party member; enemy card -> itself
        TargetEnemy,     // the card's target enemy (by TargetId, else the first enemy)
        PartyMember,     // an explicitly chosen living party member (by TargetId)
        AllPartyMembers  // every living party member, applied as independent per-member instances
    }

    /// <summary>Applies a status (key + lifetime + magnitude) to one or more holders. Magnitude rides on
    /// the resolved EffectValue (e.g. block points). Target resolution is strict: when the effect's
    /// target cannot be resolved (dead/missing member, ambiguous ownerless Self, etc.) the card is
    /// cancelled (NoValidTarget) with no partial application and no front-of-formation fallback.</summary>
    public sealed class ApplyStatusHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            var effect = ctx.Effect;
            if (effect == null || effect.StatusKey == null || effect.StatusLifetime == null)
            {
                return;
            }

            switch (effect.StatusTarget)
            {
                case StatusApplyTarget.Self:
                    ApplySelf(ctx, effect);
                    break;
                case StatusApplyTarget.TargetEnemy:
                    ApplyTargetEnemy(ctx, effect);
                    break;
                case StatusApplyTarget.PartyMember:
                    ApplyPartyMember(ctx, effect);
                    break;
                case StatusApplyTarget.AllPartyMembers:
                    ApplyAllPartyMembers(ctx, effect);
                    break;
            }
        }

        private static void ApplySelf(EffectContext ctx, EffectData effect)
        {
            if (ctx.Card.Def.Side == Side.Player)
            {
                var member = ResolvePlayerSelf(ctx.State, ctx.Card.OwnerId);
                if (member == null)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                member.Statuses.Add(effect.StatusKey.Value, effect.StatusLifetime.Value, ctx.EffectValue);
                return;
            }

            var enemy = ResolveEnemySelf(ctx.State, ctx.Card.OwnerId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            enemy.Statuses.Add(effect.StatusKey.Value, effect.StatusLifetime.Value, ctx.EffectValue);
        }

        private static void ApplyTargetEnemy(EffectContext ctx, EffectData effect)
        {
            var enemy = SelectTargetEnemy(ctx.State, ctx.Card.TargetId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            enemy.Statuses.Add(effect.StatusKey.Value, effect.StatusLifetime.Value, ctx.EffectValue);
        }

        private static void ApplyPartyMember(EffectContext ctx, EffectData effect)
        {
            var member = PartyTargeting.LivingById(ctx.State, ctx.Card.TargetId);
            if (member == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            member.Statuses.Add(effect.StatusKey.Value, effect.StatusLifetime.Value, ctx.EffectValue);
        }

        /// <summary>Applies the status to every currently-living party member as an independent bag
        /// entry (a snapshot taken at resolution time, so mid-loop deaths can't change who's hit).</summary>
        private static void ApplyAllPartyMembers(EffectContext ctx, EffectData effect)
        {
            var living = new List<PartyMember>();
            foreach (var member in ctx.State.Party)
            {
                if (member.IsAlive)
                {
                    living.Add(member);
                }
            }

            if (living.Count == 0)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            foreach (var member in living)
            {
                member.Statuses.Add(effect.StatusKey.Value, effect.StatusLifetime.Value, ctx.EffectValue);
            }
        }

        /// <summary>Player-side Self: the card's OwnerId party member if alive; with no OwnerId, only the
        /// pre-party legacy single-player shim falls back to the sole party member. Any other ownerless
        /// case (multi-party, or a single non-legacy party member) is undefined and cancels.</summary>
        private static PartyMember ResolvePlayerSelf(CombatState state, string ownerId)
        {
            if (!string.IsNullOrEmpty(ownerId))
            {
                return PartyTargeting.LivingById(state, ownerId);
            }

            if (state.Party.Count == 1 && state.Party[0].Id == CombatState.LegacyPlayerId)
            {
                return state.Party[0];
            }

            return null;
        }

        /// <summary>Enemy-side Self: the card's OwnerId enemy if alive; with no OwnerId, only a single
        /// enemy in the fight resolves unambiguously (existing single-enemy runner compat). Two or more
        /// ownerless enemies have no front-of-formation fallback and cancel instead.</summary>
        private static Enemy ResolveEnemySelf(CombatState state, string ownerId)
        {
            if (!string.IsNullOrEmpty(ownerId))
            {
                return FindLivingEnemy(state, ownerId);
            }

            return state.Enemies.Count == 1 ? state.Enemies[0] : null;
        }

        private static Enemy FindLivingEnemy(CombatState state, string enemyId)
        {
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id == enemyId && enemy.Hp > 0)
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>Picks the card's target enemy by id, else the first enemy. An explicit id that no
        /// longer matches any enemy resolves to no target rather than falling back to the front.</summary>
        private static Enemy SelectTargetEnemy(CombatState state, string targetId)
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
