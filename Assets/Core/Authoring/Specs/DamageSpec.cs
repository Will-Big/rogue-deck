using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>상대 진영의 카드 위치 규칙이 고른 대상에게 고정 피해를 준다. Traits는 이 피해의 속성들이다
    /// (관통·배율 무시, 여러 개 가능 — 계획 D10). 생략하면 보통 피해다.</summary>
    [Serializable]
    public sealed class DamageSpec : EffectSpec
    {
        public int Value;

        public DamageTrait[] Traits;

        public override EffectKey Key => EffectKeys.Damage;

        public override bool IsTargeted => true;

        /// <summary>빈 목록도 생략한다 — 편집 도구와 같은 규칙.</summary>
        public bool ShouldSerializeTraits() => Traits != null && Traits.Length > 0;

        protected override EffectData Build()
        {
            var traits = DamageTraits.Of(Traits);
            return new EffectData(Key, Value)
            {
                Payload = traits.Equals(DamageTraits.Normal) ? null : new DamagePayload(traits)
            };
        }

        public override IEnumerable<string> Validate(AuthoringContext context)
            => DamageTraitRules.Validate(Traits, "damage traits");

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, OpposingFaction(cardSide));
    }
}
