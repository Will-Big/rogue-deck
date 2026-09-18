using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Moves the formation by <see cref="Value"/> (negative = forward). 대상은 카드 쪽 진영의 Self다.</summary>
    public sealed class MoveFormationSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.MoveFormation;

        public override bool IsTargeted => true;

        protected override EffectData Build() => new EffectData(Key, Value);

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
        {
            if (target.Faction != OwnFaction(cardSide) || target.Range != CardTargetRange.Self)
            {
                yield return "move_formation moves only the card's own Self.";
            }
        }
    }
}
