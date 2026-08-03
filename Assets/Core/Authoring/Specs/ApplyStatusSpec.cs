using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Statuses;
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
            else if (!StatusContentDefaults.HasContent(Status.ToKey()))
            {
                // 행동 레지스트리에는 있지만(HasStatus 통과) 저작 카탈로그에는 없는 상태 —
                // ApplyStatusHandler가 해결 시점에 StatusContentCatalog.LifetimeOf를 호출하므로
                // 여기서 막지 않으면 KeyNotFoundException으로 죽는다.
                yield return "status '" + Status.Id + "' has no authored content.";
            }
        }

        public override string ToLiteral()
            => "new ApplyStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", Count = " + Count
                + ", Target = StatusApplyTarget." + Target
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
