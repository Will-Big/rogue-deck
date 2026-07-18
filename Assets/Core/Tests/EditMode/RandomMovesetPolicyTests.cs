using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class RandomMovesetPolicyTests
    {
        private static CardDefinition Card(string id) => new CardDefinition(
            id, id, Side.Enemy, CardType.Attack, 5, new[] { new EffectData(EffectKeys.Damage, 1) });

        private static IReadOnlyList<CardDefinition> Catalog(params string[] ids) =>
            ids.Select(Card).ToArray();

        [Test]
        public void Count_stays_within_min_and_max_and_cards_are_distinct()
        {
            var policy = new RandomMovesetPolicy(Catalog("a", "b", "c", "d"), minCards: 1, maxCards: 3);
            var rng = new Random(5);
            for (int turn = 0; turn < 100; turn++)
            {
                var cards = policy.CardsForTurn(turn, rng);
                Assert.That(cards.Count, Is.InRange(1, 3));
                Assert.AreEqual(cards.Count, cards.Select(c => c.Id).Distinct().Count());
            }
        }

        [Test]
        public void Count_actually_varies_across_turns()
        {
            var policy = new RandomMovesetPolicy(Catalog("a", "b", "c"), minCards: 1, maxCards: 3);
            var rng = new Random(5);
            var counts = Enumerable.Range(0, 100).Select(t => policy.CardsForTurn(t, rng).Count).Distinct().ToList();
            Assert.That(counts.Count, Is.GreaterThan(1), "draw count should not be fixed across turns");
        }

        [Test]
        public void Same_rng_seed_matches_and_different_seed_differs()
        {
            string Sig(int seed)
            {
                var policy = new RandomMovesetPolicy(Catalog("a", "b", "c"), 1, 3);
                var rng = new Random(seed);
                return string.Join("|", Enumerable.Range(0, 20)
                    .Select(t => string.Join(",", policy.CardsForTurn(t, rng).Select(c => c.Id))));
            }

            Assert.AreEqual(Sig(7), Sig(7));
            Assert.AreNotEqual(Sig(7), Sig(8));
        }

        [Test]
        public void Count_is_capped_by_catalog_size()
        {
            var policy = new RandomMovesetPolicy(Catalog("only"), minCards: 1, maxCards: 5);
            var rng = new Random(3);
            for (int turn = 0; turn < 20; turn++)
            {
                Assert.AreEqual(1, policy.CardsForTurn(turn, rng).Count);
            }
        }

        [Test]
        public void Fixed_count_when_min_equals_max()
        {
            var policy = new RandomMovesetPolicy(Catalog("a", "b", "c"), minCards: 2, maxCards: 2);
            var rng = new Random(1);
            for (int turn = 0; turn < 20; turn++)
            {
                Assert.AreEqual(2, policy.CardsForTurn(turn, rng).Count);
            }
        }

        [Test]
        public void Empty_catalog_yields_no_cards()
        {
            var policy = new RandomMovesetPolicy(Array.Empty<CardDefinition>(), 1, 3);
            Assert.AreEqual(0, policy.CardsForTurn(0, new Random(1)).Count);
        }

        [Test]
        public void Enemy_intent_satisfies_the_policy_seam()
        {
            IEnemyTurnPolicy policy = new EnemyIntent(new IReadOnlyList<CardDefinition>[]
            {
                new[] { Card("t0") },
                new[] { Card("t1a"), Card("t1b") }
            });

            var rng = new Random(1);
            Assert.AreEqual("t0", policy.CardsForTurn(0, rng).Single().Id);
            CollectionAssert.AreEqual(new[] { "t1a", "t1b" }, policy.CardsForTurn(1, rng).Select(c => c.Id).ToArray());
            Assert.AreEqual("t1a", policy.CardsForTurn(9, rng)[0].Id); // clamps past the end
        }
    }
}
