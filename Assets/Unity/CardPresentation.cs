using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>UI-facing snapshot of a card so CardView never touches core types directly.</summary>
    public readonly struct CardPresentation
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int ExecutionOrder { get; }
        public int EnergyCost { get; }
        public Side Side { get; }
        public string Description { get; }
        public Sprite Art { get; }
        public bool IsLocked { get; }
        public CardCategory Category { get; }
        public IReadOnlyList<CardStatusIcon> StatusIcons { get; }
        public string OwnerDisplayName { get; }
        public Color OwnerColor { get; }
        public bool IsPartyOwned { get; }

        public CardPresentation(
            string id, string displayName, int executionOrder, int energyCost, Side side,
            string description, Sprite art, bool isLocked,
            IReadOnlyList<CardStatusIcon> statusIcons = null,
            CardCategory category = CardCategory.Execution,
            string ownerDisplayName = null,
            Color ownerColor = default,
            bool isPartyOwned = false)
        {
            Id = id;
            DisplayName = displayName;
            ExecutionOrder = executionOrder;
            EnergyCost = energyCost;
            Side = side;
            Description = description;
            Art = art;
            IsLocked = isLocked;
            StatusIcons = statusIcons ?? Array.Empty<CardStatusIcon>();
            Category = category;
            OwnerDisplayName = ownerDisplayName;
            OwnerColor = ownerColor;
            IsPartyOwned = isPartyOwned;
        }

        /// <summary>Zone card (placed instance) — shows its current executionOrder. <paramref name="art"/> resolves
        /// the sprite by id (e.g. from the authored CardAsset.Art); null falls back to Resources lookup.</summary>
        public static CardPresentation From(
            ExecutionCardInstance card,
            Func<string, Sprite> art = null,
            string ownerDisplayName = null,
            Color ownerColor = default,
            bool isPartyOwned = false)
        {
            var def = card.Def;
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                card.ExecutionOrder,
                def.EnergyCost,
                def.Side,
                DescriptionComposer.Describe(def, KoreanDescriptionCatalog.Default),
                ResolveArt(def.Id, art),
                card.IsLocked,
                StatusIconsFor(card),
                def.Category,
                ownerDisplayName,
                ownerColor,
                isPartyOwned);
        }

        /// <summary>Hand card (definition) — executionOrder is the base value; cost is the key number.</summary>
        public static CardPresentation FromDefinition(
            CardDefinition def,
            Func<string, Sprite> art = null,
            string ownerDisplayName = null,
            Color ownerColor = default,
            bool isPartyOwned = false)
        {
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                def.BaseExecutionOrder,
                def.EnergyCost,
                def.Side,
                DescriptionComposer.Describe(def, KoreanDescriptionCatalog.Default),
                ResolveArt(def.Id, art),
                false,
                Array.Empty<CardStatusIcon>(),
                def.Category,
                ownerDisplayName,
                ownerColor,
                isPartyOwned);
        }

        // A resolver (GUID-backed CardAsset.Art) wins; with none supplied we fall back to the Resources path.
        private static Sprite ResolveArt(string id, Func<string, Sprite> art)
            => art != null ? art(id) : PlaytestCardArt.Sprite(id);

        private static IReadOnlyList<CardStatusIcon> StatusIconsFor(ExecutionCardInstance card)
            => card.IsLocked ? new[] { CardStatusIcon.Lock } : Array.Empty<CardStatusIcon>();
    }
}
