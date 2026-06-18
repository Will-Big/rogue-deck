using System.Collections.Generic;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Cards
{
    /// <summary>One effect entry on a card: which handler + its scalar amount (M1).</summary>
    public sealed record EffectData(EffectKey Key, int Amount)
    {
        public Condition Condition { get; init; }
        public int? SuccessAmount { get; init; }

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
    }

    /// <summary>Immutable card template.</summary>
    public sealed record CardDefinition(
        string Id,
        string Name,
        Side Side,
        CardType Type,
        int BaseInitiative,
        IReadOnlyList<EffectData> Effects);
}
