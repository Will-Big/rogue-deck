using System;
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
            Assert.AreEqual(6, thrust.BaseExecutionOrder);
            Assert.AreEqual(4, thrust.Effects.Single().EffectValue);

            Assert.AreEqual("crude_guard", guard.Id);
            Assert.AreEqual("조잡한 방어", guard.Name);
            Assert.AreEqual(4, guard.BaseExecutionOrder);
            Assert.AreEqual(3, guard.Effects.Single().EffectValue);
            Assert.AreEqual(StatusKeys.Block, ((ApplyStatusPayload)guard.Effects.Single().Payload).Key);

            Assert.AreEqual("sly_jab", sly.Id);
            Assert.AreEqual("약삭빠른 찌르기", sly.Name);
            Assert.AreEqual(3, sly.BaseExecutionOrder);
            Assert.AreEqual(3, sly.Effects.Single().EffectValue);
            Assert.AreEqual(6, sly.Effects.Single().SuccessEffectValue);
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
        public void Bundles_are_the_four_authored_combinations()
        {
            var bundles = GoblinDeck.Bundles()
                .Select(b => string.Join(",", b.Cards.Select(c => c.Id)))
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "goblin_jab",                 // A 늦은 단타
                    "sly_jab,crude_guard",        // B 선공 후 방어
                    "sly_jab,goblin_jab",         // C 앞뒤로 벌린 2연타
                    "crude_guard,crude_guard"     // D 농성 (방어 재부여는 합산 → 방어도 6)
                },
                bundles);
        }

        [Test]
        public void Policy_deploys_exactly_one_authored_bundle_each_turn()
        {
            var authored = GoblinDeck.Bundles()
                .Select(b => string.Join(",", b.Cards.Select(c => c.Id)))
                .ToArray();
            var policy = GoblinDeck.Policy();
            var rng = new Random(17);

            for (int turn = 0; turn < 50; turn++)
            {
                var deployed = string.Join(",", policy.CardsForTurn(turn, rng).Select(c => c.Id));
                CollectionAssert.Contains(authored, deployed,
                    "저작되지 않은 조합이 나오면 안 된다");
            }
        }

        [Test]
        public void Every_bundle_is_reachable()
        {
            var policy = GoblinDeck.Policy();
            var rng = new Random(17);
            var seen = Enumerable.Range(0, 200)
                .Select(turn => string.Join(",", policy.CardsForTurn(turn, rng).Select(c => c.Id)))
                .Distinct()
                .ToArray();

            Assert.AreEqual(GoblinDeck.Bundles().Count, seen.Length,
                "닿을 수 없는 묶음이 있으면 저작이 죽은 것이다");
        }

        [Test]
        public void Policy_is_deterministic_by_seed()
        {
            string Signature(int seed)
            {
                var policy = GoblinDeck.Policy();
                var rng = new Random(seed);
                return string.Join("|", Enumerable.Range(0, 20).Select(turn =>
                    string.Join(",", policy.CardsForTurn(turn, rng).Select(c => c.Id))));
            }

            Assert.AreEqual(Signature(7), Signature(7));
            Assert.AreNotEqual(Signature(7), Signature(8));
        }
    }
}
