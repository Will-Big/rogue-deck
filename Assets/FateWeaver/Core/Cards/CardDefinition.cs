using System.Collections.Generic;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Cards
{
    /// <summary>One effect entry on a card: which handler + its scalar amount (M1).</summary>
    public sealed record EffectData(EffectKey Key, int Amount)
    {
        public Condition Condition { get; init; }
        public int? SuccessAmount { get; init; }

        // Status application (read by the ApplyStatus effect handler). Magnitude rides on Amount.
        public StatusKey? StatusKey { get; init; }
        public StatusLifetime? StatusLifetime { get; init; }
        public StatusApplyTarget StatusTarget { get; init; }

        public static EffectData Conditional(
            EffectKey key,
            int amount,
            Condition condition,
            int successAmount)
            => new EffectData(key, amount)
            {
                Condition = condition,
                SuccessAmount = successAmount
            };

        public static EffectData ApplyStatus(
            StatusKey statusKey,
            StatusLifetime lifetime,
            StatusApplyTarget target,
            int magnitude = 0)
            => new EffectData(EffectKeys.ApplyStatus, magnitude)
            {
                StatusKey = statusKey,
                StatusLifetime = lifetime,
                StatusTarget = target
            };
    }

    /// <summary>Immutable card template.</summary>
    public sealed record CardDefinition(
        string Id,
        string Name,
        Side Side,
        CardType Type,
        int BaseInitiative,
        IReadOnlyList<EffectData> Effects)
    {
        /// <summary>Energy cost to play this card.</summary>
        public int Cost { get; init; }

        /// <summary>Execution (effects on the zone) or intervention (zone control).</summary>
        public CardCategory Category { get; init; }

        /// <summary>For intervention cards: the action resolved when played (null for execution cards).</summary>
        public InterventionActionData InterventionAction { get; init; }

        /// <summary>When true, the card enters the future zone locked (intervention reordering rejected).</summary>
        public bool StartsLocked { get; init; }
    }
}
