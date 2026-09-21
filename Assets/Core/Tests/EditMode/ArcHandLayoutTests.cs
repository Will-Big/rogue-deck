using FateWeaver.Simulation.Presentation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class ArcHandLayoutTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void Empty_and_single_hands_have_no_offset_or_rotation(int count)
        {
            var pose = ArcHandLayout.PoseFor(0, count, 100f, 120f);
            Assert.AreEqual(0f, pose.XOffset);
            Assert.AreEqual(0f, pose.YOffset);
            Assert.AreEqual(0f, pose.AngleDegrees);
        }

        [Test]
        public void Full_fan_places_cards_on_a_circle_with_symmetric_edges()
        {
            var left = ArcHandLayout.PoseFor(0, 5, 100f, 120f);
            var middle = ArcHandLayout.PoseFor(2, 5, 100f, 120f);
            var right = ArcHandLayout.PoseFor(4, 5, 100f, 120f);
            Assert.That(left.XOffset, Is.EqualTo(-86.60254f).Within(.001f));
            Assert.That(left.YOffset, Is.EqualTo(-50f).Within(.001f));
            Assert.That(left.AngleDegrees, Is.EqualTo(60f).Within(.001f));
            Assert.That(right.XOffset, Is.EqualTo(86.60254f).Within(.001f));
            Assert.That(right.YOffset, Is.EqualTo(-50f).Within(.001f));
            Assert.That(right.AngleDegrees, Is.EqualTo(-60f).Within(.001f));
            Assert.AreEqual(0f, middle.XOffset);
            Assert.AreEqual(0f, middle.YOffset);
        }

        [Test]
        public void Adaptive_spread_interpolates_from_minimum_to_full_angle_and_caps_at_full()
        {
            Assert.That(ArcHandLayout.PoseFor(0, 3, 100f, 100f,
                minimumAngle: 20f).AngleDegrees, Is.EqualTo(30f).Within(.001f));
            Assert.That(ArcHandLayout.PoseFor(0, 8, 100f, 100f,
                minimumAngle: 20f).AngleDegrees, Is.EqualTo(50f).Within(.001f));
            Assert.That(ArcHandLayout.PoseFor(0, 2, 100f, 100f,
                adaptiveSpread: false).AngleDegrees, Is.EqualTo(50f).Within(.001f));
        }

        [Test]
        public void Extra_angle_applies_per_gap_including_even_hands()
        {
            var left = ArcHandLayout.PoseFor(0, 4, 100f, 30f,
                adaptiveSpread: false, extraAnglePerCard: 10f);
            Assert.That(left.AngleDegrees, Is.EqualTo(30f).Within(.001f));
            Assert.That(left.XOffset, Is.EqualTo(-50f).Within(.001f));
        }

        [Test]
        public void Rotation_options_do_not_change_arc_positions()
        {
            var inverted = ArcHandLayout.PoseFor(0, 5, 100f, 120f, invertRotation: true);
            var upright = ArcHandLayout.PoseFor(0, 5, 100f, 120f, rotateWithArc: false);
            Assert.That(inverted.AngleDegrees, Is.EqualTo(-60f).Within(.001f));
            Assert.AreEqual(0f, upright.AngleDegrees);
            Assert.That(inverted.XOffset, Is.EqualTo(-86.60254f).Within(.001f));
            Assert.That(upright.YOffset, Is.EqualTo(-50f).Within(.001f));
        }

        [Test]
        public void Degenerate_settings_do_not_produce_nan_or_wrapping_cards()
        {
            var pose = ArcHandLayout.PoseFor(0, 3, -10f, 400f,
                cardsForFullSpread: 1, minimumAngle: 500f, extraAnglePerCard: 100f);
            Assert.AreEqual(0f, pose.XOffset);
            Assert.AreEqual(0f, pose.YOffset);
            Assert.That(pose.AngleDegrees, Is.EqualTo(90f).Within(.001f));
        }
    }
}
