using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>상대 진영의 카드 위치 규칙이 고른 대상에게 고정 피해를 준다. Payload.Traits는 이 피해의
    /// 속성들이다(관통·배율 무시, 여러 개 가능 — 계획 D10). 생략하면 보통 피해다.</summary>
    public sealed class DamageSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        [JsonProperty(Order = 60)]
        public DamagePayloadSpec Payload;

        public override EffectKey Key => EffectKeys.Damage;

        public override bool IsTargeted => true;

        protected override EffectData Build()
        {
            var traits = DamageTraits.Of(Payload?.Traits);
            return new EffectData(Key, EffectValue)
            {
                Payload = traits.Equals(DamageTraits.Normal) ? null : new DamagePayload(traits)
            };
        }

        /// <summary>보통 피해는 payload를 생략하는 한 가지 형태로만 쓴다. 속성 없는 payload를 허용하면
        /// 같은 뜻을 두 모양으로 저작할 수 있고, 다시 쓸 때 원본과 바이트가 달라진다(효과 JSON 설계 P3).</summary>
        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload != null && (Payload.Traits == null || Payload.Traits.Length == 0))
            {
                yield return "damage payload needs traits; omit payload for normal damage.";
            }

            foreach (var error in DamageTraitRules.Validate(Payload?.Traits, "damage traits"))
            {
                yield return error;
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, OpposingFaction(cardSide));
    }

    /// <summary>DamagePayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class DamagePayloadSpec
    {
        public DamageTrait[] Traits;
    }
}
