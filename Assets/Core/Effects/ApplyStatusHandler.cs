using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Where an ApplyStatus effect puts the status, from the acting card's perspective.</summary>
    public enum StatusApplyTarget
    {
        Self,             // the card's own side entity: player card -> its OwnerId party member; enemy card -> itself
        TargetEnemy,      // the card's target enemy (by TargetId, else the first enemy; or by TargetSelector)
        PartyMember,      // an explicitly chosen living party member (by TargetId)
        AllPartyMembers,  // every living party member, applied as independent per-member instances
        PartyBySelector   // 아군 위치 범위 — effect.TargetSelector로 확정, null이면 FrontOne
    }

    /// <summary>Applies a status (key + lifetime + magnitude) to one or more holders. Magnitude rides on
    /// the resolved EffectValue (e.g. block points). Target resolution is strict: when the effect's
    /// target cannot be resolved (dead/missing member, ambiguous ownerless Self, etc.) the card is
    /// cancelled (NoValidTarget) with no partial application and no front-of-formation fallback.</summary>
    public sealed class ApplyStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public CardTargetKey? TargetFor(CardDefinition card, EffectData effect)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
            {
                return null;
            }

            switch (payload.Target)
            {
                case StatusApplyTarget.Self:
                    return new CardTargetKey(
                        card.Side == Side.Player ? CardTargetFaction.Ally : CardTargetFaction.Enemy,
                        CardTargetRange.Self);
                case StatusApplyTarget.TargetEnemy:
                    return new CardTargetKey(
                        CardTargetFaction.Enemy,
                        CardTargetSnapshot.RangeFor(effect.TargetSelector ?? Cards.TargetSelector.FrontOne));
                case StatusApplyTarget.PartyBySelector:
                    return new CardTargetKey(
                        CardTargetFaction.Ally,
                        CardTargetSnapshot.RangeFor(effect.TargetSelector ?? Cards.TargetSelector.FrontOne));
                case StatusApplyTarget.AllPartyMembers:
                    return new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.All);
                case StatusApplyTarget.PartyMember:
                    return null;
                default:
                    return null;
            }
        }

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (!(ctx.Effect?.Payload is ApplyStatusPayload payload))
            {
                return;
            }

            if (ctx.Targets != null && TargetFor(ctx.Card.Def, ctx.Effect).HasValue)
            {
                ApplySnapshotTargets(ctx, payload, TargetFor(ctx.Card.Def, ctx.Effect).Value);
                return;
            }

            switch (payload.Target)
            {
                case StatusApplyTarget.Self:
                    ApplySelf(ctx, payload);
                    break;
                case StatusApplyTarget.TargetEnemy:
                    ApplyTargetEnemy(ctx, payload);
                    break;
                case StatusApplyTarget.PartyMember:
                    ApplyPartyMember(ctx, payload);
                    break;
                case StatusApplyTarget.AllPartyMembers:
                    ApplyAllPartyMembers(ctx, payload);
                    break;
                case StatusApplyTarget.PartyBySelector:
                    ApplyPartyBySelector(ctx, payload);
                    break;
            }
        }

        public System.Collections.Generic.IEnumerable<string> ValidateData(EffectData effect)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
            {
                yield return "apply_status effect requires an ApplyStatusPayload.";
                yield break;
            }

            if (string.IsNullOrEmpty(payload.Key.Id))
            {
                yield return "apply_status payload requires a status key.";
            }
        }

        /// <summary>Stacking-aware status application: when the key's behavior declares
        /// StacksMagnitude (e.g. Block), an existing instance's Magnitude is added to rather than
        /// replaced; otherwise falls back to the legacy replace semantics. The magnitude is first
        /// folded through the RECEIVING holder's statuses (e.g. Damaged reducing block gain).
        ///
        /// The card gives exactly one number (ctx.EffectValue, already resolved for any conditional
        /// SuccessEffectValue override). Its meaning is derived from the status's catalog lifetime kind:
        /// Permanent/ThisTurn treat it as magnitude; Turns/UntilConsumed treat it as duration.</summary>
        private static void ApplyTo(EffectContext ctx, ApplyStatusPayload payload, StatusBag bag)
        {
            var lifetimeKind = ctx.State.StatusContent.LifetimeOf(payload.Key);
            var countIsDuration = ctx.State.StatusContent.CountIsDuration(payload.Key);
            var lifetime = countIsDuration
                ? StatusLifetime.Of(lifetimeKind, ctx.EffectValue)
                : StatusLifetime.Of(lifetimeKind, 0);
            var baseMagnitude = countIsDuration ? 0 : ctx.EffectValue;

            var magnitude = StatusDamageFold.GainedMagnitude(
                payload.Key, bag, ctx.StatusRegistry, ctx.State.StatusRules, baseMagnitude);

            if (ctx.StatusRegistry != null
                && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior)
                && behavior.StacksMagnitude)
            {
                bag.Stack(payload.Key, lifetime, magnitude);
                return;
            }

            bag.Add(payload.Key, lifetime, magnitude);
        }

        private static void ApplySnapshotTargets(
            EffectContext ctx,
            ApplyStatusPayload payload,
            CardTargetKey key)
        {
            var affected = 0;
            string onlyTargetId = null;
            if (key.Faction == CardTargetFaction.Ally)
            {
                foreach (var target in ctx.Targets.PartyTargets(key))
                {
                    if (!target.IsAlive)
                    {
                        continue;
                    }

                    ApplyTo(ctx, payload, target.Statuses);
                    onlyTargetId = target.Id;
                    affected++;
                }
            }
            else
            {
                foreach (var target in ctx.Targets.EnemyTargets(key))
                {
                    if (target.Hp <= 0)
                    {
                        continue;
                    }

                    ApplyTo(ctx, payload, target.Statuses);
                    onlyTargetId = target.Id;
                    affected++;
                }
            }

            ctx.TargetId = affected == 1 ? onlyTargetId : null;
            if (affected == 0)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
            }
        }

        private static void ApplySelf(EffectContext ctx, ApplyStatusPayload payload)
        {
            if (ctx.Card.Def.Side == Side.Player)
            {
                var member = ResolvePlayerSelf(ctx.State, ctx.Card.OwnerId);
                if (member == null)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                ApplyTo(ctx, payload, member.Statuses);
                return;
            }

            var enemy = ResolveEnemySelf(ctx.State, ctx.Card.OwnerId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            ApplyTo(ctx, payload, enemy.Statuses);
        }

        private static void ApplyTargetEnemy(EffectContext ctx, ApplyStatusPayload payload)
        {
            if (ctx.Effect?.TargetSelector == Cards.TargetSelector.All)
            {
                var targets = EnemyTargeting.SelectAll(ctx.State);
                if (targets.Count == 0)
                {
                    ctx.Cancel(CardCancellationReason.NoValidTarget);
                    return;
                }

                foreach (var each in targets)
                {
                    ApplyTo(ctx, payload, each.Statuses);
                }

                return;
            }

            var enemy = ctx.Effect?.TargetSelector is Cards.TargetSelector selector
                ? EnemyTargeting.Select(ctx.State, selector)
                : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            ApplyTo(ctx, payload, enemy.Statuses);
        }

        private static void ApplyPartyMember(EffectContext ctx, ApplyStatusPayload payload)
        {
            var member = PartyTargeting.LivingById(ctx.State, ctx.Card.TargetId);
            if (member == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            ApplyTo(ctx, payload, member.Statuses);
        }

        /// <summary>Applies the status to every currently-living party member as an independent bag
        /// entry (a snapshot taken at resolution time, so mid-loop deaths can't change who's hit).</summary>
        private static void ApplyAllPartyMembers(EffectContext ctx, ApplyStatusPayload payload)
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
                ApplyTo(ctx, payload, member.Statuses);
            }
        }

        /// <summary>아군 위치 범위: effect.TargetSelector(기본 FrontOne)로 생존 파티 대형에서 확정된
        /// 한 명에게 적용한다.</summary>
        private static void ApplyPartyBySelector(EffectContext ctx, ApplyStatusPayload payload)
        {
            var selector = ctx.Effect?.TargetSelector ?? Cards.TargetSelector.FrontOne;
            var member = PartyTargeting.Select(ctx.State, selector);
            if (member == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            ApplyTo(ctx, payload, member.Statuses);
        }

        /// <summary>Player-side Self: the card's OwnerId party member if alive; with no OwnerId, only a
        /// single party member resolves unambiguously. Two or more ownerless members cancel instead.</summary>
        private static PartyMember ResolvePlayerSelf(CombatState state, string ownerId)
        {
            if (!string.IsNullOrEmpty(ownerId))
            {
                return PartyTargeting.LivingById(state, ownerId);
            }

            if (state.Party.Count == 1)
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
    }
}
