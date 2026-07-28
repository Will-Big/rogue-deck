using System;

namespace FateWeaver.Core.Intervention
{
    /// <summary>What kind of thing the player must pick before a card can be played.
    /// New target kinds (ally, enemy, hand card...) are added here when the intervention
    /// card design lands — see the 2026-07-28 P0-C targeting spec.</summary>
    public enum TargetKind { None, RailCard }

    /// <summary>A card's target-selection demand, declared by its intervention handler.
    /// The UI drives selection from this; the core validates the final pick against it.</summary>
    public readonly struct TargetingRequirement
    {
        public TargetKind Kind { get; }
        public int Count { get; }
        public bool AllowDuplicates { get; }

        private TargetingRequirement(TargetKind kind, int count, bool allowDuplicates)
        {
            Kind = kind;
            Count = count;
            AllowDuplicates = allowDuplicates;
        }

        public static readonly TargetingRequirement None = default;

        public static TargetingRequirement RailCards(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                    "A rail-card requirement needs at least one target.");
            }

            return new TargetingRequirement(TargetKind.RailCard, count, allowDuplicates: false);
        }
    }
}
