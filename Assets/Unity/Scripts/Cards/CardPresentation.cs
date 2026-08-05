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
        public CardDescriptionLayout DescriptionLayout { get; }
        public string Description => DescriptionLayout.PlainText;
        public Sprite Art { get; }
        public bool IsLocked { get; }
        public CardCategory Category { get; }
        public IReadOnlyList<CardStatusIcon> StatusIcons { get; }
        public string OwnerDisplayName { get; }
        public Color OwnerColor { get; }
        public bool IsPartyOwned { get; }

        public CardPresentation(
            string id, string displayName, int executionOrder, int energyCost, Side side,
            CardDescriptionLayout descriptionLayout, Sprite art, bool isLocked,
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
            DescriptionLayout = descriptionLayout ?? throw new ArgumentNullException(nameof(descriptionLayout));
            Art = art;
            IsLocked = isLocked;
            StatusIcons = statusIcons ?? Array.Empty<CardStatusIcon>();
            Category = category;
            OwnerDisplayName = ownerDisplayName;
            OwnerColor = ownerColor;
            IsPartyOwned = isPartyOwned;
        }

        public CardPresentation WithExecutionOrder(int executionOrder)
            => new CardPresentation(
                Id,
                DisplayName,
                executionOrder,
                EnergyCost,
                Side,
                DescriptionLayout,
                Art,
                IsLocked,
                StatusIcons,
                Category,
                OwnerDisplayName,
                OwnerColor,
                IsPartyOwned);

        /// <summary>Zone card (placed instance) — shows its current executionOrder. <paramref name="art"/> resolves
        /// the sprite by id (CardArtCatalog가 그 역할을 한다).</summary>
        public static CardPresentation From(
            ExecutionCardInstance card,
            KoreanDescriptionCatalog korean,
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
                DescriptionComposer.Compose(def, korean),
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
            KoreanDescriptionCatalog korean,
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
                DescriptionComposer.Compose(def, korean),
                ResolveArt(def.Id, art),
                false,
                Array.Empty<CardStatusIcon>(),
                def.Category,
                ownerDisplayName,
                ownerColor,
                isPartyOwned);
        }

        // 카드 앞면 아트는 주입된 resolver(CardArtCatalog)에서만 온다. id→경로 폴백은 없다.
        private static Sprite ResolveArt(string id, Func<string, Sprite> art)
            => art != null ? art(id) : null;

        private static IReadOnlyList<CardStatusIcon> StatusIconsFor(ExecutionCardInstance card)
            => card.IsLocked ? new[] { CardStatusIcon.Lock } : Array.Empty<CardStatusIcon>();
    }
}
