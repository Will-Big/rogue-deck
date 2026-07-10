using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Draw pile / discard pile / hand for one combat, with a seeded shuffle.
    /// Pure C# (no UnityEngine) so the loop is headless-testable and deterministic.</summary>
    public sealed class Deck
    {
        private readonly List<CardDefinition> _draw = new List<CardDefinition>();
        private readonly List<CardDefinition> _discard = new List<CardDefinition>();
        private readonly List<CardDefinition> _hand = new List<CardDefinition>();
        private readonly Random _rng;

        public Deck(IEnumerable<CardDefinition> cards, int seed)
        {
            _rng = new Random(seed);
            foreach (var card in cards)
            {
                _draw.Add(card);
            }

            Shuffle(_draw);
        }

        public IReadOnlyList<CardDefinition> Hand => _hand;
        public int DrawCount => _draw.Count;
        public int DiscardCount => _discard.Count;
        public int HandCount => _hand.Count;

        /// <summary>Read-only pile views for deck-viewer UI. Draw order is real — UI must sort for display.</summary>
        public IReadOnlyList<CardDefinition> DrawPile => _draw;
        public IReadOnlyList<CardDefinition> DiscardPile => _discard;

        public void Draw(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0)
                    {
                        return;
                    }

                    _draw.AddRange(_discard);
                    _discard.Clear();
                    Shuffle(_draw);
                }

                var top = _draw[_draw.Count - 1];
                _draw.RemoveAt(_draw.Count - 1);
                _hand.Add(top);
            }
        }

        public void DiscardFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count)
            {
                return;
            }

            _discard.Add(_hand[handIndex]);
            _hand.RemoveAt(handIndex);
        }

        public void DiscardHand()
        {
            _discard.AddRange(_hand);
            _hand.Clear();
        }

        private void Shuffle(List<CardDefinition> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
