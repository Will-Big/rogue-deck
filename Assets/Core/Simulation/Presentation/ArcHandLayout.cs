using System;

namespace FateWeaver.Simulation.Presentation
{
    /// <summary>Pure circular fan geometry, relative to the top of the arc.</summary>
    public static class ArcHandLayout
    {
        public static FanPose PoseFor(int index, int count, float radius, float totalAngle,
            bool rotateWithArc = true, bool invertRotation = false,
            bool adaptiveSpread = true, int cardsForFullSpread = 5,
            float minimumAngle = 0f, float extraAnglePerCard = 0f,
            int cardsForMaxSpread = 0, float maximumAngle = 0f)
        {
            if (count <= 1) return new FanPose(0f, 0f, 0f);

            radius = Math.Max(0f, radius);
            float full = Clamp(totalAngle, 0f, 180f);
            float spread = full;
            float minimum = Clamp(minimumAngle, 0f, full);
            if (adaptiveSpread)
            {
                float fraction = Clamp((count - 1f) / Math.Max(1, cardsForFullSpread - 1), 0f, 1f);
                spread = minimum + (full - minimum) * fraction;
                // Past the full count the fan keeps widening gently until the maximum count.
                if (cardsForMaxSpread > cardsForFullSpread && count > cardsForFullSpread)
                {
                    float beyond = Clamp((count - (float)cardsForFullSpread)
                        / (cardsForMaxSpread - cardsForFullSpread), 0f, 1f);
                    spread = full + (Clamp(maximumAngle, full, 180f) - full) * beyond;
                }
            }
            spread = Clamp(spread + extraAnglePerCard * (count - 1), 0f, 180f);
            float angle = (Clamp(index, 0, count - 1) / (count - 1f) - .5f) * spread;
            double radians = angle * Math.PI / 180.0;
            float rotation = rotateWithArc ? -angle : 0f;
            if (invertRotation) rotation = -rotation;
            return new FanPose(
                (float)(radius * Math.Sin(radians)),
                (float)(radius * (Math.Cos(radians) - 1.0)),
                rotation);
        }

        private static float Clamp(float value, float min, float max)
            => Math.Max(min, Math.Min(max, value));
    }
}
