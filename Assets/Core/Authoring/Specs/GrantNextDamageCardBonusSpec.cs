using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>다음 플레이어 피해 카드에 피해 보너스 EffectValue를 준다.</summary>
    public sealed class GrantNextDamageCardBonusSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        public override EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        protected override EffectData Build() => new EffectData(Key, EffectValue);
    }
}
