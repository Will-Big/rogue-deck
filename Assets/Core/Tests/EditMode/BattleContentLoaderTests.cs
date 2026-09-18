using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Battles;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class BattleContentLoaderTests
    {
        private static EnemyContentCatalog Enemies()
            => new EnemyContentCatalog(new Dictionary<string, EnemyDefinition>
            {
                { "goblin", new EnemyDefinition("goblin", "G", 28, EnemyPolicyKeys.RandomPick, new EnemyCardBundle[0]) },
                { "rat", new EnemyDefinition("rat", "R", 10, EnemyPolicyKeys.RandomPick, new EnemyCardBundle[0]) }
            });

        private static BattleContentLoadResult Load(params CardContentSource[] sources)
            => BattleContentLoader.Load(sources, Enemies());

        private static CardContentSource Source(string name, string json) => new CardContentSource(name, json);

        [Test]
        public void Loads_battles_sorted_by_id()
        {
            var result = Load(
                Source("rat_single.json", "{ \"id\": \"rat_single\", \"enemies\": [\"rat\"] }"),
                Source("goblin_single.json", "{ \"id\": \"goblin_single\", \"enemies\": [\"goblin\"] }"));

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            CollectionAssert.AreEqual(new[] { "goblin_single", "rat_single" }, result.Catalog.Ids);
            CollectionAssert.AreEqual(new[] { "rat" }, result.Catalog.Get("rat_single").Enemies);
        }

        [Test]
        public void Requires_at_least_one_battle()
        {
            var result = Load();

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "Battles: at least one battle is required.");
        }

        [TestCase("{ \"enemies\": [\"goblin\"] }", "b.json: required key 'id' is missing.")]
        [TestCase("{ \"id\": \"b\" }", "b.json: required key 'enemies' is missing.")]
        [TestCase("{ \"id\": \"\", \"enemies\": [\"goblin\"] }", "b.json: required key 'id' must be a non-empty string.")]
        [TestCase("{ \"id\": \"b\", \"enemies\": [\"ghost\"] }", "b.json: unknown enemy id 'ghost'.")]
        [TestCase("{ \"id\": \"b\", \"enemies\": [] }", "b.json: exactly one enemy is supported until per-enemy policies land.")]
        [TestCase("{ \"id\": \"b\", \"enemies\": [\"goblin\", \"goblin\"] }", "b.json: exactly one enemy is supported until per-enemy policies land.")]
        public void Rejects_an_invalid_battle(string json, string error)
        {
            var result = Load(Source("b.json", json));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            CollectionAssert.Contains(result.Errors, error);
        }

        [Test]
        public void Rejects_a_duplicate_id()
        {
            var json = "{ \"id\": \"b\", \"enemies\": [\"goblin\"] }";
            var result = Load(Source("a.json", json), Source("c.json", json));

            CollectionAssert.Contains(result.Errors, "c.json: duplicate battle id 'b' (already defined in a.json).");
        }
    }
}
