using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardSelectionControllerTests
    {
        private GameObject _root;
        private CardSelectionController _controller;
        private Button _confirmButton;
        private RectTransform _overlay;
        private TargetingArrowView _arrow;
        private HandFanView _hand;
        private ExecutionRailView _rail;
        private int _onAppliedCalls;
        private readonly List<SelectionResult> _appliedResults = new List<SelectionResult>();
        private static readonly Color SelectedOutline =
            new Color(0.35f, 0.75f, 0.95f, 1f);

        [SetUp]
        public void SetUp()
        {
            SimulateDotweenRuntimeInitializationForEditMode();
            _root = new GameObject("CardSelectionControllerTests", typeof(RectTransform));
            _root.SetActive(false);

            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(
                "Assets/Unity/Prefabs/CardView.prefab");
            Assert.IsNotNull(cardPrefab);
            _hand = Child("Hand", typeof(RectTransform)).AddComponent<HandFanView>();
            _hand.EditorBuild(cardPrefab);
            _hand.SetCards(
                new[]
                {
                    ExecutionPresentation(),
                    ExecutionPresentation(),
                    ExecutionPresentation()
                },
                _ => { },
                (_, __) => { });
            _rail = Child("Rail", typeof(RectTransform)).AddComponent<ExecutionRailView>();
            var dim = Child("Dim");
            _confirmButton = Child("Confirm").AddComponent<Button>();
            _overlay = (RectTransform)Child("Overlay", typeof(RectTransform)).transform;
            var railPrefab = RailCardView.EditorCreate(
                (RectTransform)Child("RailPrefabRoot", typeof(RectTransform)).transform,
                new Vector2(96f, 132f));
            _rail.EditorBuild(null, railPrefab, _overlay);
            _rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
            _arrow = TargetingArrowView.EditorCreate(_overlay);
            _controller = _root.AddComponent<CardSelectionController>();

            SetField(_controller, "_hand", _hand);
            SetField(_controller, "_rail", _rail);
            SetField(_controller, "_dimLayer", dim);
            SetField(_controller, "_confirmButton", _confirmButton);
            SetField(_controller, "_arrow", _arrow);
            typeof(CardSelectionController)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_controller, null);

            dim.SetActive(false);
            _confirmButton.gameObject.SetActive(false);
            _root.SetActive(true);
            _controller.Initialize(
                result =>
                {
                    _appliedResults.Add(result);
                    return true;
                },
                _ => Array.Empty<SelectionTargetRef>(),
                () => _onAppliedCalls++);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.Clear(true);
            UnityEngine.Object.DestroyImmediate(_root);
            _appliedResults.Clear();
            _onAppliedCalls = 0;
        }

        [Test]
        public void Single_target_shows_arrow_never_shows_confirm_and_dispatches_on_click()
        {
            var target = SelectionTargetRef.ExecutionCard(0);
            _controller.BeginTargetSelection(
                0, SelectionTargetKind.ExecutionCard, 1, new[] { target });

            Assert.IsTrue(_arrow.gameObject.activeSelf);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);

            _controller.OnTargetClicked(target);

            Assert.AreEqual(1, _appliedResults.Count);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);
        }

        [Test]
        public void Multiple_targets_show_confirm_only_after_requirement_is_met()
        {
            var first = SelectionTargetRef.ExecutionCard(0);
            var second = SelectionTargetRef.ExecutionCard(1);
            _controller.BeginTargetSelection(
                0, SelectionTargetKind.ExecutionCard, 2, new[] { first, second });

            _controller.OnTargetClicked(first);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);

            _controller.OnTargetClicked(second);
            Assert.IsTrue(_confirmButton.gameObject.activeSelf);
            Assert.AreEqual(0, _appliedResults.Count);

            _confirmButton.onClick.Invoke();
            Assert.AreEqual(1, _appliedResults.Count);
        }

        [Test]
        public void Selected_target_click_restores_candidate_and_hides_confirm()
        {
            var first = SelectionTargetRef.ExecutionCard(0);
            var second = SelectionTargetRef.ExecutionCard(1);
            _controller.BeginTargetSelection(
                0, SelectionTargetKind.ExecutionCard, 2, new[] { first, second });

            _controller.OnTargetClicked(first);
            _controller.OnTargetClicked(second);
            Assert.IsTrue(_confirmButton.gameObject.activeSelf);

            _controller.OnTargetClicked(first);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);

            _controller.OnTargetClicked(first);
            Assert.IsTrue(_confirmButton.gameObject.activeSelf);
        }

        [Test]
        public void Rejected_result_removes_stale_pick_and_keeps_selection_active()
        {
            var first = SelectionTargetRef.ExecutionCard(0);
            var second = SelectionTargetRef.ExecutionCard(1);
            var third = SelectionTargetRef.ExecutionCard(2);
            _controller.Initialize(
                result =>
                {
                    _appliedResults.Add(result);
                    return false;
                },
                _ => new[] { second, third },
                () => { });

            _controller.BeginTargetSelection(
                0, SelectionTargetKind.ExecutionCard, 2, new[] { first, second, third });
            _controller.OnTargetClicked(first);
            _controller.OnTargetClicked(second);
            _confirmButton.onClick.Invoke();

            Assert.AreEqual(1, _appliedResults.Count);
            Assert.IsTrue(_controller.SelectionActive);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);
        }

        [Test]
        public void Target_click_does_not_create_center_emphasis_card()
        {
            var target = SelectionTargetRef.ExecutionCard(0);
            int childCountBefore = _overlay.childCount;
            _controller.BeginTargetSelection(
                0, SelectionTargetKind.ExecutionCard, 1, new[] { target });

            _controller.OnTargetClicked(target);

            Assert.AreEqual(childCountBefore, _overlay.childCount);
        }

        [Test]
        public void Placement_hover_is_static_until_hand_card_is_selected()
        {
            var card = ExecutionPresentation();
            _controller.ShowPlacementHover(0, card, 0);
            var rail = Field<ExecutionRailView>(_controller, "_rail");
            var preview = Field<RailCardView>(rail, "_placementPreview");

            Assert.IsTrue(preview.gameObject.activeSelf);
            Assert.IsNull(Field<Tween>(rail, "_placementPreviewTween"));

            _controller.BeginPlacement(0, card, 0);

            Assert.IsTrue(_controller.SelectionActive);
            Assert.IsNotNull(Field<Tween>(rail, "_placementPreviewTween"));
            Assert.AreEqual(0, _appliedResults.Count);
        }

        [Test]
        public void Armed_silhouette_applies_once_and_refreshes_only_after_flight_lands()
        {
            var card = ExecutionPresentation();
            _controller.ShowPlacementHover(0, card, 0);
            _controller.BeginPlacement(0, card, 0);
            var preview = Field<RailCardView>(_rail, "_placementPreview");
            var button = Field<Button>(preview, "_button");

            button.onClick.Invoke();
            button.onClick.Invoke();

            Assert.AreEqual(1, _appliedResults.Count);
            Assert.IsTrue(_controller.SelectionActive);
            Assert.AreEqual(0, _onAppliedCalls);
            Assert.IsFalse(button.interactable);
            var sequence = Field<Sequence>(_rail, "_placementFlightSequence");
            Assert.IsTrue(sequence.IsActive());

            sequence.Complete();

            Assert.IsFalse(_controller.SelectionActive);
            Assert.AreEqual(1, _onAppliedCalls);
            Assert.IsNull(Field<Sequence>(_rail, "_placementFlightSequence"));
        }

        [Test]
        public void Begin_placement_holds_the_hand_card_in_blue_hover_pose()
        {
            var card = ExecutionPresentation();
            var source = Field<List<CardView>>(_hand, "_views")[0];

            _controller.ShowPlacementHover(0, card, 0);
            _controller.BeginPlacement(0, card, 0);

            Assert.AreEqual(Quaternion.identity, source.transform.localRotation);
            Assert.AreEqual(Vector3.one * 1.35f, source.transform.localScale);
            var outline = Field<Outline>(source, "_selectionOutline");
            Assert.IsTrue(outline.enabled);
            Assert.AreEqual(SelectedOutline, outline.effectColor);
        }

        [Test]
        public void Placement_hover_exit_hides_unselected_preview()
        {
            _controller.ShowPlacementHover(0, ExecutionPresentation(), 0);
            var rail = Field<ExecutionRailView>(_controller, "_rail");
            var preview = Field<RailCardView>(rail, "_placementPreview");

            _controller.HidePlacementHover(0);

            Assert.IsFalse(preview.gameObject.activeSelf);
        }

        [Test]
        public void Intervention_hover_never_creates_execution_preview()
        {
            var intervention = new CardPresentation(
                "intervention", "intervention", 0, 1,
                FateWeaver.Core.Cards.Side.Player,
                string.Empty, null, false,
                category: FateWeaver.Core.Cards.CardCategory.Intervention);
            _controller.ShowPlacementHover(0, intervention, 0);
            var rail = Field<ExecutionRailView>(_controller, "_rail");

            Assert.IsNull(Field<RailCardView>(rail, "_placementPreview"));
        }

        [Test]
        public void Rejected_targetless_placement_clears_selection_and_pulse()
        {
            _controller.Initialize(
                result =>
                {
                    _appliedResults.Add(result);
                    return false;
                },
                _ => Array.Empty<SelectionTargetRef>(),
                () => _onAppliedCalls++);
            _controller.ShowPlacementHover(0, ExecutionPresentation(), 0);
            _controller.BeginPlacement(0, ExecutionPresentation(), 0);
            var rail = Field<ExecutionRailView>(_controller, "_rail");
            var preview = Field<RailCardView>(rail, "_placementPreview");
            var tween = Field<Tween>(rail, "_placementPreviewTween");

            Field<Button>(preview, "_button").onClick.Invoke();

            Assert.AreEqual(1, _appliedResults.Count);
            Assert.IsFalse(_controller.SelectionActive);
            Assert.IsFalse(preview.gameObject.activeSelf);
            Assert.IsFalse(tween.IsActive());
            Assert.IsNull(Field<Sequence>(_rail, "_placementFlightSequence"));
            Assert.AreEqual(0, _onAppliedCalls);
            Assert.AreEqual(
                0,
                _overlay.GetComponentsInChildren<CardView>(true).Length);
        }

        [Test]
        public void Existing_rail_card_click_does_not_confirm_armed_placement()
        {
            _controller.ShowPlacementHover(0, ExecutionPresentation(), 0);
            _controller.BeginPlacement(0, ExecutionPresentation(), 0);

            _controller.OnTargetClicked(SelectionTargetRef.ExecutionCard(0));

            Assert.AreEqual(0, _appliedResults.Count);
            Assert.IsTrue(_controller.SelectionActive);
        }

        private static CardPresentation ExecutionPresentation()
            => new CardPresentation(
                "execution", "execution", 3, 1,
                FateWeaver.Core.Cards.Side.Player,
                string.Empty, null, false);

        private static void SimulateDotweenRuntimeInitializationForEditMode()
        {
            var initialized = typeof(DOTween).GetField(
                "initialized", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(initialized);
            initialized.SetValue(null, true);
        }

        private GameObject Child(string name, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private static void SetField(object target, string name, object value)
        {
            typeof(CardSelectionController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
