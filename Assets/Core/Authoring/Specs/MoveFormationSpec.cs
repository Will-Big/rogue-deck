using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대형을 EffectValue만큼 옮긴다(음수 = 앞으로). 대상은 카드 쪽 진영의 Self다.</summary>
    public sealed class MoveFormationSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        public override EffectKey Key => EffectKeys.MoveFormation;

        public override bool IsTargeted => true;

        protected override EffectData Build() => new EffectData(Key, EffectValue);

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
        {
            if (target.Faction != OwnFaction(cardSide) || target.Range != CardTargetRange.Self)
            {
                yield return "move_formation moves only the card's own Self.";
            }
        }
    }
}
