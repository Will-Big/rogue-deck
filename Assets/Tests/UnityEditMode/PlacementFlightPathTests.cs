using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    public class PlacementFlightPathTests
    {
        private const float Split = 0.72f;
        private PlacementFlightPath.Geometry _geometry;

        [SetUp]
        public void SetUp()
        {
            var settings = new PlacementFlightPath.Settings(
                riseRatio: 0.7f,
                overshootRatio: 0.9f,
                approachWidthRatio: 1.25f,
                approachDropRatio: 0.3f);
            _geometry = PlacementFlightPath.Create(
                new Vector2(0f, -300f),
                new Vector2(0f, 100f),
                new Vector2(96f, 132f),
                settings);
        }

        [Test]
        public void Tangents_follow_12_to_2_to_10_to_9_then_finish_at_12()
        {
            var start = PlacementFlightPath.Evaluate(_geometry, 0f, Split);
            var early = PlacementFlightPath.Evaluate(_geometry, Split * 0.25f, Split);
            var rewind = PlacementFlightPath.Evaluate(_geometry, Split * 0.95f, Split);
            var firstEnd = PlacementFlightPath.Evaluate(_geometry, Split, Split);
            var secondStart = PlacementFlightPath.Evaluate(
                _geometry, Split + 0.0001f, Split);
            var end = PlacementFlightPath.Evaluate(_geometry, 1f, Split);

            Assert.That(Vector2.Angle(Vector2.up, start.Tangent), Is.LessThan(0.01f));
            Assert.Greater(early.Tangent.x, 0f, "early tangent should turn toward 2 o'clock");
            Assert.Less(rewind.Tangent.x, 0f, "late first segment should turn toward 10 o'clock");
            Assert.Greater(rewind.Tangent.y, 0f);
            Assert.That(Vector2.Angle(Vector2.left, firstEnd.Tangent), Is.LessThan(0.01f));
            Assert.That(Vector2.Angle(Vector2.left, secondStart.Tangent), Is.LessThan(0.1f));
            Assert.That(Vector2.Angle(Vector2.up, end.Tangent), Is.LessThan(0.01f));
        }

        [Test]
        public void Final_segment_never_drops_below_the_silhouette_bottom()
        {
            float silhouetteBottom = 100f - 132f * 0.5f;
            for (int i = 0; i <= 20; i++)
            {
                float progress = Mathf.Lerp(Split, 1f, i / 20f);
                var sample = PlacementFlightPath.Evaluate(_geometry, progress, Split);
                Assert.GreaterOrEqual(sample.Position.y, silhouetteBottom);
            }
        }

        [Test]
        public void End_sample_matches_the_target_and_zero_degree_rotation()
        {
            var end = PlacementFlightPath.Evaluate(_geometry, 1f, Split);

            Assert.AreEqual(new Vector2(0f, 100f), end.Position);
            Assert.That(Mathf.Abs(end.AngleDegrees), Is.LessThan(0.01f));
        }

        [Test]
        public void Flip_angle_rises_to_edge_on_then_unfolds_to_zero()
        {
            Assert.That(PlacementFlightPath.FlipAngle(0f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(PlacementFlightPath.FlipAngle(0.25f), Is.EqualTo(45f).Within(0.001f));
            Assert.That(PlacementFlightPath.FlipAngle(0.4999f), Is.EqualTo(89.982f).Within(0.01f));
            Assert.That(PlacementFlightPath.FlipAngle(0.5f), Is.EqualTo(-90f).Within(0.001f));
            Assert.That(PlacementFlightPath.FlipAngle(0.75f), Is.EqualTo(-45f).Within(0.001f));
            Assert.That(PlacementFlightPath.FlipAngle(1f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Flip_angle_clamps_out_of_range_progress()
        {
            Assert.That(PlacementFlightPath.FlipAngle(-1f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(PlacementFlightPath.FlipAngle(2f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Settle_progress_is_zero_on_the_first_segment_and_normalized_after()
        {
            Assert.That(PlacementFlightPath.SettleProgress(0f, Split), Is.EqualTo(0f).Within(0.001f));
            Assert.That(PlacementFlightPath.SettleProgress(Split, Split), Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                PlacementFlightPath.SettleProgress(Split + (1f - Split) * 0.5f, Split),
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(PlacementFlightPath.SettleProgress(1f, Split), Is.EqualTo(1f).Within(0.001f));
        }
    }
}
