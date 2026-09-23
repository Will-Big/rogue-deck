using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardFrameResponsiveLayoutTests
    {
        private const float HorizontalSafeMarginPerSide = 16f;
        private const float VerticalSafeMarginPerSide = 8f;

        [TestCase(960f, 720f)]
        [TestCase(1280f, 800f)]
        [TestCase(1280f, 720f)]
        [TestCase(1680f, 720f)]
        public void Hand_rests_on_art_edge_and_reveals_each_hovered_card_inside_safe_area(
            float width,
            float height)
        {
            for (int cardCount = 1; cardCount <= 5; cardCount++)
            {
                var fixture = BuildSceneEquivalentHand(width, height, cardCount);
                try
                {
                    Assert.AreEqual(420f, fixture.HandRect.rect.height, 0.01f);
                    AssertUniformContentScale(fixture.Content);
                    AssertRestingCardsStayInsideHorizontalSafeArea(fixture);
                    AssertApexArtEdgeRestsOnHandBottom(fixture);
                    AssertRestingCardsLeaveHandBottom(fixture);
                    AssertCardsStayInLeftToRightOrder(fixture.Views);
                    AssertAdjacentCardsLeaveBadgesVisible(fixture.Views);
                    AssertHoveredCardsStayInsideHandSafeArea(fixture);
                }
                finally
                {
                    Object.DestroyImmediate(fixture.Root);
                }
            }
        }

        [Test]
        public void Too_small_root_scales_only_the_common_content_root()
        {
            var fixture = BuildDirectHand(420f, 190f, 5);
            try
            {
                Assert.That(fixture.Content.localScale.x, Is.LessThan(1f));
                AssertUniformContentScale(fixture.Content);
                foreach (var view in fixture.Views)
                {
                    Assert.That(view.transform.localScale.x, Is.EqualTo(1f).Within(.001f));
                    Assert.AreEqual(view.transform.localScale.x, view.transform.localScale.y);
                    Assert.AreEqual(
                        new Vector2(200f, 336f),
                        ((RectTransform)view.transform).sizeDelta);
                }
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Root_dimension_change_recomputes_geometry_immediately_without_frame_loop()
        {
            var fixture = BuildDirectHand(650f, 260f, 5);
            try
            {
                float narrowScale = fixture.Content.localScale.x;
                fixture.HandRect.sizeDelta = new Vector2(900f, 260f);
                fixture.Hand.GetComponent<HandFanLayoutView>().Refresh();
                Assert.That(fixture.Content.localScale.x, Is.GreaterThanOrEqualTo(narrowScale - .0001f));
                AssertRestingCardsStayInsideHorizontalSafeArea(fixture);
                AssertHoveredCardsStayInsideHandSafeArea(fixture);
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        private static HandFixture BuildSceneEquivalentHand(
            float width,
            float height,
            int cardCount)
        {
            var root = new GameObject("LogicalRoot", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(width, height);
            var handObject = new GameObject("HandFan", typeof(RectTransform));
            var handRect = (RectTransform)handObject.transform;
            handRect.SetParent(rootRect, false);
            handRect.anchorMin = new Vector2(0f, 0f);
            handRect.anchorMax = new Vector2(1f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 210f);
            handRect.sizeDelta = new Vector2(0f, 420f);
            return BuildHand(root, handRect, cardCount);
        }

        private static HandFixture BuildDirectHand(
            float width,
            float height,
            int cardCount)
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            var handRect = (RectTransform)root.transform;
            handRect.sizeDelta = new Vector2(width, height);
            return BuildHand(root, handRect, cardCount);
        }

        private static HandFixture BuildHand(
            GameObject root,
            RectTransform handRect,
            int cardCount)
        {
            var contentObject = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(handRect, false);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = Vector2.zero;

            var hand = handRect.gameObject.AddComponent<HandFanView>();
            hand.EditorBuild(CardPrefabCatalogTests.LoadCatalog(), content);
            // Verify fitting before intentional position offsets move the hand outside the safe area.
            typeof(HandFanLayoutView).GetField("_baselinePadding",
                BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hand.GetComponent<HandFanLayoutView>(), VerticalSafeMarginPerSide);
            hand.SetCards(
                Presentations(cardCount),
                _ => { },
                (_, __) => { });
            Canvas.ForceUpdateCanvases();
            return new HandFixture(
                root,
                hand,
                handRect,
                content,
                content.GetComponentsInChildren<CardView>());
        }

        private static IReadOnlyList<CardPresentation> Presentations(int count)
            => Enumerable.Range(0, count)
                .Select(index => Presentation(
                    index % 2 == 0
                        ? CardCategory.Execution
                        : CardCategory.Intervention,
                    index))
                .ToArray();

        private static CardPresentation Presentation(
            CardCategory category,
            int index)
            => new CardPresentation(
                category + "-" + index,
                category + " " + index,
                3,
                index + 1,
                Side.Player,
                new CardDescriptionLayout(
                    Array.Empty<CardTargetKey>(),
                    Array.Empty<CardDescriptionLine>(),
                    string.Empty),
                null,
                false,
                category: category);

        private static void AssertUniformContentScale(RectTransform content)
        {
            Assert.AreEqual(content.localScale.x, content.localScale.y, 0.0001f);
            Assert.AreEqual(content.localScale.x, content.localScale.z, 0.0001f);
            Assert.That(
                content.localScale.x,
                Is.InRange(.0001f, 1f));
        }

        private static void AssertRestingCardsStayInsideHorizontalSafeArea(HandFixture fixture)
        {
            var area = fixture.HandRect.rect;
            foreach (var view in fixture.Views)
            {
                var bounds = BoundsInHand(fixture, view.transform);
                Assert.That(bounds.min.x,
                    Is.GreaterThanOrEqualTo(area.xMin + HorizontalSafeMarginPerSide - 0.5f));
                Assert.That(bounds.max.x,
                    Is.LessThanOrEqualTo(area.xMax - HorizontalSafeMarginPerSide + 0.5f));
            }
        }

        private static void AssertApexArtEdgeRestsOnHandBottom(HandFixture fixture)
        {
            float line = fixture.HandRect.rect.yMin + VerticalSafeMarginPerSide;
            var edges = fixture.Views.Select(view => BoundsInHand(fixture, RestLine(view)).min.y).ToArray();
            // Cards away from the top of the arc sit lower, so no art edge rises above the line.
            Assert.That(edges.Max(), Is.LessThanOrEqualTo(line + 0.5f));
            if (edges.Length % 2 == 1)
            {
                Assert.That(edges[edges.Length / 2], Is.EqualTo(line).Within(0.5f));
            }
        }

        private static void AssertRestingCardsLeaveHandBottom(HandFixture fixture)
        {
            foreach (var view in fixture.Views)
            {
                Assert.Less(BoundsInHand(fixture, view.transform).min.y, fixture.HandRect.rect.yMin);
            }
        }

        private static void AssertHoveredCardsStayInsideHandSafeArea(HandFixture fixture)
        {
            var area = fixture.HandRect.rect;
            foreach (var view in fixture.Views)
            {
                var hover = view.GetComponent<HandCardHoverEffect>();
                hover.OnPointerEnter(null);
                try
                {
                    Assert.Less(
                        Quaternion.Angle(Quaternion.identity, view.transform.localRotation), 0.01f);
                    var bounds = BoundsInHand(fixture, view.transform);
                    Assert.That(bounds.min.x,
                        Is.GreaterThanOrEqualTo(area.xMin + HorizontalSafeMarginPerSide - 0.5f));
                    Assert.That(bounds.max.x,
                        Is.LessThanOrEqualTo(area.xMax - HorizontalSafeMarginPerSide + 0.5f));
                    Assert.That(bounds.min.y,
                        Is.GreaterThanOrEqualTo(area.yMin + VerticalSafeMarginPerSide - 0.5f));
                    Assert.That(bounds.max.y,
                        Is.LessThanOrEqualTo(area.yMax - VerticalSafeMarginPerSide + 0.5f));
                }
                finally
                {
                    hover.OnPointerExit(null);
                }
            }
        }

        private static Bounds BoundsInHand(HandFixture fixture, Transform target)
            => RectTransformUtility.CalculateRelativeRectTransformBounds(fixture.HandRect, target);

        private static RectTransform RestLine(CardView view)
            => Field<RectTransform>(view.GetComponent<HandCardHoverEffect>(), "_restLine");

        private static void AssertCardsStayInLeftToRightOrder(CardView[] views)
        {
            for (int i = 1; i < views.Length; i++)
                Assert.Greater(((RectTransform)views[i].transform).anchoredPosition.x,
                    ((RectTransform)views[i - 1].transform).anchoredPosition.x);
        }

        private static void AssertAdjacentCardsLeaveBadgesVisible(CardView[] views)
        {
            for (int index = 0; index + 1 < views.Length; index++)
            {
                var nextFrame = (RectTransform)views[index + 1].transform;
                var costText = Field<TMP_Text>(views[index], "_costText");
                AssertNotFullyCovered(
                    (RectTransform)costText.transform.parent,
                    nextFrame);
                var orderBadge = Field<RectTransform>(
                    views[index],
                    "_executionOrderBadge");
                if (orderBadge != null)
                {
                    AssertNotFullyCovered(orderBadge, nextFrame);
                }
            }
        }

        private static void AssertNotFullyCovered(
            RectTransform badge,
            RectTransform adjacentFrame)
        {
            var corners = new Vector3[4];
            badge.GetWorldCorners(corners);
            Assert.IsTrue(
                corners
                    .Select(adjacentFrame.InverseTransformPoint)
                    .Any(corner => !adjacentFrame.rect.Contains(corner)),
                badge.name + " is fully covered by the adjacent card frame.");
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private sealed class HandFixture
        {
            public HandFixture(
                GameObject root,
                HandFanView hand,
                RectTransform handRect,
                RectTransform content,
                CardView[] views)
            {
                Root = root;
                Hand = hand;
                HandRect = handRect;
                Content = content;
                Views = views;
            }

            public GameObject Root { get; }
            public HandFanView Hand { get; }
            public RectTransform HandRect { get; }
            public RectTransform Content { get; }
            public CardView[] Views { get; }
        }
    }
}
