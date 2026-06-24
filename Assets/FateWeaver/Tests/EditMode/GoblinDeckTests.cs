using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class GoblinDeckTests
    {
        [Test]
        public void Starting_hp_is_twenty_eight()
        {
            Assert.AreEqual(28, GoblinDeck.StartingHp);
        }

        [Test]
        public void Defines_thrust_crude_guard_and_sly_jab()
        {
            var thrust = GoblinDeck.Thrust();
            var guard = GoblinDeck.CrudeGuard();
            var sly = GoblinDeck.SlyJab();

            Assert.AreEqual("goblin_jab", thrust.Id);
            Assert.AreEqual("찌르기", thrust.Name);
            Assert.AreEqual(6, thrust.BaseInitiative);
            Assert.AreEqual(4, thrust.Effects.Single().Amount);

            Assert.AreEqual("crude_guard", guard.Id);
            Assert.AreEqual("조잡한 방어", guard.Name);
            Assert.AreEqual(CardType.Defense, guard.Type);
            Assert.AreEqual(4, guard.BaseInitiative);
            Assert.AreEqual(3, guard.Effects.Single().Amount);
            Assert.AreEqual(StatusKeys.Block, guard.Effects.Single().StatusKey.Value);

            Assert.AreEqual("sly_jab", sly.Id);
            Assert.AreEqual("약삭빠른 찌르기", sly.Name);
            Assert.AreEqual(3, sly.BaseInitiative);
            Assert.AreEqual(3, sly.Effects.Single().Amount);
            Assert.AreEqual(6, sly.Effects.Single().SuccessAmount);
            var condition = (NoPrecedingCardOfSide)sly.Effects.Single().Condition;
            Assert.AreEqual(Side.Player, condition.Side);
        }

        [Test]
        public void All_cards_lists_every_catalog_card()
        {
            var ids = GoblinDeck.AllCards().Select(c => c.Id).ToArray();
            CollectionAssert.AreEquivalent(new[] { "goblin_jab", "crude_guard", "sly_jab" }, ids);
        }

        [Test]
        public void Policy_telegraphs_one_or_two_distinct_cards_per_turn()
        {
            var policy = GoblinDeck.Policy(seed: 17);
            for (int turn = 0; turn < 50; turn++)
            {
                var cards = policy.CardsForTurn(turn);
                Assert.That(cards.Count, Is.InRange(1, 2));
                Assert.AreEqual(cards.Count, cards.Select(c => c.Id).Distinct().Count());
            }
        }

        [Test]
        public void Policy_is_deterministic_by_seed()
        {
            string Signature(int seed)
            {
                var policy = GoblinDeck.Policy(seed);
                return string.Join("|", Enumerable.Range(0, 20).Select(turn =>
                    string.Join(",", policy.CardsForTurn(turn).Select(c => c.Id))));
            }

            Assert.AreEqual(Signature(7), Signature(7));
            Assert.AreNotEqual(Signature(7), Signature(8));
        }
    }
}
