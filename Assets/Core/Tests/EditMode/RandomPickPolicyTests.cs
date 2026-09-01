using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class RandomPickPolicyTests
    {
        private static CardDefinition Card(string id) => new CardDefinition(
            id, id, Side.Enemy, 5, new[] { new EffectData(EffectKeys.Damage, 1) });

        /// <summary>id 문자열 하나가 묶음 하나다 — "ab"는 카드 a와 b를 함께 내는 묶음.</summary>
        private static IReadOnlyList<EnemyCardBundle> Bundles(params string[] specs) =>
            specs
                .Select(spec => new EnemyCardBundle(
                    spec.Select(ch => Card(ch.ToString())).ToArray()))
                .ToArray();

        private static string Sig(IEnemyTurnPolicy policy, Random rng, int turns) =>
            string.Join("|", Enumerable.Range(0, turns)
                .Select(t => string.Join(",", policy.CardsForTurn(t, rng).Select(c => c.Id))));

        [Test]
        public void Every_turn_deploys_exactly_one_authored_bundle()
        {
            var authored = new[] { "a", "bc", "bd" };
            var policy = new RandomPickPolicy(Bundles(authored));
            var rng = new Random(5);

            for (int turn = 0; turn < 100; turn++)
            {
                var ids = string.Join("", policy.CardsForTurn(turn, rng).Select(c => c.Id));
                CollectionAssert.Contains(authored, ids,
                    "저작되지 않은 조합이 나오면 안 된다 — 묶음 통째로만 전개된다");
            }
        }

        [Test]
        public void A_single_card_bundle_deploys_that_one_card()
        {
            var policy = new RandomPickPolicy(Bundles("a"));
            var cards = policy.CardsForTurn(0, new Random(1));

            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual("a", cards.Single().Id);
        }

        [Test]
        public void A_bundle_may_repeat_the_same_card()
        {
            var policy = new RandomPickPolicy(Bundles("aa"));
            var ids = policy.CardsForTurn(0, new Random(1)).Select(c => c.Id).ToArray();

            CollectionAssert.AreEqual(new[] { "a", "a" }, ids);
        }

        [Test]
        public void Which_bundle_actually_varies_across_turns()
        {
            var policy = new RandomPickPolicy(Bundles("a", "bc", "bd"));
            var rng = new Random(5);
            var seen = Enumerable.Range(0, 100)
                .Select(t => string.Join("", policy.CardsForTurn(t, rng).Select(c => c.Id)))
                .Distinct()
                .ToList();

            Assert.That(seen.Count, Is.GreaterThan(1), "묶음 선택이 고정되면 안 된다");
        }

        [Test]
        public void Draw_is_with_replacement_so_the_same_bundle_can_repeat()
        {
            var policy = new RandomPickPolicy(Bundles("a", "b"));
            var rng = new Random(3);
            var picks = Enumerable.Range(0, 60)
                .Select(t => policy.CardsForTurn(t, rng).Single().Id)
                .ToArray();

            var repeated = picks.Zip(picks.Skip(1), (x, y) => x == y).Any(same => same);
            Assert.IsTrue(repeated, "복원 추출이므로 연속으로 같은 묶음이 나올 수 있어야 한다");
        }

        [Test]
        public void Same_rng_seed_matches_and_different_seed_differs()
        {
            Assert.AreEqual(
                Sig(new RandomPickPolicy(Bundles("a", "bc", "bd")), new Random(7), 20),
                Sig(new RandomPickPolicy(Bundles("a", "bc", "bd")), new Random(7), 20));
            Assert.AreNotEqual(
                Sig(new RandomPickPolicy(Bundles("a", "bc", "bd")), new Random(7), 20),
                Sig(new RandomPickPolicy(Bundles("a", "bc", "bd")), new Random(8), 20));
        }

        [Test]
        public void Consumes_exactly_one_rng_draw_per_turn()
        {
            // 같은 시드의 두 RNG를 두고, 한쪽은 정책이 3턴 쓰고 다른 쪽은 손으로 3번 굴린다.
            // 이후 두 RNG의 다음 값이 같으면 소비량이 턴당 정확히 1이다.
            var policyRng = new Random(11);
            var manualRng = new Random(11);
            var policy = new RandomPickPolicy(Bundles("a", "bc", "bd"));

            for (int turn = 0; turn < 3; turn++)
            {
                policy.CardsForTurn(turn, policyRng);
                manualRng.Next(3);
            }

            Assert.AreEqual(manualRng.Next(1000), policyRng.Next(1000));
        }

        [Test]
        public void Empty_bundle_list_yields_no_cards()
        {
            var policy = new RandomPickPolicy(Array.Empty<EnemyCardBundle>());
            Assert.AreEqual(0, policy.CardsForTurn(0, new Random(1)).Count);
        }
    }
}
