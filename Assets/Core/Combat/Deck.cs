using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Draw pile / discard pile / hand for one combat. Shuffles draw from the injected combat RNG
    /// (CombatState.Rng — AGENTS.md rule 7), so the deck never owns its own randomness.
    /// Pure C# (no UnityEngine) so the loop is headless-testable and deterministic.</summary>
    public sealed class Deck
    {
        private readonly List<OwnedCard> _draw = new List<OwnedCard>();
        private readonly List<OwnedCard> _discard = new List<OwnedCard>();
        private readonly List<OwnedCard> _hand = new List<OwnedCard>();
        private readonly Random _rng;
        private readonly ReadOnlyCollection<OwnedCard> _drawView;
        private readonly ReadOnlyCollection<OwnedCard> _discardView;

        public Deck(IEnumerable<CardDefinition> cards, Random rng)
            : this(WithLegacyOwner(cards), rng)
        {
        }

        public Deck(IEnumerable<OwnedCard> cards, Random rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            foreach (var card in cards)
            {
                _draw.Add(card);
            }

            Shuffle(_draw);
            _drawView = _draw.AsReadOnly();
            _discardView = _discard.AsReadOnly();
        }

        public IReadOnlyList<OwnedCard> Hand => _hand;
        public int DrawCount => _draw.Count;
        public int DiscardCount => _discard.Count;
        public int HandCount => _hand.Count;

        /// <summary>Read-only pile views for deck-viewer UI. Draw order is real — UI must sort for display.</summary>
        public IReadOnlyList<OwnedCard> DrawPile => _drawView;
        public IReadOnlyList<OwnedCard> DiscardPile => _discardView;

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

        public void RemoveOwnedBy(string ownerId)
        {
            _draw.RemoveAll(card => card.OwnerId != null && card.OwnerId == ownerId);
            _discard.RemoveAll(card => card.OwnerId != null && card.OwnerId == ownerId);
            _hand.RemoveAll(card => card.OwnerId != null && card.OwnerId == ownerId);
        }

        private static IEnumerable<OwnedCard> WithLegacyOwner(IEnumerable<CardDefinition> cards)
        {
            foreach (var card in cards)
            {
                yield return new OwnedCard(card, CombatState.SoloPlayerId);
            }
        }

        private void Shuffle(List<OwnedCard> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
