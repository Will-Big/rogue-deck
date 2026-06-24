using System;
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
        public int Cost { get; }
        public Side Side { get; }
        public string Description { get; }
        public Sprite Art { get; }
        public bool IsLocked { get; }

        public CardPresentation(
            string id, string displayName, int initiative, int cost, Side side,
            string description, Sprite art, bool isLocked)
        {
            Id = id;
            DisplayName = displayName;
            Initiative = initiative;
            Cost = cost;
            Side = side;
            Description = description;
            Art = art;
            IsLocked = isLocked;
        }

        /// <summary>Zone card (placed instance) — shows its current initiative. <paramref name="art"/> resolves
        /// the sprite by id (e.g. from the authored CardAsset.Art); null falls back to Resources lookup.</summary>
        public static CardPresentation From(ActionCardInstance card, Func<string, Sprite> art = null)
        {
            var def = card.Def;
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                card.Initiative,
                def.Cost,
                def.Side,
                PlaytestKoreanText.CardDescription(def.Id),
                ResolveArt(def.Id, art),
                card.IsLocked);
        }

        /// <summary>Hand card (definition) — initiative is the base value; cost is the key number.</summary>
        public static CardPresentation FromDefinition(CardDefinition def, Func<string, Sprite> art = null)
        {
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                def.BaseInitiative,
                def.Cost,
                def.Side,
                PlaytestKoreanText.CardDescription(def.Id),
                ResolveArt(def.Id, art),
                false);
        }

        // A resolver (GUID-backed CardAsset.Art) wins; with none supplied we fall back to the Resources path.
        private static Sprite ResolveArt(string id, Func<string, Sprite> art)
            => art != null ? art(id) : PlaytestCardArt.Sprite(id);
    }
}
