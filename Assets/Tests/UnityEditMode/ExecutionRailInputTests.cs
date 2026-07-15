using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
    }
}
