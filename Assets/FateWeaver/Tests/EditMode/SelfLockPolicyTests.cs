using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class SelfLockPolicyTests
    {
        private static CardDefinition Card(string id) => new CardDefinition(
            id, id, Side.Enemy, CardType.Attack, 5, new[] { new EffectData(EffectKeys.Damage, 1) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        private static EnemyIntent Intent(params IReadOnlyList<CardDefinition>[] turns) => new EnemyIntent(turns);

        private static string Signature(SelfLockPolicy policy, int turns)
            => string.Join("|", Enumerable.Range(0, turns)
                .Select(t => string.Join(",", policy.CardsForTurn(t).Select(c => c.StartsLocked ? c.Id + "*" : c.Id))));

        [Test]
        public void Locks_exactly_one_card_and_preserves_original_definitions()
        {
            var original = new[] { Card("a"), Card("b"), Card("c") };
            var policy = new SelfLockPolicy(Intent(original), seed: 5);

            var cards = policy.CardsForTurn(0);

            Assert.AreEqual(1, cards.Count(c => c.StartsLocked));
            Assert.AreEqual(2, cards.Count(c => !c.StartsLocked));
            Assert.AreEqual(0, original.Count(c => c.StartsLocked));
        }

        [Test]
        public void Same_seed_matches_and_different_seed_differs()
        {
            IReadOnlyList<CardDefinition>[] Turns()
            {
                return new IReadOnlyList<CardDefinition>[]
                {
                    new[] { Card("a"), Card("b"), Card("c") },
                    new[] { Card("d"), Card("e"), Card("f") },
                    new[] { Card("g"), Card("h"), Card("i") }
                };
            }

            Assert.AreEqual(
                Signature(new SelfLockPolicy(Intent(Turns()), seed: 7), 3),
                Signature(new SelfLockPolicy(Intent(Turns()), seed: 7), 3));
            Assert.AreNotEqual(
                Signature(new SelfLockPolicy(Intent(Turns()), seed: 7), 3),
                Signature(new SelfLockPolicy(Intent(Turns()), seed: 8), 3));
        }

        [Test]
        public void Empty_inner_result_returns_empty()
        {
            var policy = new SelfLockPolicy(Intent(new CardDefinition[0]), seed: 1);
            Assert.AreEqual(0, policy.CardsForTurn(0).Count);
        }
    }
}
