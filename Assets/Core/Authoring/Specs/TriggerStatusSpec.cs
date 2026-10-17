using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 적의 상태 틱을 즉시 발동시키고 이번 턴 종료 발동을 막는다 (조기 발병). 어떤
    /// 마커로 막는지는 상태 자신의 behavior가 안다(StatusBehavior.SuppressThisTurn) — 카드는 더 이상
    /// 마커 키를 저작하지 않는다. 세기 숫자가 없으므로 EffectValue 필드를 두지 않는다(효과 JSON 설계 P1).</summary>
    public sealed class TriggerStatusSpec : EffectSpec
    {
        [JsonProperty(Order = 60)]
        public TriggerStatusPayloadSpec Payload;

        public override EffectKey Key => EffectKeys.TriggerStatus;

        public override bool IsTargeted => true;

        protected override EffectData Build()
            => new EffectData(Key, 0) { Payload = new TriggerStatusPayload(Payload.Key.ToKey()) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "trigger_status requires a payload.";
                yield break;
            }

            if (Payload.Key.IsEmpty || !context.HasStatus(Payload.Key.ToKey()))
            {
                yield return "trigger_status spec requires a known status key.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, CardTargetFaction.Enemy);
    }

    /// <summary>TriggerStatusPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class TriggerStatusPayloadSpec
    {
        public StatusKeyRef Key;
    }
}
