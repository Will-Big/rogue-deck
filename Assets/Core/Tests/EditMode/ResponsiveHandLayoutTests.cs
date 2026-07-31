using FateWeaver.Simulation.Presentation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class ResponsiveHandLayoutTests
    {
        [Test]
        public void Wide_five_card_hand_keeps_baseline_spacing_and_scale()
        {
            var result = ResponsiveHandLayout.Calculate(900f, 260f, 5, Settings());

            Assert.AreEqual(150f, result.Spacing, 0.01f);
            Assert.AreEqual(1f, result.Scale, 0.001f);
        }

        [Test]
        public void Narrow_hand_reduces_spacing_before_scaling()
        {
            var result = ResponsiveHandLayout.Calculate(650f, 260f, 5, Settings());

            Assert.That(result.Spacing, Is.InRange(72f, 149.99f));
            Assert.AreEqual(1f, result.Scale, 0.001f);
        }

        [Test]
        public void Too_small_hand_uses_minimum_spacing_then_uniform_scale()
        {
            var result = ResponsiveHandLayout.Calculate(420f, 190f, 5, Settings());

            Assert.AreEqual(72f, result.Spacing, 0.01f);
            Assert.That(result.Scale, Is.LessThan(1f));
            Assert.That(result.Scale, Is.GreaterThanOrEqualTo(Settings().MinimumScale));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void Zero_or_one_card_uses_baseline_spacing_without_division(int cardCount)
        {
            var result = ResponsiveHandLayout.Calculate(24f, 24f, cardCount, Settings());

            Assert.AreEqual(150f, result.Spacing, 0.01f);
            Assert.IsFalse(float.IsNaN(result.Scale));
            Assert.IsFalse(float.IsInfinity(result.Scale));
            Assert.AreEqual(0.65f, result.Scale, 0.001f);
        }

        [Test]
        public void Height_constraint_can_set_scale_independently_of_width()
        {
            var result = ResponsiveHandLayout.Calculate(900f, 190f, 5, Settings());

            Assert.AreEqual(150f, result.Spacing, 0.01f);
            Assert.AreEqual(174f / 238f, result.Scale, 0.001f);
        }

        private static ResponsiveHandSettings Settings()
            => new ResponsiveHandSettings(
                cardWidth: 170f,
                requiredFanHeight: 238f,
                baseSpacing: 150f,
                minimumSpacing: 72f,
                badgeOverflow: 85.36f,
                horizontalSafeMargins: 32f,
                verticalSafeMargins: 16f,
                minimumScale: 0.65f);
    }
}
