using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 적의 상태 틱을 즉시 발동시키고 이번 턴 종료 발동을 막는다 (조기 발병). 어떤
    /// 마커로 막는지는 상태 자신의 behavior가 안다(StatusBehavior.SuppressThisTurn) — 카드는 더 이상
    /// 마커 키를 저작하지 않는다.</summary>
    [Serializable]
    public sealed class TriggerStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.TriggerStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, 0)
            {
                Payload = new TriggerStatusPayload(Status.ToKey())
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty || !context.HasStatus(Status.ToKey()))
            {
                yield return "trigger_status spec requires a known status key.";
            }
        }
    }
}
