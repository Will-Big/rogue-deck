using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Applies a status (key + lifetime + magnitude) to every target this effect chose at its start.
    /// Magnitude rides on the resolved EffectValue (e.g. block points). Which holders are chosen comes from
    /// the effect's faction and the card's range on that side; a Self whose owner is dead or ambiguous has
    /// no target, so the effect is not applied.</summary>
    public sealed class ApplyStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public void Apply(EffectContext ctx)
        {
            if (!(ctx.Effect?.Payload is ApplyStatusPayload payload))
            {
                return;
            }

            foreach (var target in ctx.RequireTargets().Party)
            {
                ApplyTo(ctx, payload, target.Statuses, target.Id);
            }

            foreach (var target in ctx.RequireTargets().Enemies)
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
            ctx.Signals.Add(new CombatSignal(CombatSignalKeys.StatusGained, ctx.ActorId, holderId, magnitude)
            {
                Detail = payload.Key.Id
            });
        }
    }
}
