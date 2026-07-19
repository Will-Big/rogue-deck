using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class ExecutionRailInputTests
    {
        private static readonly Color BlueOutline =
            new Color(0.35f, 0.75f, 0.95f, 1f);

        [TearDown]
        public void ClearDotweenState()
        {
            DOTween.Clear(true);
        }

        [Test]
        public void Disabling_input_disables_scroll_rect()
        {
            var root = new GameObject("Rail", typeof(RectTransform));
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            try
            {
                var rail = root.AddComponent<ExecutionRailView>();
                rail.EditorBuild(null, null, (RectTransform)overlay.transform);
                var scroll = root.GetComponent<ScrollRect>();

                rail.SetInputEnabled(false);

                Assert.IsFalse(scroll.enabled);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(overlay);
            }
        }

        [Test]
        public void Disabling_input_immediately_hides_open_preview()
        {
            var root = new GameObject("Rail", typeof(RectTransform));
            var previewObject = new GameObject("Preview", typeof(RectTransform));
            try
            {
                var rail = root.AddComponent<ExecutionRailView>();
                var preview = previewObject.AddComponent<CardView>();
                typeof(ExecutionRailView)
                    .GetField("_preview", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(rail, preview);
                previewObject.SetActive(true);

                rail.SetInputEnabled(false);

                Assert.IsFalse(previewObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(previewObject);
            }
        }

        [Test]
        public void Disabled_rail_card_does_not_raise_hover_callback()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var view = RailCardView.EditorCreate((RectTransform)root.transform, new Vector2(96f, 132f));
                var card = new CardPresentation(
                    "test", "test", 1, 0, Side.Enemy, string.Empty, null, false);
                int hoverCalls = 0;
                view.Bind(card, null, _ => hoverCalls++);
                view.SetInteractable(false);

                view.OnPointerEnter(null);

                Assert.AreEqual(0, hoverCalls);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rail_card_button_completes_common_single_target_selection_without_confirm()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var hand = Child<HandFanView>(root.transform, "Hand");
                var rail = Child<ExecutionRailView>(root.transform, "Rail");
                var overlay = ChildRect(root.transform, "Overlay");
                rail.EditorBuild(null, RailCardView.EditorCreate(
                    ChildRect(root.transform, "RailCardPrefabRoot"), new Vector2(96f, 132f)), overlay);
                var confirmButton = Child<Button>(root.transform, "Confirm");
                var dim = ChildRect(root.transform, "Dim").gameObject;
                var arrow = TargetingArrowView.EditorCreate(overlay);
                var selection = root.AddComponent<CardSelectionController>();
                SetField(selection, "_hand", hand);
                SetField(selection, "_rail", rail);
                SetField(selection, "_dimLayer", dim);
                SetField(selection, "_confirmButton", confirmButton);
                SetField(selection, "_arrow", arrow);

                var completed = new List<SelectionResult>();
                selection.Initialize(
                    result =>
                    {
                        completed.Add(result);
                        return true;
                    },
                    _ => Array.Empty<SelectionTargetRef>(),
                    () => { });
                var card = new CardPresentation(
                    "test", "test", 1, 0, Side.Player, string.Empty, null, false);
                rail.SetCards(
                    new[] { card },
                    index => selection.OnTargetClicked(
                        SelectionTargetRef.ExecutionCard(index)));
                var target = SelectionTargetRef.ExecutionCard(0);
                root.SetActive(true);

                selection.BeginTargetSelection(
                    0, SelectionTargetKind.ExecutionCard, 1, new[] { target });
                rail.GetComponentsInChildren<RailCardView>(true)
                    .Single(view => view.transform.parent.name == "Content")
                    .GetComponent<Button>().onClick.Invoke();

                Assert.AreEqual(1, completed.Count);
                Assert.IsTrue(completed[0].IsComplete);
                Assert.AreEqual(0, completed[0].Targets.Single().Index);
                Assert.IsFalse(confirmButton.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Hover_preview_is_immediate_static_translucent_and_noninteractive()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var overlay = ChildRect(root.transform, "Overlay");
            try
            {
                var prefab = RailCardView.EditorCreate(
                    ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
                var rail = Child<ExecutionRailView>(root.transform, "Rail");
                rail.EditorBuild(null, prefab, overlay);
                var existing = Card("existing", order: 4, side: Side.Enemy);
                var candidate = Card("candidate", order: 3, side: Side.Player);
                rail.SetCards(new[] { existing, existing }, _ => { });

                rail.ShowPlacementHover(candidate, 1);

                var preview = Field<RailCardView>(rail, "_placementPreview");
                Assert.IsTrue(preview.gameObject.activeSelf);
                Assert.AreEqual(1, preview.transform.GetSiblingIndex());
                Assert.AreEqual(Vector3.one, preview.transform.localScale);
                Assert.AreEqual(0.5f, preview.GetComponent<CanvasGroup>().alpha);
                Assert.IsFalse(Field<Button>(preview, "_button").interactable);
                Assert.IsNull(Field<Tween>(rail, "_placementPreviewTween"));
                Assert.AreEqual(BlueOutline, Field<Image>(preview, "_selectionOutline").color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rebuilding_cards_clears_active_placement_preview()
        {
            SimulateDotweenRuntimeInitializationForEditMode();
            var root = new GameObject("Root", typeof(RectTransform));
            var overlay = ChildRect(root.transform, "Overlay");
            try
            {
                var prefab = RailCardView.EditorCreate(
                    ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
                var rail = Child<ExecutionRailView>(root.transform, "Rail");
                rail.EditorBuild(null, prefab, overlay);
                var candidate = Card("candidate", 3, Side.Player);
                rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
                rail.ShowPlacementHover(candidate, 0);
                rail.ArmPlacementPreview(() => { });
                var preview = Field<RailCardView>(rail, "_placementPreview");
                var tween = Field<Tween>(rail, "_placementPreviewTween");
                Assert.IsTrue(preview.gameObject.activeSelf);

                rail.SetCards(Array.Empty<CardPresentation>(), _ => { });

                Assert.IsFalse(preview.gameObject.activeSelf);
                Assert.IsFalse(tween.IsActive());
                Assert.AreEqual(Vector3.one, preview.transform.localScale);
                Assert.IsNull(Field<Tween>(rail, "_placementPreviewTween"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Armed_preview_is_clickable_and_owns_a_yoyo_tween()
        {
            SimulateDotweenRuntimeInitializationForEditMode();
            var root = new GameObject("Root", typeof(RectTransform));
            var overlay = ChildRect(root.transform, "Overlay");
            try
            {
                var prefab = RailCardView.EditorCreate(
                    ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
                var rail = Child<ExecutionRailView>(root.transform, "Rail");
                rail.EditorBuild(null, prefab, overlay);
                rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
                rail.ShowPlacementHover(Card("candidate", 3, Side.Player), 0);
                int clicks = 0;

                rail.ArmPlacementPreview(() => clicks++);

                var preview = Field<RailCardView>(rail, "_placementPreview");
                var tween = Field<Tween>(rail, "_placementPreviewTween");
                Assert.IsTrue(Field<Button>(preview, "_button").interactable);
                Assert.IsTrue(tween.IsActive());
                Field<Button>(preview, "_button").onClick.Invoke();
                Assert.AreEqual(1, clicks);

                rail.ClearPlacementPreview();

                Assert.IsFalse(preview.gameObject.activeSelf);
                Assert.IsFalse(tween.IsActive());
                Assert.AreEqual(Vector3.one, preview.transform.localScale);
                Assert.IsNull(Field<Tween>(rail, "_placementPreviewTween"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Placement_flight_hides_silhouette_and_settles_at_its_pose()
        {
            SimulateDotweenRuntimeInitializationForEditMode();
            var root = new GameObject("Root", typeof(RectTransform));
            var overlay = ChildRect(root.transform, "Overlay");
            try
            {
                var prefab = RailCardView.EditorCreate(
                    ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
                var rail = Child<ExecutionRailView>(root.transform, "Rail");
                rail.EditorBuild(null, prefab, overlay);
                rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
                rail.ShowPlacementHover(Card("candidate", 3, Side.Player), 0);
                rail.ArmPlacementPreview(() => { });
                var flight = ChildRect(overlay, "Flight");
                flight.sizeDelta = new Vector2(170f, 238f);
                bool completed = false;

                Assert.IsTrue(rail.StartPlacementFlight(
                    flight, () => completed = true));

                var preview = Field<RailCardView>(rail, "_placementPreview");
                var sequence = Field<Sequence>(rail, "_placementFlightSequence");
                Assert.AreEqual(0f, preview.GetComponent<CanvasGroup>().alpha);
                Assert.IsFalse(Field<Button>(preview, "_button").interactable);
                Assert.IsTrue(sequence.IsActive());

                sequence.Complete();

                Assert.IsTrue(completed);
                Assert.That(Vector3.Distance(
                    flight.position, preview.transform.position), Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(
                    flight.eulerAngles.z, preview.transform.eulerAngles.z)),
                    Is.LessThan(0.01f));
                Assert.IsNull(Field<Sequence>(rail, "_placementFlightSequence"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Existing_rail_card_hover_still_opens_detail_while_preview_is_armed()
        {
            SimulateDotweenRuntimeInitializationForEditMode();
            var root = new GameObject("Root", typeof(RectTransform));
            var overlay = ChildRect(root.transform, "Overlay");
            try
            {
                var fullPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<CardView>(
                    "Assets/Unity/Prefabs/CardView.prefab");
                Assert.IsNotNull(fullPrefab);
                var miniPrefab = RailCardView.EditorCreate(
                    ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
                var rail = Child<ExecutionRailView>(root.transform, "Rail");
                rail.EditorBuild(fullPrefab, miniPrefab, overlay);
                rail.SetCards(new[] { Card("existing", 4, Side.Enemy) }, _ => { });
                rail.ShowPlacementHover(Card("candidate", 3, Side.Player), 0);
                rail.ArmPlacementPreview(() => { });
                var existing = Field<List<RailCardView>>(rail, "_views")[0];

                existing.OnPointerEnter(null);

                var detail = Field<CardView>(rail, "_preview");
                Assert.IsNotNull(detail);
                Assert.IsTrue(detail.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T Child<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            return child.GetComponent<T>();
        }

        private static RectTransform ChildRect(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static CardPresentation Card(string id, int order, Side side)
            => new CardPresentation(
                id, id, order, 1, side, string.Empty, null, false);

        private static void SimulateDotweenRuntimeInitializationForEditMode()
        {
            var initialized = typeof(DOTween).GetField(
                "initialized", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(initialized);
            initialized.SetValue(null, true);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
    }
}
