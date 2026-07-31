using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 적의 상태 틱을 즉시 발동시키고 이번 턴 종료 발동을 마커로 막는다 (조기 발병).</summary>
    [Serializable]
    public sealed class TriggerStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public StatusKeyRef SuppressMarker;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.TriggerStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, 0)
            {
                Payload = new TriggerStatusPayload(Status.ToKey(), SuppressMarker.ToKey())
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty || !context.HasStatus(Status.ToKey()))
            {
                yield return "trigger_status spec requires a known status key.";
            }

            if (SuppressMarker.IsEmpty || !context.HasStatus(SuppressMarker.ToKey()))
            {
                yield return "trigger_status spec requires a known suppress-marker key.";
            }
        }

        public override string ToLiteral()
            => "new TriggerStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", SuppressMarker = new StatusKeyRef { Id = " + Quote(SuppressMarker.Id) + " }"
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
