using System;

namespace FateWeaver.Simulation.Presentation
{
    public readonly struct ResponsiveHandSettings
    {
        public float CardWidth { get; }
        public float RequiredFanHeight { get; }
        public float BaseSpacing { get; }
        public float MinimumSpacing { get; }
        public float BadgeOverflow { get; }
        public float HorizontalSafeMargins { get; }
        public float VerticalSafeMargins { get; }
        public float MinimumScale { get; }

        public ResponsiveHandSettings(
            float cardWidth,
            float requiredFanHeight,
            float baseSpacing,
            float minimumSpacing,
            float badgeOverflow,
            float horizontalSafeMargins,
            float verticalSafeMargins,
            float minimumScale)
        {
            CardWidth = cardWidth;
            RequiredFanHeight = requiredFanHeight;
            BaseSpacing = baseSpacing;
            MinimumSpacing = minimumSpacing;
            BadgeOverflow = badgeOverflow;
            HorizontalSafeMargins = horizontalSafeMargins;
            VerticalSafeMargins = verticalSafeMargins;
            MinimumScale = minimumScale;
        }
    }

    public readonly struct ResponsiveHandMetrics
    {
        public float Spacing { get; }
        public float Scale { get; }

        public ResponsiveHandMetrics(float spacing, float scale)
        {
            Spacing = spacing;
            Scale = scale;
        }
    }

    public static class ResponsiveHandLayout
    {
        public static ResponsiveHandMetrics Calculate(
            float availableWidth,
            float availableHeight,
            int cardCount,
            ResponsiveHandSettings settings)
        {
            float widthForCards = Math.Max(
                0f,
                availableWidth - settings.HorizontalSafeMargins);
            int slotCount = Math.Max(0, cardCount - 1);
            float spacing = settings.BaseSpacing;
            if (slotCount > 0)
            {
                float rawSpacing =
                    (widthForCards - settings.CardWidth - settings.BadgeOverflow)
                    / slotCount;
                spacing = Clamp(
                    rawSpacing,
                    settings.MinimumSpacing,
                    settings.BaseSpacing);
            }

            float widthAtMinimum =
                settings.CardWidth
                + settings.BadgeOverflow
                + settings.MinimumSpacing * slotCount;
            float widthScale = widthForCards / widthAtMinimum;
            float heightScale =
                (availableHeight - settings.VerticalSafeMargins)
                / settings.RequiredFanHeight;
            float scale = Math.Min(1f, Math.Min(widthScale, heightScale));
            scale = Math.Max(settings.MinimumScale, scale);
            return new ResponsiveHandMetrics(spacing, scale);
        }

        private static float Clamp(float value, float minimum, float maximum)
            => Math.Max(minimum, Math.Min(maximum, value));
    }
}
