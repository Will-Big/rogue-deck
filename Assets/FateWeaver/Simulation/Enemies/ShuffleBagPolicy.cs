using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Stateful no-replacement enemy policy. When fewer than the draw count remains, it discards
    /// the partial remainder and starts a freshly shuffled full deck.</summary>
    public sealed class ShuffleBagPolicy : IEnemyTurnPolicy
    {
        private readonly IReadOnlyList<CardDefinition> _deck;
        private readonly int _drawPerTurn;
        private readonly Random _rng;
        private List<CardDefinition> _bag = new List<CardDefinition>();

        public ShuffleBagPolicy(IReadOnlyList<CardDefinition> deck, int drawPerTurn, int seed)
        {
            _deck = deck ?? Array.Empty<CardDefinition>();
            _drawPerTurn = Math.Max(0, drawPerTurn);
            _rng = new Random(seed);
        }

        public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex)
        {
            if (_deck.Count == 0 || _drawPerTurn == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            if (_bag.Count < _drawPerTurn)
            {
                _bag = ShuffledDeck();
            }

            var count = Math.Min(_drawPerTurn, _bag.Count);
            var drawn = _bag.GetRange(0, count);
            _bag.RemoveRange(0, count);
            return drawn;
        }

        private List<CardDefinition> ShuffledDeck()
        {
            var cards = new List<CardDefinition>(_deck);
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = cards[i];
                cards[i] = cards[j];
                cards[j] = tmp;
            }

            return cards;
        }
    }
}
