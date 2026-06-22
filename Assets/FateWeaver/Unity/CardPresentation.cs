using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>UI-facing snapshot of a card so CardView never touches core types directly.</summary>
    public readonly struct CardPresentation
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Initiative { get; }
        public Side Side { get; }
        public string Description { get; }
        public Sprite Art { get; }
        public bool IsLocked { get; }

        public CardPresentation(
            string id, string displayName, int initiative, Side side,
            string description, Sprite art, bool isLocked)
        {
            Id = id;
            DisplayName = displayName;
            Initiative = initiative;
            Side = side;
            Description = description;
            Art = art;
            IsLocked = isLocked;
        }

        public static CardPresentation From(ActionCardInstance card)
        {
            var def = card.Def;
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                card.Initiative,
                def.Side,
                PlaytestKoreanText.CardDescription(def.Id),
                PlaytestCardArt.Sprite(def.Id),
                card.IsLocked);
        }
    }
}
