using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Rules;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CombatRulesLoaderTests
    {
        private const string Valid =
            "{ \"fateEnergyPerTurn\": 3, \"minPartySize\": 1, \"maxPartySize\": 3, "
            + "\"drawByLivingCount\": { \"1\": 3, \"2\": 4, \"3\": 5 }, \"rewardChoices\": 3 }";

        private static CombatRulesLoadResult Load(string json)
            => CombatRulesLoader.Load(new CardContentSource("combat_rules.json", json));

        [Test]
        public void Loads_rules_with_party_tuning_inside()
        {
            var result = Load(Valid);

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            Assert.AreEqual(3, result.Rules.FateEnergyPerTurn);
            Assert.AreEqual(3, result.Rules.RewardChoices);
            Assert.AreEqual(1, result.Rules.Party.MinPartySize);
            Assert.AreEqual(3, result.Rules.Party.MaxPartySize);
            Assert.AreEqual(3, result.Rules.Party.DrawFor(1));
            Assert.AreEqual(4, result.Rules.Party.DrawFor(2));
            Assert.AreEqual(5, result.Rules.Party.DrawFor(3));
        }

        [Test]
        public void Missing_file_is_an_error()
        {
            var result = CombatRulesLoader.Load(null);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "combat_rules.json: file is missing.");
        }

        [TestCase("fateEnergyPerTurn")]
        [TestCase("minPartySize")]
        [TestCase("maxPartySize")]
        [TestCase("drawByLivingCount")]
        [TestCase("rewardChoices")]
        public void Rejects_a_missing_required_key(string key)
        {
            var json = Valid.Replace("\"" + key + "\"", "\"removed_" + key + "\"");

            var result = Load(json);

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "combat_rules.json: required key '" + key + "' is missing.");
        }

        [TestCase("\"fateEnergyPerTurn\": 3", "\"fateEnergyPerTurn\": 0", "combat_rules.json: fateEnergyPerTurn must be positive.")]
        [TestCase("\"minPartySize\": 1", "\"minPartySize\": 0", "combat_rules.json: minPartySize must be at least 1.")]
        [TestCase("\"minPartySize\": 1", "\"minPartySize\": 4", "combat_rules.json: minPartySize must not exceed maxPartySize.")]
        [TestCase("\"rewardChoices\": 3", "\"rewardChoices\": 0", "combat_rules.json: rewardChoices must be positive.")]
        [TestCase("\"3\": 5", "\"3\": 0", "combat_rules.json: drawByLivingCount must give a positive draw for living count 3.")]
        [TestCase(", \"3\": 5", "", "combat_rules.json: drawByLivingCount must give a positive draw for living count 3.")]
        public void Rejects_invalid_values(string from, string to, string error)
        {
            var result = Load(Valid.Replace(from, to));

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Rules);
            CollectionAssert.Contains(result.Errors, error);
        }
    }
}
