using System;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class ConfigurableHandLayoutTests
    {
        private GameObject _root;
        private HandFanView _hand;
        private HandFanLayoutView _layout;
        private RectTransform _content;
        private CardView[] _cards;
        private int _lastClicked;

        [SetUp]
        public void SetUp()
        {
            _lastClicked = -1;
            _root = new GameObject("Hand", typeof(RectTransform));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(900, 300);
            _content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            _content.SetParent(_root.transform, false);
            _content.sizeDelta = Vector2.zero;
            _hand = _root.AddComponent<HandFanView>();
            _hand.EditorBuild(CardPrefabCatalogTests.LoadCatalog(), _content);
            _layout = _root.GetComponent<HandFanLayoutView>();
            SetHand(5);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Inspector_radius_changes_spacing_without_changing_card_identity()
        {
            Set("_radius", 400f);
            _layout.Refresh();
            float before = Rect(4).anchoredPosition.x;
            Set("_radius", 800f);
            _layout.Refresh();
            Assert.That(Rect(4).anchoredPosition.x, Is.EqualTo(before * 2).Within(.01f));
            Assert.AreSame(_cards[0], _content.GetComponentsInChildren<CardView>()[0]);
        }

        [Test]
        public void Bottom_baseline_rests_the_apex_art_edge_on_the_padding()
        {
            Set("_baselinePadding", 25f);
            _layout.Refresh();
            Assert.That(ArtEdge(2), Is.EqualTo(-125f).Within(.1f));
            Set("_baselinePadding", 45f);
            _layout.Refresh();
            Assert.That(ArtEdge(2), Is.EqualTo(-105f).Within(.1f));
        }

        [TestCase(3)]
        [TestCase(7)]
        public void Apex_art_edge_stays_on_the_baseline_as_the_hand_grows(int count)
        {
            Set("_baselinePadding", 25f);
            _layout.Refresh();
            float five = ArtEdge(2);
            SetHand(count);
            _layout.Refresh();
            Assert.That(ArtEdge(count / 2), Is.EqualTo(five).Within(.1f));
        }

        [Test]
        public void Fan_keeps_widening_gently_until_the_max_spread_count()
        {
            float full = (float)typeof(HandFanLayoutView)
                .GetField("_totalAngle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_layout);
            float max = (float)typeof(HandFanLayoutView)
                .GetField("_maxAngle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_layout);
            Assert.Greater(max, full);
            _layout.Refresh();
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 0f, -full * .5f), Rect(4).localRotation), .01f);
            SetHand(10);
            _layout.Refresh();
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 0f, -max * .5f), Rect(9).localRotation), .01f);
        }

        [Test]
        public void Resting_cards_below_the_art_edge_leave_the_hand_area()
        {
            _layout.Refresh();
            float bottom = ((RectTransform)_root.transform).rect.yMin;
            for (int i = 0; i < _cards.Length; i++)
                Assert.Less(BoundsOf(Rect(i)).min.y, bottom, "card " + i + " should be clipped");
        }

        [Test]
        public void Hovered_card_rises_upright_and_fully_inside_the_hand_area()
        {
            _layout.Refresh();
            _cards[2].GetComponent<HandCardHoverEffect>().OnPointerEnter(null);
            Assert.Less(Quaternion.Angle(Quaternion.identity, Rect(2).localRotation), .01f);
            AssertInsideSafeArea(Rect(2));
        }

        [Test]
        public void Hovered_edge_card_is_pushed_inside_the_hand_width()
        {
            Set("_hoverScale", 1.6f);
            ((RectTransform)_root.transform).sizeDelta = new Vector2(420f, 300f);
            _layout.Refresh();
            _cards[0].GetComponent<HandCardHoverEffect>().OnPointerEnter(null);
            AssertInsideSafeArea(Rect(0));
        }

        [TestCase(116f, 100f)]
        [TestCase(-84f, -100f)]
        [TestCase(0f, -16f)]
        public void Baseline_padding_translates_the_hand_without_rescaling_cards(float padding, float deltaY)
        {
            Set("_baselinePadding", 16f);
            _layout.Refresh();
            Vector3 scale = Rect(0).lossyScale;
            Vector3 position = Rect(0).position;
            Set("_baselinePadding", padding);
            _layout.Refresh();
            Assert.That(Vector3.Distance(scale, Rect(0).lossyScale), Is.LessThan(.0001f));
            Assert.That(Rect(0).position.y - position.y, Is.EqualTo(deltaY).Within(.01f));
            Assert.That(Rect(0).position.x, Is.EqualTo(position.x).Within(.01f));
        }

        [TestCase(true, 120f, -80f)]
        [TestCase(false, -120f, 80f)]
        public void Position_offset_moves_both_axes_without_changing_world_scale(
            bool bottomBaseline, float x, float y)
        {
            Set("_useBottomBaseline", bottomBaseline);
            _layout.Refresh();
            Vector3 scale = Rect(0).lossyScale;
            Vector3 position = Rect(0).position;
            Set("_positionOffset", new Vector2(x, y));
            _layout.Refresh();
            Assert.That(Vector3.Distance(scale, Rect(0).lossyScale), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(position + new Vector3(x, y, 0f), Rect(0).position),
                Is.LessThan(.01f));
        }

        [Test]
        public void Baseline_mode_only_changes_alignment_not_scale()
        {
            _layout.Refresh();
            Vector3 scale = Rect(0).lossyScale;
            Set("_useBottomBaseline", false);
            _layout.Refresh();
            Assert.That(Vector3.Distance(scale, Rect(0).lossyScale), Is.LessThan(.0001f));
        }

        [Test]
        public void Scale_toggle_restores_prefab_scale_without_accumulating_multiplier()
        {
            Set("_cardScale", .5f);
            _layout.Refresh();
            _layout.Refresh();
            Assert.That(Rect(0).localScale.x, Is.EqualTo(.5f).Within(.001f));
            Set("_controlCardScale", false);
            _layout.Refresh();
            Assert.That(Rect(0).localScale.x, Is.EqualTo(1f).Within(.001f));
        }

        [Test]
        public void Hover_and_held_card_keep_logical_slot_across_relayout()
        {
            Set("_cardScale", .5f);
            _layout.Refresh();
            var rect = Rect(1);
            var before = rect.anchoredPosition;
            var hover = _cards[1].GetComponent<HandCardHoverEffect>();
            hover.OnPointerEnter(null);
            _hand.SetHeld(1, true);
            hover.OnPointerExit(null);
            _layout.Refresh();
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(before.x).Within(.001f));
            Assert.That(rect.localScale.x, Is.EqualTo(.5f * HoverScale()).Within(.001f));
            Assert.AreEqual(4, rect.GetSiblingIndex());
            _hand.SetHeld(1, false);
            Assert.That(rect.anchoredPosition, Is.EqualTo(before));
            Assert.That(rect.localScale.x, Is.EqualTo(.5f).Within(.001f));
            Assert.AreEqual(1, rect.GetSiblingIndex());
        }

        [Test]
        public void Sibling_order_changes_visual_slots_without_changing_click_indices()
        {
            var effects = _cards.Select(c => c.GetComponent<HandCardHoverEffect>()).ToArray();
            Rect(4).SetAsFirstSibling();
            _layout.Bind(effects);
            Assert.Less(Rect(4).anchoredPosition.x, Rect(0).anchoredPosition.x);
            _cards[4].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(4, _lastClicked);
            Set("_useSiblingOrder", false);
            _layout.Refresh();
            Assert.Less(Rect(0).anchoredPosition.x, Rect(4).anchoredPosition.x);
            _cards[0].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(0, _lastClicked);
        }

        [Test]
        public void Arc_scales_with_cards()
        {
            Set("_controlCardScale", true);
            Set("_cardScale", .5f);
            _layout.Refresh();
            float before = Rect(4).anchoredPosition.x;
            Set("_cardScale", 1f);
            _layout.Refresh();
            Assert.That(Rect(4).anchoredPosition.x, Is.EqualTo(before * 2f).Within(.01f));
        }

        [Test]
        public void Resting_hand_is_capped_to_the_hand_width()
        {
            Set("_controlCardScale", true);
            Set("_cardScale", 1.5f);
            _layout.Refresh();
            Assert.Less(Rect(0).lossyScale.x, 1.5f);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_root.transform, _content);
            Assert.That(bounds.size.x, Is.LessThanOrEqualTo(((RectTransform)_root.transform).rect.width + .01f));
        }

        [Test]
        public void Card_scale_reaches_the_screen_until_the_area_caps_it()
        {
            Set("_controlCardScale", true);
            Set("_cardScale", .3f);
            _layout.Refresh();
            Assert.That(Rect(0).lossyScale.x, Is.EqualTo(.3f).Within(.001f), "below the cap the knob is 1:1");
            Set("_cardScale", 1.5f);
            _layout.Refresh();
            float capped = Rect(0).lossyScale.x;
            Set("_cardScale", 3f);
            _layout.Refresh();
            Assert.That(Rect(0).lossyScale.x, Is.EqualTo(capped).Within(.001f), "above the cap the area pins the size");
            Assert.Less(capped, 1.5f);
        }

        // Default _safeMargins split per side.
        private const float HorizontalMargin = 16f;
        private const float VerticalMargin = 8f;

        private RectTransform Rect(int index) => (RectTransform)_cards[index].transform;

        private Bounds BoundsOf(RectTransform rect)
            => RectTransformUtility.CalculateRelativeRectTransformBounds(_root.transform, rect);

        private void AssertInsideSafeArea(RectTransform card)
        {
            var area = ((RectTransform)_root.transform).rect;
            var bounds = BoundsOf(card);
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(area.xMin + HorizontalMargin - .5f));
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(area.xMax - HorizontalMargin + .5f));
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(area.yMin + VerticalMargin - .5f));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(area.yMax - VerticalMargin + .5f));
        }

        private float ArtEdge(int index) => BoundsOf(RestLine(_cards[index])).min.y;

        private void SetHand(int count)
        {
            _hand.SetCards(Enumerable.Range(0, count).Select(i => new CardPresentation(
                "card-" + i, "card-" + i, 3, 1, Side.Player,
                new CardDescriptionLayout(Array.Empty<CardTargetKey>(),
                    Array.Empty<CardDescriptionLine>(), string.Empty), null, false)).ToArray(), index => _lastClicked = index, null);
            _cards = _content.GetComponentsInChildren<CardView>();
        }

        private static RectTransform RestLine(CardView card)
            => (RectTransform)typeof(HandCardHoverEffect)
                .GetField("_restLine", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(card.GetComponent<HandCardHoverEffect>());

        private float HoverScale()
            => (float)typeof(HandFanLayoutView)
                .GetField("_hoverScale", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_layout);
        private void Set(string field, object value)
            => typeof(HandFanLayoutView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_layout, value);
    }
}
