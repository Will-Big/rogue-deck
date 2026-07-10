using NUnit.Framework;
using FateWeaver.Simulation.Presentation;

namespace FateWeaver.Tests
{
    public class HandFanLayoutTests
    {
        [Test]
        public void Single_card_sits_at_center_with_no_tilt()
        {
            var pose = HandFanLayout.PoseFor(0, 1, 170f, 4f, 12f);

            Assert.AreEqual(0f, pose.XOffset);
            Assert.AreEqual(0f, pose.YOffset);
            Assert.AreEqual(0f, pose.AngleDegrees);
        }

        [Test]
        public void Middle_card_of_odd_hand_is_centered()
        {
            var pose = HandFanLayout.PoseFor(2, 5, 170f, 4f, 12f);

            Assert.AreEqual(0f, pose.XOffset);
            Assert.AreEqual(0f, pose.YOffset);
            Assert.AreEqual(0f, pose.AngleDegrees);
        }

        [Test]
        public void Fan_is_symmetric_around_center()
        {
            var left = HandFanLayout.PoseFor(0, 5, 170f, 4f, 12f);
            var right = HandFanLayout.PoseFor(4, 5, 170f, 4f, 12f);

            Assert.AreEqual(-right.XOffset, left.XOffset, 1e-4f);
            Assert.AreEqual(right.YOffset, left.YOffset, 1e-4f);
            Assert.AreEqual(-right.AngleDegrees, left.AngleDegrees, 1e-4f);
        }

        [Test]
        public void Left_card_sits_left_sinks_and_tilts_counterclockwise()
        {
            var left = HandFanLayout.PoseFor(0, 5, 170f, 4f, 12f);

            Assert.Less(left.XOffset, 0f);
            Assert.Less(left.YOffset, 0f);
            Assert.Greater(left.AngleDegrees, 0f);
        }

        [Test]
        public void Even_hand_straddles_the_center()
        {
            var a = HandFanLayout.PoseFor(1, 4, 170f, 4f, 12f);
            var b = HandFanLayout.PoseFor(2, 4, 170f, 4f, 12f);

            Assert.AreEqual(-85f, a.XOffset, 1e-4f);
            Assert.AreEqual(85f, b.XOffset, 1e-4f);
        }
    }
}
