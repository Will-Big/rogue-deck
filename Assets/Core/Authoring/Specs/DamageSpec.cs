using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>상대 진영의 카드 위치 규칙이 고른 대상에게 고정 피해를 준다.</summary>
    [Serializable]
    public sealed class DamageSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.Damage;

        public override bool IsTargeted => true;

        protected override EffectData Build() => new EffectData(Key, Value);

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, OpposingFaction(cardSide));
    }
}
