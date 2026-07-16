using System;
using System.Collections.Generic;
using System.Reflection;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardSelectionControllerTests
    {
        private GameObject _root;
        private CardSelectionController _controller;
        private Button _confirmButton;
        private TargetingArrowView _arrow;
        private readonly List<SelectionResult> _appliedResults = new List<SelectionResult>();

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CardSelectionControllerTests", typeof(RectTransform));
            _root.SetActive(false);

            var hand = Child("Hand").AddComponent<HandFanView>();
            var rail = Child("Rail").AddComponent<ExecutionRailView>();
            var dim = Child("Dim");
            _confirmButton = Child("Confirm").AddComponent<Button>();
            var overlay = (RectTransform)Child("Overlay", typeof(RectTransform)).transform;
            _arrow = TargetingArrowView.EditorCreate(overlay);
            _controller = _root.AddComponent<CardSelectionController>();

            SetField(_controller, "_hand", hand);
            SetField(_controller, "_rail", rail);
            SetField(_controller, "_dimLayer", dim);
            SetField(_controller, "_confirmButton", _confirmButton);
            SetField(_controller, "_overlay", overlay);
            SetField(_controller, "_arrow", _arrow);

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
                () => { });
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            _appliedResults.Clear();
        }

        [Test]
        public void Single_target_shows_arrow_never_shows_confirm_and_dispatches_on_click()
        {
            var target = SelectionTargetRef.PartyMember("member-a");
            _controller.BeginTargetSelection(
                0, SelectionTargetKind.PartyMember, 1, new[] { target });

            Assert.IsTrue(_arrow.gameObject.activeSelf);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);

            _controller.OnTargetClicked(target, null);

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

            _controller.OnTargetClicked(first, null);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);

            _controller.OnTargetClicked(second, null);
            Assert.IsTrue(_confirmButton.gameObject.activeSelf);
            Assert.AreEqual(0, _appliedResults.Count);

            _confirmButton.onClick.Invoke();
            Assert.AreEqual(1, _appliedResults.Count);
        }

        [Test]
        public void Rejected_result_removes_stale_pick_and_keeps_selection_active()
        {
            var first = SelectionTargetRef.PartyMember("member-a");
            var second = SelectionTargetRef.PartyMember("member-b");
            var third = SelectionTargetRef.PartyMember("member-c");
            var firstView = UnitView.EditorCreate(
                (RectTransform)_root.transform, new Vector2(180f, 250f));
            var secondView = UnitView.EditorCreate(
                (RectTransform)_root.transform, new Vector2(180f, 250f));
            _controller.RegisterUnitTarget(first, firstView);
            _controller.RegisterUnitTarget(second, secondView);
            _controller.Initialize(
                result =>
                {
                    _appliedResults.Add(result);
                    return false;
                },
                _ => new[] { second, third },
                () => { });

            _controller.BeginTargetSelection(
                0, SelectionTargetKind.PartyMember, 2, new[] { first, second, third });
            _controller.OnTargetClicked(first, null);
            _controller.OnTargetClicked(second, null);
            _confirmButton.onClick.Invoke();

            Assert.AreEqual(1, _appliedResults.Count);
            Assert.IsTrue(_controller.SelectionActive);
            Assert.IsFalse(Badge(firstView).activeSelf);
            Assert.IsTrue(Badge(secondView).activeSelf);
            Assert.AreEqual("1", Badge(secondView).GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(_confirmButton.gameObject.activeSelf);
        }

        private GameObject Child(string name, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        private static GameObject Badge(UnitView view)
        {
            return (GameObject)typeof(UnitView)
                .GetField("_targetOrderBadge", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(view);
        }

        private static void SetField(object target, string name, object value)
        {
            typeof(CardSelectionController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
