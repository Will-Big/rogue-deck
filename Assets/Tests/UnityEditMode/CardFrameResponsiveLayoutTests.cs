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
        private const float MinimumSpacing = 72f;
        private const float BaseSpacing = 150f;
        private const float MinimumScale = 0.65f;
        private const float HorizontalSafeMarginPerSide = 16f;
        private const float VerticalSafeMarginPerSide = 8f;

        [TestCase(960f, 720f)]
        [TestCase(1280f, 800f)]
        [TestCase(1280f, 720f)]
        [TestCase(1680f, 720f)]
        public void Logical_root_keeps_one_to_five_mixed_cards_inside_safe_area(
            float width,
            float height)
        {
            for (int cardCount = 1; cardCount <= 5; cardCount++)
            {
                var fixture = BuildSceneEquivalentHand(width, height, cardCount);
                try
                {
                    Assert.AreEqual(260f, fixture.HandRect.rect.height, 0.01f);
                    Assert.AreEqual(Vector2.zero, fixture.Content.anchoredPosition);
                    AssertUniformContentScale(fixture.Content);
                    AssertCardsStayInsideSafeArea(fixture);
                    AssertSpacingStaysInAuthoredRange(fixture.Views);
                    AssertAdjacentCardsLeaveBadgesVisible(fixture.Views);
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
                    Assert.AreEqual(Vector3.one, view.transform.localScale);
                    Assert.AreEqual(
                        new Vector2(170f, 238f),
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
                float narrowSpacing = Spacing(fixture.Views);
                float narrowScale = fixture.Content.localScale.x;

                fixture.HandRect.sizeDelta = new Vector2(900f, 260f);
                var dimensionCallback = typeof(HandFanView).GetMethod(
                    "OnRectTransformDimensionsChange",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(dimensionCallback);
                dimensionCallback.Invoke(fixture.Hand, null);

                Assert.That(narrowSpacing, Is.LessThan(BaseSpacing));
                Assert.AreEqual(BaseSpacing, Spacing(fixture.Views), 0.01f);
                Assert.That(
                    fixture.Content.localScale.x,
                    Is.GreaterThanOrEqualTo(narrowScale));
                Assert.IsNull(
                    typeof(HandFanView).GetMethod(
                        "LateUpdate",
                        BindingFlags.Instance | BindingFlags.NonPublic));
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
            handRect.sizeDelta = new Vector2(0f, 260f);
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
                Is.InRange(MinimumScale, 1f));
        }

        private static void AssertCardsStayInsideSafeArea(HandFixture fixture)
        {
            var rootRect = (RectTransform)fixture.Root.transform;
            foreach (var view in fixture.Views)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    rootRect,
                    view.transform);
                Assert.That(
                    bounds.min.x,
                    Is.GreaterThanOrEqualTo(
                        rootRect.rect.xMin + HorizontalSafeMarginPerSide - 0.5f));
                Assert.That(
                    bounds.max.x,
                    Is.LessThanOrEqualTo(
                        rootRect.rect.xMax - HorizontalSafeMarginPerSide + 0.5f));
                Assert.That(
                    bounds.min.y,
                    Is.GreaterThanOrEqualTo(
                        rootRect.rect.yMin + VerticalSafeMarginPerSide - 0.5f));
                Assert.That(
                    bounds.max.y,
                    Is.LessThanOrEqualTo(
                        rootRect.rect.yMax - VerticalSafeMarginPerSide + 0.5f));
            }
        }

        private static void AssertSpacingStaysInAuthoredRange(CardView[] views)
        {
            if (views.Length < 2)
            {
                return;
            }

            Assert.That(
                Spacing(views),
                Is.InRange(MinimumSpacing, BaseSpacing));
        }

        private static float Spacing(CardView[] views)
            => Mathf.Abs(
                ((RectTransform)views[1].transform).anchoredPosition.x
                - ((RectTransform)views[0].transform).anchoredPosition.x);

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
