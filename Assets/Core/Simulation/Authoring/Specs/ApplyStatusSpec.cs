using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Applies a status effect to the selected target(s).</summary>
    [Serializable]
    public sealed class ApplyStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public int Value;
        public StatusLifetimeKind Lifetime;
        public int LifetimeCount;
        public StatusApplyTarget Target;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Value)
            {
                Payload = new ApplyStatusPayload(Status.ToKey(), ToLifetime(), Target)
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "apply_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }
        }

        public override string ToLiteral()
            => "new ApplyStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", Value = " + Value
                + ", Lifetime = StatusLifetimeKind." + Lifetime
                + ", LifetimeCount = " + LifetimeCount
                + ", Target = StatusApplyTarget." + Target
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";

        private StatusLifetime ToLifetime()
        {
            switch (Lifetime)
            {
                case StatusLifetimeKind.Permanent: return StatusLifetime.Permanent;
                case StatusLifetimeKind.Turns: return StatusLifetime.Turns(LifetimeCount);
                case StatusLifetimeKind.UntilConsumed: return StatusLifetime.UntilConsumed(LifetimeCount);
                default: return StatusLifetime.ThisTurn;
            }
        }
    }
}
