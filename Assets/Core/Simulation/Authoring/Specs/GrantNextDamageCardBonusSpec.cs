using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Grants a damage bonus to the next player damage card.</summary>
    [Serializable]
    public sealed class GrantNextDamageCardBonusSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, Value));

        public override string ToLiteral()
            => "new GrantNextDamageCardBonusSpec { Value = " + Value + ", "
                + ConditionLiteral() + " }";
    }
}
