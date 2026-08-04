using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Applies a status effect to the selected target(s).</summary>
    [Serializable]
    public sealed class ApplyStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;

        /// <summary>이 카드가 거는 양. 뜻은 상태가 정한다 — 수명이 Permanent·ThisTurn이면 세기,
        /// Turns·UntilConsumed면 지속.</summary>
        public int Count;

        public StatusApplyTarget Target;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Count)
            {
                Payload = new ApplyStatusPayload(Status.ToKey(), Target)
            }) with { TargetSelector = ToSelector(Selector) };

        /// <summary>저작 콘텐츠 존재 여부는 검사하지 않는다. StatusContentLoader가 "등록된 모든
        /// 상태에 저작이 있다"를 요구하고 부팅이 상태를 카드보다 먼저 읽으므로, 여기 도달한
        /// 시점에는 HasStatus가 곧 저작 존재다 — 가드를 두면 같은 불변식을 두 곳에서 지키게 된다.</summary>
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
    }
}
