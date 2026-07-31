using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Moves the formation by <see cref="Value"/> (negative = forward).</summary>
    [Serializable]
    public sealed class MoveFormationSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.MoveFormation;

        public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, Value));

        public override string ToLiteral()
            => "new MoveFormationSpec { Value = " + Value + ", " + ConditionLiteral() + " }";
    }
}
