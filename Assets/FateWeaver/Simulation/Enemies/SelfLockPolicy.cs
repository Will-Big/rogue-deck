using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Decorates another enemy policy by copying exactly one returned card each turn with
    /// StartsLocked set.</summary>
    public sealed class SelfLockPolicy : IEnemyTurnPolicy
    {
        private readonly IEnemyTurnPolicy _inner;
        private readonly Random _rng;

        public SelfLockPolicy(IEnemyTurnPolicy inner, int seed)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _rng = new Random(seed);
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex)
        {
            var source = _inner.CardsForTurn(turnIndex);
            if (source == null || source.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            var cards = new CardDefinition[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                cards[i] = source[i];
            }

            int lockedIndex = _rng.Next(cards.Length);
            cards[lockedIndex] = cards[lockedIndex] with { StartsLocked = true };
            return cards;
        }
    }
}
