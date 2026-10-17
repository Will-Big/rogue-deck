using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>적 진영 위치 규칙이 고른 적의 상태를 EffectValue만큼 Mode 방식으로 소비한다(스펙 §5).
    /// 실제 소비량은 이 효과의 결과가 되고, 뒤 효과가 requirement·scaling으로 이 효과의 id를 가리켜 읽는다.</summary>
    public sealed class ConsumeStatusSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        [JsonProperty(Order = 60)]
        public ConsumeStatusPayloadSpec Payload;

        public override EffectKey Key => EffectKeys.ConsumeStatus;

        public override bool IsTargeted => true;

        public override bool ProducesConsumption => true;

        protected override EffectData Build()
            => new EffectData(Key, EffectValue)
            {
                Payload = new ConsumeStatusPayload(Payload.Key.ToKey(), Payload.Mode)
            };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (EffectValue < 1)
            {
                yield return "consume_status effectValue must be at least 1.";
            }

            if (Payload == null)
            {
                yield return "consume_status requires a payload.";
                yield break;
            }

            if (Payload.Key.IsEmpty)
            {
                yield return "consume_status payload requires a status key.";
            }
            else if (!context.HasStatus(Payload.Key.ToKey()))
            {
                yield return "Unknown status key '" + Payload.Key.Id + "'.";
            }

            if (!Enum.IsDefined(typeof(ConsumptionMode), Payload.Mode))
            {
                yield return "consume_status mode must be UpTo or Exact.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, CardTargetFaction.Enemy);
    }

    /// <summary>ConsumeStatusPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class ConsumeStatusPayloadSpec
    {
        public StatusKeyRef Key;

        /// <summary>UpTo가 enum 기본값이라 생략되면 조용히 UpTo가 된다. 소비 방식은 카드의 뜻을 가르므로
        /// 항상 쓴다.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public ConsumptionMode Mode;
    }
}
