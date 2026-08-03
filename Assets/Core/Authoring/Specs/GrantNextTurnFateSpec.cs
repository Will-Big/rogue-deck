using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>다음 플레이어 사용 턴에 운명력 Value를 준다 (증류).</summary>
    [Serializable]
    public sealed class GrantNextTurnFateSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.GrantNextTurnFate;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Value));
    }
}
