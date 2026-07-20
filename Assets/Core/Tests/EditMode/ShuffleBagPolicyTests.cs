using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class ShuffleBagPolicyTests
    {
        private static CardDefinition Card(string id) => new CardDefinition(
            id, id, Side.Enemy, 5, new[] { new EffectData(EffectKeys.Damage, 1) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        private static IReadOnlyList<CardDefinition> Catalog(params string[] ids) =>
            ids.Select(Card).ToArray();

        private static string Signature(ShuffleBagPolicy policy, Random rng, int turns)
            => string.Join("|", Enumerable.Range(0, turns)
                .Select(t => string.Join(",", policy.CardsForTurn(t, rng).Select(c => c.Id))));

        [Test]
        public void Draws_each_card_once_before_reshuffling()
        {
            var policy = new ShuffleBagPolicy(Catalog("a", "b", "c", "d", "e", "f"), 2);
            var rng = new Random(11);
            var firstCycle = Enumerable.Range(0, 3)
                .SelectMany(t => policy.CardsForTurn(t, rng).Select(c => c.Id))
                .ToArray();

            CollectionAssert.AreEquivalent(new[] { "a", "b", "c", "d", "e", "f" }, firstCycle);
            Assert.AreEqual(6, firstCycle.Distinct().Count());
            Assert.AreEqual(2, policy.CardsForTurn(3, rng).Count);
        }

        [Test]
        public void Same_rng_seed_matches_and_different_seed_differs()
        {
            Assert.AreEqual(
                Signature(new ShuffleBagPolicy(Catalog("a", "b", "c", "d"), 2), new Random(7), 6),
                Signature(new ShuffleBagPolicy(Catalog("a", "b", "c", "d"), 2), new Random(7), 6));
            Assert.AreNotEqual(
                Signature(new ShuffleBagPolicy(Catalog("a", "b", "c", "d"), 2), new Random(7), 6),
                Signature(new ShuffleBagPolicy(Catalog("a", "b", "c", "d"), 2), new Random(8), 6));
        }

        [Test]
        public void Reshuffles_full_deck_when_remaining_cards_are_insufficient()
        {
            var policy = new ShuffleBagPolicy(Catalog("a", "b", "c", "d", "e"), 3);
            var rng = new Random(3);
            var first = policy.CardsForTurn(0, rng).Select(c => c.Id).ToArray();
            var second = policy.CardsForTurn(1, rng).Select(c => c.Id).ToArray();

            Assert.AreEqual(3, first.Length);
            Assert.AreEqual(3, second.Length);
            Assert.AreEqual(3, second.Distinct().Count());
        }

        [Test]
        public void Empty_or_zero_draw_yields_no_cards()
        {
            var rng = new Random(1);
            Assert.AreEqual(0, new ShuffleBagPolicy(Array.Empty<CardDefinition>(), 2).CardsForTurn(0, rng).Count);
            Assert.AreEqual(0, new ShuffleBagPolicy(Catalog("a", "b"), 0).CardsForTurn(0, rng).Count);
            Assert.AreEqual(0, new ShuffleBagPolicy(Catalog("a", "b"), -2).CardsForTurn(0, rng).Count);
        }
    }
}
