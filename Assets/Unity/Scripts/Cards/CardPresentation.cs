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

        /// <summary>배치된 카드 인스턴스의 식별자. 손패·더미 카드처럼 인스턴스가 없으면 -1이다.
        /// 재생 계층이 CardResolved.InstanceId로 레일의 어느 카드인지 찾는 데 쓴다.</summary>
        public int InstanceId { get; }
        public GameLocalization TextSource { get; }
        public LocalizedCardDescription DescriptionRequest { get; }
        public string OwnerId { get; }

        public CardPresentation(
            string id, string displayName, int executionOrder, int energyCost, Side side,
            CardDescriptionLayout descriptionLayout, Sprite art, bool isLocked,
            IReadOnlyList<CardStatusIcon> statusIcons = null,
            CardCategory category = CardCategory.Execution,
            string ownerDisplayName = null,
            Color ownerColor = default,
            bool isPartyOwned = false,
            int instanceId = -1,
            GameLocalization textSource = null,
            LocalizedCardDescription descriptionRequest = null,
            string ownerId = null)
        {
            TextSource = textSource;
            DescriptionRequest = descriptionRequest;
            OwnerId = ownerId;
            InstanceId = instanceId;
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
                IsPartyOwned,
                InstanceId, TextSource, DescriptionRequest, OwnerId);

        public CardPresentation Relocalized()
        {
            if (TextSource == null || DescriptionRequest == null) return this;
            var owner = Side == Side.Enemy ? null : IsPartyOwned ? TextSource.Name("party.name")
                : OwnerId == null ? OwnerDisplayName : TextSource.Name("character." + OwnerId + ".name");
            return new CardPresentation(Id, TextSource.Name("card." + Id + ".name"), ExecutionOrder,
                EnergyCost, Side, TextSource.Renderer.Render(DescriptionRequest), Art, IsLocked,
                StatusIcons, Category, owner, OwnerColor, IsPartyOwned, InstanceId,
                TextSource, DescriptionRequest, OwnerId);
        }
        public static CardPresentation FromLocalized(CardDefinition definition, DescriptionRequestCatalog catalog,
            GameLocalization text, Sprite art, int order, bool locked, string ownerId,
            Color ownerColor, bool partyOwned, int instanceId = -1)
        {
            var request = DescriptionComposer.Compose(definition, catalog);
            return new CardPresentation(definition.Id, text.Name("card." + definition.Id + ".name"),
                order, definition.EnergyCost, definition.Side, text.Renderer.Render(request), art, locked,
                locked ? new[] { CardStatusIcon.Lock } : Array.Empty<CardStatusIcon>(), definition.Category,
                definition.Side == Side.Enemy ? null : partyOwned ? text.Name("party.name") : ownerId == null ? null : text.Name("character." + ownerId + ".name"),
                ownerColor, partyOwned, instanceId, text, request, ownerId);
        }
    }
}
