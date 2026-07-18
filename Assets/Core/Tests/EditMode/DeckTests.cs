using System;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class DeckTests
    {
        private static CardDefinition Card(string id) => new CardDefinition(
            id, id, Side.Player, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) }) { EnergyCost = 1 };

        [Test]
        public void Draw_moves_cards_from_draw_pile_to_hand()
        {
            var deck = new Deck(new[] { Card("a"), Card("b"), Card("c") }, new Random(1));
            Assert.AreEqual(3, deck.DrawCount);
            Assert.AreEqual(0, deck.HandCount);

            deck.Draw(2);

            Assert.AreEqual(2, deck.HandCount);
            Assert.AreEqual(1, deck.DrawCount);
        }

        [Test]
        public void Draw_reshuffles_discard_when_draw_pile_empty()
        {
            var deck = new Deck(new[] { Card("a"), Card("b"), Card("c") }, new Random(1));
            deck.Draw(3);          // hand 3, draw 0
            deck.DiscardHand();    // discard 3, hand 0
            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(3, deck.DiscardCount);

            deck.Draw(2);          // must reshuffle the discard pile back in

            Assert.AreEqual(2, deck.HandCount);
            Assert.AreEqual(1, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void Draw_stops_when_no_cards_remain_anywhere()
        {
            var deck = new Deck(new[] { Card("a") }, new Random(1));
            deck.Draw(5); // only one card exists
            Assert.AreEqual(1, deck.HandCount);
        }
    }
}
