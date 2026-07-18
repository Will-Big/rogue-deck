using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Stateful no-replacement enemy policy. When fewer than the draw count remains, it discards
    /// the partial remainder and starts a freshly shuffled full deck. Shuffles draw from the combat RNG
    /// passed to <see cref="CardsForTurn"/>.</summary>
    public sealed class ShuffleBagPolicy : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<CardDefinition> _deck;
        private readonly int _drawPerTurn;
        private List<CardDefinition> _bag = new List<CardDefinition>();

        public ShuffleBagPolicy(IReadOnlyList<CardDefinition> deck, int drawPerTurn)
        {
            _deck = deck ?? Array.Empty<CardDefinition>();
            _drawPerTurn = Math.Max(0, drawPerTurn);
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
        {
            if (_deck.Count == 0 || _drawPerTurn == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            if (_bag.Count < _drawPerTurn)
            {
                _bag = ShuffledDeck(rng);
            }

            var count = Math.Min(_drawPerTurn, _bag.Count);
            var drawn = _bag.GetRange(0, count);
            _bag.RemoveRange(0, count);
            return drawn;
        }

        private List<CardDefinition> ShuffledDeck(Random rng)
        {
            var cards = new List<CardDefinition>(_deck);
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = cards[i];
                cards[i] = cards[j];
                cards[j] = tmp;
            }

            return cards;
        }
    }
}
