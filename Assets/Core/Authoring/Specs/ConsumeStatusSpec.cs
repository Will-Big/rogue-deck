using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>적 진영 위치 규칙이 고른 적의 상태를 Amount만큼 Mode 방식으로 소비한다(스펙 §5).
    /// 실제 소비량은 이 효과의 결과가 되고, 뒤 효과가 requires·scaleBy로 이 효과의 id를 가리켜 읽는다.</summary>
    [Serializable]
    public sealed class ConsumeStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public int Amount;

        /// <summary>UpTo가 enum 기본값이라 생략되면 조용히 UpTo가 된다. 소비 방식은 카드의 뜻을 가르므로
        /// 항상 쓴다.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public ConsumptionMode Mode;

        public override EffectKey Key => EffectKeys.ConsumeStatus;

        public override bool IsTargeted => true;

        public override bool ProducesConsumption => true;

        protected override EffectData Build(Side cardSide, CardTargetKey? target)
            => new EffectData(Key, 0)
            {
                Payload = new ConsumeStatusPayload(Status.ToKey(), Amount, Mode),
                TargetSelector = SelectorFor(target.Value.Range)
            };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "consume_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }

            if (Amount < 1)
            {
                yield return "consume_status amount must be at least 1.";
            }

            if (!Enum.IsDefined(typeof(ConsumptionMode), Mode))
            {
                yield return "consume_status mode must be UpTo or Exact.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, CardTargetFaction.Enemy);
    }
}
