using System;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class EncounterSourceTests
    {
        [Test]
        public void Enemy_keeps_combat_id_and_spec_id_apart()
        {
            var enemy = new Enemy("goblin#0", "goblin", 28);

            Assert.AreEqual("goblin#0", enemy.Id);
            Assert.AreEqual("goblin", enemy.SpecId);
            Assert.AreEqual(28, enemy.Hp);
        }

        [Test]
        public void Legacy_enemy_constructor_uses_the_id_as_spec_id()
        {
            Assert.AreEqual("goblin", new Enemy("goblin", 28).SpecId);
        }

        [Test]
        public void Goblin_source_returns_one_goblin_with_its_own_policy()
        {
            var setup = new GoblinEncounterSource().Pick(new Random(1));

            Assert.AreEqual(1, setup.Enemies.Count);
            var pair = setup.Enemies[0];
            Assert.AreEqual("goblin#0", pair.Enemy.Id);
            Assert.AreEqual(GoblinDeck.EnemyId, pair.Enemy.SpecId);
            Assert.AreEqual(GoblinDeck.StartingHp, pair.Enemy.Hp);
            Assert.IsInstanceOf<RandomPickPolicy>(pair.Policy);
        }

        [Test]
        public void Goblin_source_makes_fresh_instances_every_pick()
        {
            var source = new GoblinEncounterSource();

            var first = source.Pick(new Random(1)).Enemies[0];
            var second = source.Pick(new Random(1)).Enemies[0];

            Assert.AreNotSame(first.Enemy, second.Enemy);
            Assert.AreNotSame(first.Policy, second.Policy);
        }
    }
}
