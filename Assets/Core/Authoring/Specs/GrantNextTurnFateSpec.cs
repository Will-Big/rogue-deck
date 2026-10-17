using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>다음 플레이어 사용 턴에 운명력 EffectValue를 준다 (증류).</summary>
    public sealed class GrantNextTurnFateSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        public override EffectKey Key => EffectKeys.GrantNextTurnFate;

        protected override EffectData Build() => new EffectData(Key, EffectValue);
    }
}
