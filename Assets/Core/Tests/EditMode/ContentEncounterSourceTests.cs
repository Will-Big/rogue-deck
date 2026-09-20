using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Battles;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Enemies;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class ContentEncounterSourceTests
    {
        private static ContentEncounterSource RepositorySource()
            => new ContentEncounterSource(TestContent.Content(), CombatRegistries.EnemyPolicies());

        [Test]
        public void Repository_goblin_single_gives_one_goblin_with_combat_and_spec_ids()
        {
            var setup = RepositorySource().For("goblin_single");

            Assert.AreEqual(1, setup.Enemies.Count);
            var pair = setup.Enemies[0];
            Assert.AreEqual("goblin#0", pair.Enemy.Id);
            Assert.AreEqual("goblin", pair.Enemy.SpecId);
            Assert.AreEqual(28, pair.Enemy.Hp);
            Assert.IsInstanceOf<ShuffleBagPolicy>(pair.Policy);
        }

        [Test]
        public void Repository_goblin_pair_gives_two_goblins_with_separate_combat_ids()
        {
            var setup = RepositorySource().For("goblin_pair");

            CollectionAssert.AreEqual(
                new[] { "goblin#0", "goblin#1" }, setup.Enemies.Select(e => e.Enemy.Id).ToArray());
            Assert.IsTrue(setup.Enemies.All(e => e.Enemy.SpecId == "goblin"));
            Assert.AreNotSame(
                setup.Enemies[0].Policy, setup.Enemies[1].Policy,
                "같은 적이라도 정책 인스턴스는 따로다 — 가방 상태를 공유하면 안 된다.");
        }

        [Test]
        public void Every_pick_makes_fresh_enemies_and_policies()
        {
            var source = RepositorySource();

            var first = source.Pick(new Random(1)).Enemies[0];
            var second = source.Pick(new Random(1)).Enemies[0];

            Assert.AreNotSame(first.Enemy, second.Enemy);
            Assert.AreNotSame(first.Policy, second.Policy);
        }

        /// <summary>편성 후보 둘인 합성 콘텐츠. 쓰지 않는 카탈로그는 null이다.</summary>
        private static ContentEncounterSource TwoBattles()
        {
            var enemies = new EnemyContentCatalog(new Dictionary<string, EnemyDefinition>
            {
                { "goblin", new EnemyDefinition("goblin", "G", 28, EnemyPolicyKeys.Sequence, new EnemyCardBundle[0]) },
                { "rat", new EnemyDefinition("rat", "R", 10, EnemyPolicyKeys.Sequence, new EnemyCardBundle[0]) }
            });
            var battles = new BattleContentCatalog(new Dictionary<string, BattleDefinition>
            {
                { "goblin_single", new BattleDefinition("goblin_single", new[] { "goblin" }) },
                { "rat_single", new BattleDefinition("rat_single", new[] { "rat" }) }
            });
            var content = new GameContent(null, null, null, null, null, enemies, battles, null);
            return new ContentEncounterSource(content, CombatRegistries.EnemyPolicies());
        }

        private static string SpecAt(ContentEncounterSource source, int runSeed, int nodeIndex)
            => source.Pick(new Random(SeedDerivation.Stream(
                SeedDerivation.NodeSeed(runSeed, nodeIndex), SeedStream.Encounter))).Enemies[0].Enemy.SpecId;

        [Test]
        public void Same_node_seed_picks_the_same_battle()
        {
            var source = TwoBattles();

            for (int node = 0; node < 10; node++)
            {
                Assert.AreEqual(SpecAt(source, 7, node), SpecAt(source, 7, node));
            }
        }

        [Test]
        public void Different_nodes_reach_both_battles()
        {
            var source = TwoBattles();

            var picked = Enumerable.Range(0, 20).Select(node => SpecAt(source, 7, node)).Distinct().ToArray();

            CollectionAssert.AreEquivalent(new[] { "goblin", "rat" }, picked);
        }
    }
}
