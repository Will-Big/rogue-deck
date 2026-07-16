using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                SetField(selection, "_overlay", overlay);
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
                        SelectionTargetRef.ExecutionCard(index), card));
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

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
