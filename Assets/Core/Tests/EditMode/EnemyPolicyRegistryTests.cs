using System;
using System.Collections.Generic;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class EnemyPolicyRegistryTests
    {
        private static IReadOnlyList<EnemyCardBundle> OneBundle()
            => new[] { new EnemyCardBundle(CardFixtures.EnemyAttack("smash", 1, 3)) };

        [Test]
        public void Default_registry_creates_the_three_authored_policies()
        {
            var registry = CombatRegistries.EnemyPolicies();

            Assert.IsInstanceOf<RandomPickPolicy>(registry.Create(EnemyPolicyKeys.RandomPick, OneBundle()));
            Assert.IsInstanceOf<ShuffleBagPolicy>(registry.Create(EnemyPolicyKeys.ShuffleBag, OneBundle()));
            Assert.IsInstanceOf<SequencePolicy>(registry.Create(EnemyPolicyKeys.Sequence, OneBundle()));
            Assert.AreEqual("random_pick", EnemyPolicyKeys.RandomPick.Id);
            Assert.AreEqual("shuffle_bag", EnemyPolicyKeys.ShuffleBag.Id);
            Assert.AreEqual("sequence", EnemyPolicyKeys.Sequence.Id);
        }

        [Test]
        public void Create_returns_a_fresh_instance_every_call()
        {
            var registry = CombatRegistries.EnemyPolicies();
            var bundles = OneBundle();

            Assert.AreNotSame(
                registry.Create(EnemyPolicyKeys.ShuffleBag, bundles),
                registry.Create(EnemyPolicyKeys.ShuffleBag, bundles));
        }

        [Test]
        public void Unregistered_key_is_not_contained_and_cannot_be_created()
        {
            var registry = CombatRegistries.EnemyPolicies();
            var unknown = new EnemyPolicyKey("random_pik");

            Assert.IsFalse(registry.Contains(unknown));
            Assert.IsTrue(registry.Contains(new EnemyPolicyKey("random_pick")), "키는 값으로 비교한다.");
            Assert.Throws<KeyNotFoundException>(() => registry.Create(unknown, OneBundle()));
        }
    }
}
