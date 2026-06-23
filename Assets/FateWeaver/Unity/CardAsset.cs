using System;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Inspector-authored card. The single source of truth for card data; converts to a pure
    /// CardSpec for the rules layer. Art/Description are presentation-only (not part of CardSpec).</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Card", fileName = "Card")]
    public sealed class CardAsset : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Side Side;
        public CardType Type;
        public CardCategory Category;
        public int Cost = 1;
        public int BaseInitiative = 5;
        public Sprite Art;
        [TextArea] public string Description;
        public EffectSpec[] Effects = Array.Empty<EffectSpec>();
        public FateKind Fate;
        public int FateAmount;

        public CardSpec ToSpec() => new CardSpec
        {
            Id = Id,
            Name = DisplayName,
            Side = Side,
            Type = Type,
            Category = Category,
            Cost = Cost,
            BaseInitiative = BaseInitiative,
            Effects = Effects,
            Fate = Fate,
            FateAmount = FateAmount
        };
    }
}
