using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Decorates another enemy policy by copying exactly one returned card each turn with
    /// StartsLocked set. The locked pick draws from the combat RNG passed to <see cref="CardsForTurn"/>.</summary>
    public sealed class SelfLockPolicy : IEnemyTurnPolicy
    {
        private readonly IEnemyTurnPolicy _inner;

        public SelfLockPolicy(IEnemyTurnPolicy inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
        {
            var source = _inner.CardsForTurn(turnIndex, rng);
            if (source == null || source.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            var cards = new CardDefinition[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                cards[i] = source[i];
            }

            int lockedIndex = rng.Next(cards.Length);
            cards[lockedIndex] = cards[lockedIndex] with { StartsLocked = true };
            return cards;
        }
    }
}
