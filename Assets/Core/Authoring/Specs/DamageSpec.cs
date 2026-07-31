using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Deals flat damage to the target selected by <see cref="Selector"/> (or the handler's
    /// default when unset).</summary>
    [Serializable]
    public sealed class DamageSpec : EffectSpec
    {
        public int Value;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.Damage;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Value)) with { TargetSelector = ToSelector(Selector) };

        public override string ToLiteral()
            => "new DamageSpec { Value = " + Value
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
