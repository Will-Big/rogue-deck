namespace FateWeaver.Simulation.Presentation
{
    /// <summary>Pose of one hand card in the fan, in abstract units relative to the fan center.
    /// Views multiply offsets into pixels and apply AngleDegrees as a Z rotation.</summary>
    public readonly struct FanPose
    {
        /// <summary>Signed X offset from the fan center in abstract units (left cards negative).</summary>
        public float XOffset { get; }

        /// <summary>Vertical offset from the fan center (edge cards sink below zero).</summary>
        public float YOffset { get; }

        /// <summary>Z tilt in degrees, Unity counter-clockwise positive.</summary>
        public float AngleDegrees { get; }

        public FanPose(float xOffset, float yOffset, float angleDegrees)
        {
            XOffset = xOffset;
            YOffset = yOffset;
            AngleDegrees = angleDegrees;
        }
    }

    /// <summary>Curved-fan hand layout. Pure C# (no UnityEngine) so it stays headless-testable.
    /// Callers derive count from a non-empty hand list; no empty-count behavior is required.</summary>
    public static class HandFanLayout
    {
        /// <summary>spacing = X per slot, anglePerCard = degrees per slot (left card tilts CCW = positive),
        /// arcDrop = how far edge cards sink per squared slot distance.</summary>
        public static FanPose PoseFor(int index, int count, float spacing, float anglePerCard, float arcDrop)
        {
            float t = index - (count - 1) * 0.5f;
            return new FanPose(t * spacing, -arcDrop * t * t, -t * anglePerCard);
        }
    }
}
