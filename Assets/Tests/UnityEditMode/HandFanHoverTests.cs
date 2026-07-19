using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class HandFanHoverTests
    {
        private static readonly Color BlueOutline =
            new Color(0.35f, 0.75f, 0.95f, 1f);

        [Test]
        public void Hand_card_reports_its_index_on_hover_enter_and_exit()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<CardView>(
                    "Assets/Unity/Prefabs/CardView.prefab");
                Assert.IsNotNull(prefab);
                var hand = root.AddComponent<HandFanView>();
                hand.EditorBuild(prefab);
                var calls = new List<(int Index, bool Hovering)>();
                var cards = new[]
                {
                    new CardPresentation(
                        "execution", "execution", 3, 1, Side.Player,
                        string.Empty, null, false)
                };
                hand.SetCards(cards, _ => { },
                    (index, hovering) => calls.Add((index, hovering)));
                var hover = root.GetComponentInChildren<HandCardHoverEffect>();

                hover.OnPointerEnter(null);
                hover.OnPointerExit(null);

                CollectionAssert.AreEqual(
                    new[] { (0, true), (0, false) }, calls.ToArray());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Held_card_keeps_the_exact_hover_pose_after_pointer_exit()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            try
            {
                var hand = BuildHand(root, ThreeCards());
                var view = root.GetComponentsInChildren<CardView>()[0];
                var hover = view.GetComponent<HandCardHoverEffect>();
                var rect = (RectTransform)view.transform;
                hover.OnPointerEnter(null);
                Vector2 hoverPosition = rect.anchoredPosition;
                Quaternion hoverRotation = rect.localRotation;
                Vector3 hoverScale = rect.localScale;
                hover.OnPointerExit(null);

                hand.SetHeld(0, true);
                hover.OnPointerExit(null);

                Assert.AreEqual(hoverPosition, rect.anchoredPosition);
                Assert.AreEqual(hoverRotation, rect.localRotation);
                Assert.AreEqual(Quaternion.identity, rect.localRotation);
                Assert.AreEqual(hoverScale, rect.localScale);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Target_selected_hand_card_uses_blue_outline()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            try
            {
                var hand = BuildHand(root, ThreeCards());
                var selected = root.GetComponentsInChildren<CardView>()[0];

                hand.SetTargetSelection(0, true);

                Assert.AreEqual(
                    BlueOutline,
                    Field<Image>(selected, "_selectionOutline").color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Prepared_flight_reuses_card_prefab_and_stays_hidden_until_shown()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            try
            {
                overlay.transform.SetParent(root.transform, false);
                var cards = ThreeCards();
                var hand = BuildHand(root, cards);
                var source = root.GetComponentsInChildren<CardView>()[0];

                Assert.IsTrue(hand.TryPreparePlacementFlight(
                    0,
                    cards[0],
                    (RectTransform)overlay.transform,
                    out var visual));
                Assert.IsFalse(visual.Card.gameObject.activeSelf);
                Assert.IsFalse(visual.Card.GetComponent<Button>().interactable);
                Assert.IsTrue(visual.Card.GetComponentsInChildren<Graphic>(true)
                    .All(graphic => !graphic.raycastTarget));

                hand.ShowPlacementFlight(visual);

                Assert.IsTrue(visual.Card.gameObject.activeSelf);
                Assert.AreEqual(0f, source.GetComponent<CanvasGroup>().alpha);

                hand.ClearPlacementFlight(visual);

                Assert.AreEqual(1f, source.GetComponent<CanvasGroup>().alpha);
                Assert.IsTrue(visual.Card == null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static HandFanView BuildHand(
            GameObject root,
            IReadOnlyList<CardPresentation> cards)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<CardView>(
                "Assets/Unity/Prefabs/CardView.prefab");
            Assert.IsNotNull(prefab);
            var hand = root.AddComponent<HandFanView>();
            hand.EditorBuild(prefab);
            hand.SetCards(cards, _ => { }, (_, __) => { });
            return hand;
        }

        private static CardPresentation[] ThreeCards()
            => Enumerable.Range(0, 3)
                .Select(index => new CardPresentation(
                    "execution-" + index,
                    "execution",
                    3,
                    1,
                    Side.Player,
                    string.Empty,
                    null,
                    false))
                .ToArray();

        private static T Field<T>(object target, string name)
            => (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
    }
}
