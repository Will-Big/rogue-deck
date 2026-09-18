using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Where an ApplyStatus effect puts the status, from the acting card's perspective.</summary>
    public enum StatusApplyTarget
    {
        Self,             // the card's own side entity: player card -> its OwnerId party member; enemy card -> itself
        TargetEnemy,      // enemies at the effect's TargetSelector position (null = FrontOne)
        PartyMember,      // 명시 선택한 파티원 — 효과 단위 대상 선택(계획 T3)에서 실행 경로가 없어졌다. T3b에서 지운다
        AllPartyMembers,  // every living party member, applied as independent per-member instances
        PartyBySelector   // 아군 위치 범위 — effect.TargetSelector로 확정, null이면 FrontOne
    }

    /// <summary>Applies a status (key + lifetime + magnitude) to every target this effect chose at its start.
    /// Magnitude rides on the resolved EffectValue (e.g. block points). Which holders are chosen comes from
    /// the payload's StatusApplyTarget and the effect's TargetSelector (TargetFor); a Self whose owner is
    /// dead or ambiguous has no target, so the effect is not applied.</summary>
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
                        EffectTargetResolver.RangeFor(effect.TargetSelector ?? Cards.TargetSelector.FrontOne));
                case StatusApplyTarget.PartyBySelector:
                    return new CardTargetKey(
                        CardTargetFaction.Ally,
                        EffectTargetResolver.RangeFor(effect.TargetSelector ?? Cards.TargetSelector.FrontOne));
                case StatusApplyTarget.AllPartyMembers:
                    return new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.All);
                default:
                    throw new System.NotSupportedException(
                        "apply_status target " + payload.Target + " has no position rule.");
            }
        }

        public void Apply(EffectContext ctx)
        {
            if (!(ctx.Effect?.Payload is ApplyStatusPayload payload))
            {
                return;
            }

            foreach (var target in ctx.Targets.Party)
            {
                ApplyTo(ctx, payload, target.Statuses, target.Id);
            }

            foreach (var target in ctx.Targets.Enemies)
            {
                ApplyTo(ctx, payload, target.Statuses, target.Id);
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

            if (payload.Target == StatusApplyTarget.PartyMember)
            {
                yield return "apply_status PartyMember target has no position rule.";
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
        private static void ApplyTo(
            EffectContext ctx, ApplyStatusPayload payload, StatusBag bag, string holderId)
        {
            var lifetimeKind = ctx.State.StatusContent.LifetimeOf(payload.Key);
            var countIsDuration = ctx.State.StatusContent.CountIsDuration(payload.Key);
            var lifetime = countIsDuration
                ? StatusLifetime.Of(lifetimeKind, ctx.EffectValue)
                : StatusLifetime.Of(lifetimeKind, 0);
            var baseMagnitude = countIsDuration ? 0 : ctx.EffectValue;

            var magnitude = StatusDamageFold.GainedMagnitude(
                payload.Key, bag, ctx.StatusRegistry, ctx.State.StatusRules, baseMagnitude);

            StatusInstance instance;
            bool stacked;
            if (ctx.StatusRegistry != null
                && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior)
                && behavior.StacksMagnitude)
            {
                stacked = bag.Has(payload.Key);
                instance = bag.Stack(payload.Key, lifetime, magnitude);
            }
            else
            {
                stacked = false;
                bag.Add(payload.Key, lifetime, magnitude);
                instance = bag.Get(payload.Key);
            }

            ctx.ExtraEvents.Add(new StatusApplied(
                holderId, payload.Key.Id, instance.Count, instance.Magnitude, stacked));
        }
    }
}
