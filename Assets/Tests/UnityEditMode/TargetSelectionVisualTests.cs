using System.Collections.Generic;
using System.Reflection;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Tests.UnityEditMode
{
    public class TargetSelectionVisualTests
    {
        private static readonly Color CandidateOutline =
            new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color SelectedOutline =
            new Color(0.35f, 0.75f, 0.95f, 1f);

        [Test]
        public void Arrow_tracks_a_new_start_point_each_frame()
        {
            var overlay = new GameObject("Overlay", typeof(RectTransform));
            try
            {
                ((RectTransform)overlay.transform).sizeDelta = new Vector2(1280f, 720f);
                var arrow = TargetingArrowView.EditorCreate((RectTransform)overlay.transform);
                arrow.Show(new Vector2(100f, 100f), new Vector2(300f, 100f));
                arrow.Track(new Vector2(150f, 100f), new Vector2(350f, 100f));

                var shaft = (RectTransform)typeof(TargetingArrowView)
                    .GetField("_shaft", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(arrow);
                Assert.AreEqual(200f, shaft.sizeDelta.x, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(overlay);
            }
        }

        [Test]
        public void Unit_target_state_uses_gold_for_candidate_and_blue_for_selected()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var view = UnitView.EditorCreate(
                    (RectTransform)root.transform, new Vector2(180f, 250f));
                var highlight = (Image)typeof(UnitView)
                    .GetField("_targetHighlight", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);

                view.SetTargetSelection(true, true, false);
                Assert.AreEqual(CandidateOutline, highlight.color);

                view.SetTargetSelection(true, true, true);
                Assert.AreEqual(SelectedOutline, highlight.color);
                Assert.IsNull(view.transform.Find("TargetOrderBadge"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rail_target_state_uses_blue_for_picks_and_dims_noncandidates()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var rail = root.AddComponent<ExecutionRailView>();
                var views = (List<RailCardView>)typeof(ExecutionRailView)
                    .GetField("_views", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(rail);
                for (int i = 0; i < 3; i++)
                {
                    views.Add(RailCardView.EditorCreate(
                        (RectTransform)root.transform, new Vector2(96f, 132f)));
                }

                var candidates = new[]
                {
                    SelectionTargetRef.ExecutionCard(0),
                    SelectionTargetRef.ExecutionCard(1),
                };
                var pickedTargets = new[] { SelectionTargetRef.ExecutionCard(0) };

                rail.SetTargetSelection(true, candidates, pickedTargets);

                var firstOutline = (Image)typeof(RailCardView)
                    .GetField("_selectionOutline", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[0]);
                var secondOutline = (Image)typeof(RailCardView)
                    .GetField("_selectionOutline", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[1]);
                var thirdDim = (GameObject)typeof(RailCardView)
                    .GetField("_targetDim", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[2]);
                var firstButton = (Button)typeof(RailCardView)
                    .GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[0]);
                var thirdButton = (Button)typeof(RailCardView)
                    .GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[2]);
                Assert.AreEqual(SelectedOutline, firstOutline.color);
                Assert.AreEqual(CandidateOutline, secondOutline.color);
                Assert.IsTrue(thirdDim.activeSelf);
                Assert.IsTrue(firstButton.interactable);
                Assert.IsFalse(thirdButton.interactable);
                foreach (var view in views)
                {
                    Assert.IsNull(view.transform.Find("TargetOrderBadge"));
                }

                rail.SetTargetSelection(false, candidates, pickedTargets);

                Assert.AreEqual(Color.clear, firstOutline.color);
                Assert.AreEqual(Color.clear, secondOutline.color);
                Assert.IsFalse(thirdDim.activeSelf);
                Assert.IsTrue(firstButton.interactable);
                Assert.IsTrue(thirdButton.interactable);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
