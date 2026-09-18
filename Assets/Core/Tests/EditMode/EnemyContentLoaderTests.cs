using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Enemies;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>적 로더가 저작 실수를 로드 시점에 거부하는지 잠근다. 다른 로더와 같이 실패하면
    /// 카탈로그를 내주지 않고 모든 이유를 모은다.</summary>
    public class EnemyContentLoaderTests
    {
        private static CardContentCatalog Cards()
        {
            var cards = new Dictionary<string, CardDefinition>();
            var specs = new Dictionary<string, CardSpec>();
            void Add(string id, Side side)
            {
                var spec = new ExecutionCardSpec { Id = id, Name = id, Side = side, Category = CardCategory.Execution };
                cards.Add(id, CardSpecMapper.ToDefinition(spec));
                specs.Add(id, spec);
            }

            Add("jab", Side.Enemy);
            Add("guard", Side.Enemy);
            Add("hero_strike", Side.Player);
            return new CardContentCatalog(cards, specs);
        }

        private static EnemyContentLoadResult Load(params CardContentSource[] sources)
            => EnemyContentLoader.Load(sources, Cards(), AuthoringContext.Default());

        private static CardContentSource Source(string name, string json) => new CardContentSource(name, json);

        private const string Valid =
            "{ \"id\": \"goblin\", \"displayName\": \"고블린\", \"maxHp\": 28, \"policy\": \"random_pick\", "
            + "\"bundles\": [[\"jab\"], [\"jab\", \"guard\"]] }";

        [Test]
        public void Loads_an_enemy_with_shared_card_definitions_and_policy_key()
        {
            var result = Load(Source("goblin.json", Valid));

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            var goblin = result.Catalog.Get("goblin");
            Assert.AreEqual("고블린", goblin.DisplayName);
            Assert.AreEqual(28, goblin.MaxHp);
            Assert.AreEqual(EnemyPolicyKeys.RandomPick, goblin.Policy);
            CollectionAssert.AreEqual(
                new[] { "jab", "jab,guard" },
                goblin.Bundles.Select(b => string.Join(",", b.Cards.Select(c => c.Id))).ToArray());
            Assert.AreSame(goblin.Bundles[0].Cards[0], goblin.Bundles[1].Cards[0], "같은 카드 id는 정의 하나를 공유한다.");
            CollectionAssert.AreEqual(new[] { "goblin" }, result.Catalog.Ids);
            Assert.IsTrue(result.Catalog.Contains("goblin"));
        }

        [TestCase("id")]
        [TestCase("displayName")]
        [TestCase("maxHp")]
        [TestCase("policy")]
        [TestCase("bundles")]
        public void Rejects_a_missing_required_key(string key)
        {
            var json = key switch
            {
                "id" => "{ \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
                "displayName" => "{ \"id\": \"goblin\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
                "maxHp" => "{ \"id\": \"goblin\", \"displayName\": \"G\", \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
                "policy" => "{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"bundles\": [[\"jab\"]] }",
                _ => "{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\" }"
            };

            var result = Load(Source("goblin.json", json));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "goblin.json: required key '" + key + "' is missing.");
        }

        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: requires a displayName.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 0, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: maxHp must be positive.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pik\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: unknown enemy policy 'random_pik'.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [] }",
            "goblin.json: requires at least one bundle.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"], []] }",
            "goblin.json: bundle 1 is empty.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"ghost\"]] }",
            "goblin.json: unknown card id 'ghost' in bundle 0.")]
        [TestCase("{ \"id\": \"goblin\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"hero_strike\"]] }",
            "goblin.json: card 'hero_strike' in bundle 0 is not an enemy card.")]
        [TestCase("{ \"id\": \"\", \"displayName\": \"G\", \"maxHp\": 28, \"policy\": \"random_pick\", \"bundles\": [[\"jab\"]] }",
            "goblin.json: required key 'id' must be a non-empty string.")]
        public void Rejects_an_invalid_enemy(string json, string error)
        {
            var result = Load(Source("goblin.json", json));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            CollectionAssert.Contains(result.Errors, error);
        }

        [Test]
        public void Rejects_a_duplicate_id()
        {
            var result = Load(Source("a.json", Valid), Source("b.json", Valid));

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "b.json: duplicate enemy id 'goblin' (already defined in a.json).");
        }

        [Test]
        public void Reports_every_reason_at_once()
        {
            var result = Load(Source("goblin.json",
                "{ \"id\": \"goblin\", \"displayName\": \"\", \"maxHp\": 0, \"policy\": \"random_pik\", \"bundles\": [[\"ghost\"]] }"));

            Assert.AreEqual(4, result.Errors.Count, string.Join("\n", result.Errors));
        }
    }
}
