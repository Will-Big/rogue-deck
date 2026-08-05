using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Pure math for the placement flight: a cubic Bézier climb from the hand to a
    /// right-side approach point, then a quadratic settle into the rail silhouette. Samples
    /// expose position plus the clockwise tangent so the card can rotate along its path.</summary>
    public static class PlacementFlightPath
    {
        private const float MinDimension = 0.001f;
        private const float MinSegmentRatio = 0.01f;
        private const float MaxSegmentRatio = 0.99f;
        private const float MaxApproachDropRatio = 0.49f;

        public readonly struct Settings
        {
            public Settings(
                float riseRatio,
                float overshootRatio,
                float approachWidthRatio,
                float approachDropRatio)
            {
                RiseRatio = riseRatio;
                OvershootRatio = overshootRatio;
                ApproachWidthRatio = approachWidthRatio;
                ApproachDropRatio = approachDropRatio;
            }

            public float RiseRatio { get; }
            public float OvershootRatio { get; }
            public float ApproachWidthRatio { get; }
            public float ApproachDropRatio { get; }
        }

        public readonly struct Geometry
        {
            internal Geometry(
                Vector2 start,
                Vector2 firstControl,
                Vector2 secondControl,
                Vector2 approach,
                Vector2 settleControl,
                Vector2 target)
            {
                Start = start;
                FirstControl = firstControl;
                SecondControl = secondControl;
                Approach = approach;
                SettleControl = settleControl;
                Target = target;
            }

            public Vector2 Start { get; }
            public Vector2 FirstControl { get; }
            public Vector2 SecondControl { get; }
            public Vector2 Approach { get; }
            public Vector2 SettleControl { get; }
            public Vector2 Target { get; }
        }

        public readonly struct Sample
        {
            internal Sample(Vector2 position, Vector2 tangent)
            {
                Position = position;
                Tangent = tangent.sqrMagnitude > MinDimension * MinDimension
                    ? tangent.normalized
                    : Vector2.up;
                AngleDegrees = Vector2.SignedAngle(Vector2.up, Tangent);
            }

            public Vector2 Position { get; }
            public Vector2 Tangent { get; }
            public float AngleDegrees { get; }
        }

        public static Geometry Create(
            Vector2 start,
            Vector2 target,
            Vector2 targetSize,
            Settings settings)
        {
            float width = Mathf.Max(Mathf.Abs(targetSize.x), MinDimension);
            float height = Mathf.Max(Mathf.Abs(targetSize.y), MinDimension);
            float verticalGap = Mathf.Max(target.y - start.y, height);
            float dropRatio = Mathf.Clamp(
                settings.ApproachDropRatio, 0f, MaxApproachDropRatio);
            Vector2 approach = target
                + Vector2.right * width * settings.ApproachWidthRatio
                + Vector2.down * height * dropRatio;
            Vector2 firstControl = start
                + Vector2.up * verticalGap * settings.RiseRatio;
            Vector2 secondControl = approach
                + Vector2.right * width * settings.OvershootRatio;
            Vector2 settleControl = new Vector2(target.x, approach.y);
            return new Geometry(
                start,
                firstControl,
                secondControl,
                approach,
                settleControl,
                target);
        }

        public static Sample Evaluate(
            Geometry geometry,
            float progress,
            float segmentSplit)
        {
            float split = Mathf.Clamp(
                segmentSplit, MinSegmentRatio, MaxSegmentRatio);
            float clamped = Mathf.Clamp01(progress);
            if (clamped <= split)
            {
                float t = clamped / split;
                return new Sample(
                    Cubic(
                        geometry.Start,
                        geometry.FirstControl,
                        geometry.SecondControl,
                        geometry.Approach,
                        t),
                    CubicDerivative(
                        geometry.Start,
                        geometry.FirstControl,
                        geometry.SecondControl,
                        geometry.Approach,
                        t));
            }

            float settleT = (clamped - split) / (1f - split);
            return new Sample(
                Quadratic(
                    geometry.Approach,
                    geometry.SettleControl,
                    geometry.Target,
                    settleT),
                QuadraticDerivative(
                    geometry.Approach,
                    geometry.SettleControl,
                    geometry.Target,
                    settleT));
        }

        public const float FlipSwapProgress = 0.5f;

        /// <summary>0 on the first segment; normalized 0→1 across the settle segment,
        /// using the same split clamp as Evaluate.</summary>
        public static float SettleProgress(float progress, float segmentSplit)
        {
            float split = Mathf.Clamp(
                segmentSplit, MinSegmentRatio, MaxSegmentRatio);
            float clamped = Mathf.Clamp01(progress);
            return clamped <= split
                ? 0f
                : (clamped - split) / (1f - split);
        }

        /// <summary>Y-axis flip: the front face turns 0→90° until FlipSwapProgress,
        /// then the mini face unfolds -90°→0°. Both halves meet edge-on, so the swap
        /// is invisible and the flight lands at exactly 0°.</summary>
        public static float FlipAngle(float settleT)
        {
            float clamped = Mathf.Clamp01(settleT);
            return clamped < FlipSwapProgress
                ? clamped * 180f
                : clamped * 180f - 180f;
        }

        private static Vector2 Cubic(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * p0
                + 3f * inverse * inverse * t * p1
                + 3f * inverse * t * t * p2
                + t * t * t * p3;
        }

        private static Vector2 CubicDerivative(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * (p1 - p0)
                + 6f * inverse * t * (p2 - p1)
                + 3f * t * t * (p3 - p2);
        }

        private static Vector2 Quadratic(
            Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * p0
                + 2f * inverse * t * p1
                + t * t * p2;
        }

        private static Vector2 QuadraticDerivative(
            Vector2 p0, Vector2 p1, Vector2 p2, float t)
            => 2f * (1f - t) * (p1 - p0)
                + 2f * t * (p2 - p1);
    }
}
