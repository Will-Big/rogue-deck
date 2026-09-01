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

        /// <summary>id 문자열 하나가 묶음 하나다 — "ab"는 카드 a와 b를 함께 내는 묶음.</summary>
        private static IReadOnlyList<EnemyCardBundle> Bundles(params string[] specs) =>
            specs
                .Select(spec => new EnemyCardBundle(
                    spec.Select(ch => Card(ch.ToString())).ToArray()))
                .ToArray();

        private static string Signature(ShuffleBagPolicy policy, Random rng, int turns)
            => string.Join("|", Enumerable.Range(0, turns)
                .Select(t => string.Join(",", policy.CardsForTurn(t, rng).Select(c => c.Id))));

        private static string PickAt(ShuffleBagPolicy policy, int turn, Random rng)
            => string.Join("", policy.CardsForTurn(turn, rng).Select(c => c.Id));

        [Test]
        public void Draws_each_bundle_once_before_reshuffling()
        {
            var policy = new ShuffleBagPolicy(Bundles("a", "bc", "d", "ef"));
            var rng = new Random(11);
            var firstCycle = Enumerable.Range(0, 4).Select(t => PickAt(policy, t, rng)).ToArray();

            CollectionAssert.AreEquivalent(new[] { "a", "bc", "d", "ef" }, firstCycle);
        }

        [Test]
        public void Reshuffles_a_full_bag_once_the_previous_one_is_spent()
        {
            var policy = new ShuffleBagPolicy(Bundles("a", "bc", "d"));
            var rng = new Random(3);
            var twoCycles = Enumerable.Range(0, 6).Select(t => PickAt(policy, t, rng)).ToArray();

            CollectionAssert.AreEquivalent(new[] { "a", "bc", "d" }, twoCycles.Take(3).ToArray());
            CollectionAssert.AreEquivalent(new[] { "a", "bc", "d" }, twoCycles.Skip(3).ToArray());
        }

        [Test]
        public void Each_turn_still_deploys_exactly_one_bundle()
        {
            var policy = new ShuffleBagPolicy(Bundles("a", "bc", "def"));
            var rng = new Random(5);
            var sizes = Enumerable.Range(0, 9)
                .Select(t => policy.CardsForTurn(t, rng).Count)
                .Distinct()
                .OrderBy(n => n)
                .ToArray();

            // 카드 수는 묶음 크기를 따라 1·2·3이 나오지만, 매 턴 나오는 묶음은 언제나 하나다.
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, sizes);
        }

        [Test]
        public void Same_rng_seed_matches_and_different_seed_differs()
        {
            Assert.AreEqual(
                Signature(new ShuffleBagPolicy(Bundles("a", "bc", "d", "ef")), new Random(7), 6),
                Signature(new ShuffleBagPolicy(Bundles("a", "bc", "d", "ef")), new Random(7), 6));
            Assert.AreNotEqual(
                Signature(new ShuffleBagPolicy(Bundles("a", "bc", "d", "ef")), new Random(7), 6),
                Signature(new ShuffleBagPolicy(Bundles("a", "bc", "d", "ef")), new Random(8), 6));
        }

        [Test]
        public void Empty_bundle_list_yields_no_cards()
        {
            Assert.AreEqual(
                0,
                new ShuffleBagPolicy(Array.Empty<EnemyCardBundle>()).CardsForTurn(0, new Random(1)).Count);
        }
    }
}
