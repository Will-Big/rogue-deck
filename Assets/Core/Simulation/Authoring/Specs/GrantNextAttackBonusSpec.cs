using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Grants a damage bonus to the next player attack.</summary>
    [Serializable]
    public sealed class GrantNextAttackBonusSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.GrantNextPlayerAttackDamageBonus;

        public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, Value));

        public override string ToLiteral()
            => "new GrantNextAttackBonusSpec { Value = " + Value + ", " + ConditionLiteral() + " }";
    }
}
