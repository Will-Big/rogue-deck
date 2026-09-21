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
            _hand.SetCards(Enumerable.Range(0, 5).Select(i => new CardPresentation(
                "card-" + i, "card-" + i, 3, 1, Side.Player,
                new CardDescriptionLayout(Array.Empty<CardTargetKey>(),
                    Array.Empty<CardDescriptionLine>(), string.Empty), null, false)).ToArray(), index => _lastClicked = index, null);
            _cards = _content.GetComponentsInChildren<CardView>();
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
        public void Bottom_baseline_includes_rotated_card_bounds_and_padding()
        {
            Set("_baselinePadding", 25f);
            _layout.Refresh();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_root.transform, _content);
            Assert.That(bounds.min.y, Is.EqualTo(-125f).Within(.1f));
            Set("_baselinePadding", 45f);
            _layout.Refresh();
            bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_root.transform, _content);
            Assert.That(bounds.min.y, Is.EqualTo(-105f).Within(.1f));
        }

        [Test]
        public void Baseline_padding_translates_the_hand_without_rescaling_cards()
        {
            Set("_baselinePadding", 16f);
            _layout.Refresh();
            Vector3 scale = Rect(0).lossyScale;
            Vector3 position = Rect(0).position;
            Set("_baselinePadding", 116f);
            _layout.Refresh();
            Assert.That(Vector3.Distance(scale, Rect(0).lossyScale), Is.LessThan(.0001f));
            Assert.That(Rect(0).position.y - position.y, Is.EqualTo(100f).Within(.01f));
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
            var rect = Rect(1);
            var before = rect.anchoredPosition;
            var hover = _cards[1].GetComponent<HandCardHoverEffect>();
            hover.OnPointerEnter(null);
            _hand.SetHeld(1, true);
            hover.OnPointerExit(null);
            Set("_cardScale", .5f);
            _layout.Refresh();
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(before.x).Within(.001f));
            Assert.That(rect.localScale.x, Is.EqualTo(.675f).Within(.001f));
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

        private RectTransform Rect(int index) => (RectTransform)_cards[index].transform;
        private void Set(string field, object value)
            => typeof(HandFanLayoutView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_layout, value);
    }
}
