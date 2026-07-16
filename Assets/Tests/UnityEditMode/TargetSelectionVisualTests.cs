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
    public class TargetSelectionVisualTests
    {
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
        public void Unit_target_state_shows_candidate_and_selection_order()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            try
            {
                var view = UnitView.EditorCreate(
                    (RectTransform)root.transform, new Vector2(180f, 250f));
                view.SetTargetSelection(true, true, 2);

                var badge = (GameObject)typeof(UnitView)
                    .GetField("_targetOrderBadge", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);
                Assert.IsTrue(badge.activeSelf);
                Assert.AreEqual("2", badge.GetComponentInChildren<TMP_Text>().text);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Rail_target_state_shows_pick_order_and_dims_noncandidates()
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

                var firstBadge = (GameObject)typeof(RailCardView)
                    .GetField("_targetOrderBadge", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[0]);
                var thirdDim = (GameObject)typeof(RailCardView)
                    .GetField("_targetDim", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[2]);
                var firstButton = (Button)typeof(RailCardView)
                    .GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[0]);
                var thirdButton = (Button)typeof(RailCardView)
                    .GetField("_button", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(views[2]);
                Assert.IsTrue(firstBadge.activeSelf);
                Assert.AreEqual("1", firstBadge.GetComponentInChildren<TMP_Text>().text);
                Assert.IsTrue(thirdDim.activeSelf);
                Assert.IsTrue(firstButton.interactable);
                Assert.IsFalse(thirdButton.interactable);

                rail.SetTargetSelection(false, candidates, pickedTargets);

                Assert.IsFalse(firstBadge.activeSelf);
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
