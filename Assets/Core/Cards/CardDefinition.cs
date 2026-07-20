using System.Collections.Generic;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Cards
{
    /// <summary>One effect entry on a card: which handler + its scalar effect value (M1).</summary>
    public sealed record EffectData(EffectKey Key, int EffectValue)
    {
        public Condition Condition { get; init; }
        public int? SuccessEffectValue { get; init; }

        /// <summary>Effect-kind-specific parameters (null when the scalar is enough).</summary>
        public IEffectPayload Payload { get; init; }

        // Position selector for enemy attacks against the player party formation. Null means
        // FrontMost (pre-party content has no selector, so this keeps single-enemy-attack compat).
        public TargetSelector? TargetSelector { get; init; }

        public static EffectData Conditional(
            EffectKey key,
            int effectValue,
            Condition condition,
            int successEffectValue)
            => new EffectData(key, effectValue)
            {
                Condition = condition,
                SuccessEffectValue = successEffectValue
            };

        public static EffectData ApplyStatus(
            StatusKey statusKey,
            StatusLifetime lifetime,
            StatusApplyTarget target,
            int magnitude = 0)
            => new EffectData(EffectKeys.ApplyStatus, magnitude)
            {
                Payload = new ApplyStatusPayload(statusKey, lifetime, target)
            };
    }

    /// <summary>Immutable card template.</summary>
    public sealed record CardDefinition(
        string Id,
        string Name,
        Side Side,
        int BaseExecutionOrder,
        IReadOnlyList<EffectData> Effects)
    {
        public bool HasEffect(EffectKey key)
        {
            if (string.IsNullOrEmpty(key.Id))
                throw new System.ArgumentException("Effect key must not be empty.", nameof(key));

            foreach (var effect in Effects)
            {
                if (effect.Key == key)
                    return true;
            }

            return false;
        }

        /// <summary>Energy cost to play this card.</summary>
        public int EnergyCost { get; init; }

        /// <summary>Execution (effects on the zone) or intervention (zone control).</summary>
        public CardCategory Category { get; init; }

        /// <summary>For intervention cards: the action resolved when played (null for execution cards).</summary>
        public InterventionActionData InterventionAction { get; init; }

        /// <summary>When true, the card enters the future zone locked (intervention reordering rejected).</summary>
        public bool StartsLocked { get; init; }
    }
}
