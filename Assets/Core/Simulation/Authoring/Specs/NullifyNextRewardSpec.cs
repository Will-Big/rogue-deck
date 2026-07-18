using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Nullifies the next player condition reward. No parameters.</summary>
    [Serializable]
    public sealed class NullifyNextRewardSpec : EffectSpec
    {
        public override EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public override EffectData ToEffectData() => ApplyCondition(new EffectData(Key, 0));

        public override string ToLiteral()
            => "new NullifyNextRewardSpec { " + ConditionLiteral() + " }";
    }
}
