using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>카드 JSON 형식 2의 로딩 계약(전투 실행 계약 스펙 §4, 검증 V21)과 구형(형식 1) 변환 규칙
    /// (계획 T2a 변환 규칙 표). 잘못된 카드는 조용히 생략되거나 전투 중 취소되지 않고, 파일·카드·효과 경로가
    /// 담긴 로딩 오류가 된다.</summary>
    public class CardFormatContractTests
    {
        /// <summary>계획 T2a의 fixture. 게임 카드가 아니다.</summary>
        private const string PaymentFixture = @"{
  ""cardFormat"": 2,
  ""id"": ""payment_fixture"",
  ""name"": ""소비 보상 검증"",
  ""side"": ""Player"",
  ""category"": ""Execution"",
  ""energyCost"": 1,
  ""baseExecutionOrder"": 5,
  ""targets"": { ""enemy"": ""FrontOne"" },
  ""effects"": [
    { ""kind"": ""consume_status"", ""id"": ""pay"", ""targetFaction"": ""Enemy"",
      ""status"": ""poison"", ""amount"": 1, ""mode"": ""Exact"" },
    { ""kind"": ""grant_next_turn_fate"", ""id"": ""reward"", ""value"": 1,
      ""requires"": { ""sourceEffectId"": ""pay"", ""minimumConsumed"": 1 } }
  ]
}";

        private static CardContentLoadResult Load(string json, string file = "fixture.json")
            => CardContentLoader.Load(new[] { new CardContentSource(file, json) }, AuthoringContext.Default());

        private static string Mutate(System.Action<JObject> change)
        {
            var card = JObject.Parse(PaymentFixture);
            change(card);
            return card.ToString();
        }

        private static JObject Effect(JObject card, int index) => (JObject)card["effects"][index];

        [Test]
        public void The_fixture_loads_with_its_result_reference()
        {
            var result = Load(PaymentFixture);

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            var reward = result.Catalog.Get("payment_fixture").Effects[1];
            Assert.AreEqual(new EffectResultRequirement("pay", 1), reward.Requirement);
            var pay = (ConsumeStatusPayload)result.Catalog.Get("payment_fixture").Effects[0].Payload;
            Assert.AreEqual(ConsumptionMode.Exact, pay.Mode);
        }

        private static void AssertRejected(string json, string expected)
        {
            var result = Load(json);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Catalog);
            Assert.That(result.Errors, Has.Some.Contains("fixture.json"));
            Assert.That(result.Errors, Has.Some.Contains("Card 'payment_fixture'"));
            Assert.That(result.Errors, Has.Some.Contains(expected), string.Join("\n", result.Errors));
        }

        [Test]
        public void Rejects_a_reference_to_a_missing_effect()
            => AssertRejected(
                Mutate(card => Effect(card, 1)["requires"]["sourceEffectId"] = "missing"),
                "effects[1] (id 'reward'): requires refers to 'missing', which is not an earlier effect");

        [Test]
        public void Rejects_a_self_reference()
            => AssertRejected(
                Mutate(card => Effect(card, 1)["requires"]["sourceEffectId"] = "reward"),
                "effects[1] (id 'reward'): requires refers to itself");

        [Test]
        public void Rejects_a_reference_to_an_effect_that_consumes_nothing()
            => AssertRejected(
                Mutate(card =>
                {
                    ((JArray)card["effects"]).Insert(0, JObject.Parse(
                        @"{ ""kind"": ""damage"", ""id"": ""hit"", ""targetFaction"": ""Enemy"", ""value"": 1 }"));
                    Effect(card, 2)["requires"]["sourceEffectId"] = "hit";
                }),
                "requires refers to 'hit' (damage), which produces no consumed amount");

        [Test]
        public void Rejects_a_reference_to_a_later_effect()
            => AssertRejected(
                Mutate(card =>
                {
                    var effects = (JArray)card["effects"];
                    var reward = effects[1];
                    effects.RemoveAt(1);
                    effects.Insert(0, reward);
                }),
                "effects[0] (id 'reward'): requires refers to 'pay', which is not an earlier effect");

        [Test]
        public void Rejects_a_duplicate_effect_id()
            => AssertRejected(
                Mutate(card => Effect(card, 1)["id"] = "pay"),
                "effects[1] (id 'pay'): duplicate effect id 'pay'");

        [Test]
        public void Rejects_a_targeted_effect_without_its_card_axis()
            => AssertRejected(
                Mutate(card => card.Remove("targets")),
                "effects[0] (id 'pay'): targetFaction Enemy needs targets.enemy on the card");

        [Test]
        public void Rejects_a_faction_on_an_effect_without_a_target()
            => AssertRejected(
                Mutate(card => Effect(card, 1)["targetFaction"] = "Ally"),
                "grant_next_turn_fate has no target, so targetFaction must be omitted");

        [Test]
        public void Rejects_a_success_value_without_a_start_condition()
            => AssertRejected(
                Mutate(card => Effect(card, 1)["successEffectValue"] = 3),
                "successEffectValue needs a card startCondition");

        [Test]
        public void Rejects_an_unused_card_axis()
            => AssertRejected(
                Mutate(card => card["targets"]["ally"] = "Self"),
                "targets.ally is declared but no effect targets Ally");

        [Test]
        public void Rejects_an_unsupported_card_format()
        {
            var result = Load(Mutate(card => card["cardFormat"] = 3));

            Assert.IsFalse(result.Succeeded);
            Assert.That(result.Errors, Has.Some.Contains("fixture.json"));
            Assert.That(result.Errors, Has.Some.Contains("Unsupported card format '3'"));
        }

        // --- 구형(형식 1) 변환 ---------------------------------------------------------

        private const string LegacyBurst = @"{
  ""id"": ""legacy_burst"", ""name"": ""구형 파열"", ""side"": ""Player"", ""category"": ""Execution"",
  ""energyCost"": 2, ""baseExecutionOrder"": 6,
  ""effects"": [
    { ""kind"": ""consume_status"", ""status"": ""poison"", ""maxAmount"": 3, ""damageBonusPerConsumed"": 2, ""selector"": ""FrontOne"" },
    { ""kind"": ""damage"", ""value"": 2, ""selector"": ""FrontOne"" },
    { ""kind"": ""apply_status"", ""status"": ""block"", ""count"": 4,
      ""condition"": { ""kind"": ""ConsumedStatusAtLeast"", ""n"": 1, ""successEffectValue"": 5, ""skipOnBasic"": true } }
  ]
}";

        [Test]
        public void Legacy_card_upgrades_by_rule()
        {
            var entry = JObject.Parse(LegacyBurst);

            CardFormatMigration.Upgrade(entry);

            Assert.AreEqual(2, (int)entry["cardFormat"]);
            Assert.AreEqual("FrontOne", (string)entry["targets"]["enemy"]);
            Assert.AreEqual("Self", (string)entry["targets"]["ally"]);
            var effects = (JArray)entry["effects"];
            CollectionAssert.AreEqual(new[] { "e0", "e1", "e2" }, effects.Select(e => (string)e["id"]).ToArray());
            Assert.AreEqual(3, (int)effects[0]["amount"]);
            Assert.AreEqual("UpTo", (string)effects[0]["mode"]);
            Assert.IsNull(effects[0]["maxAmount"]);
            Assert.IsNull(effects[0]["damageBonusPerConsumed"]);
            Assert.IsNull(effects[0]["selector"]);
            Assert.AreEqual("e0", (string)effects[1]["scaleBy"]["sourceEffectId"]);
            Assert.AreEqual(2, (int)effects[1]["scaleBy"]["perConsumed"]);
            Assert.AreEqual("Ally", (string)effects[2]["targetFaction"]);
            Assert.AreEqual("e0", (string)effects[2]["requires"]["sourceEffectId"]);
            Assert.AreEqual(5, (int)effects[2]["count"]);
            Assert.IsNull(entry["startCondition"]);
        }

        [Test]
        public void Legacy_card_loads_to_the_same_definition_as_its_upgraded_form()
        {
            var upgraded = JObject.Parse(LegacyBurst);
            CardFormatMigration.Upgrade(upgraded);

            var fromLegacy = Load(LegacyBurst, "legacy.json");
            var fromUpgraded = Load(upgraded.ToString(), "upgraded.json");

            Assert.IsTrue(fromLegacy.Succeeded, string.Join("\n", fromLegacy.Errors));
            Assert.IsTrue(fromUpgraded.Succeeded, string.Join("\n", fromUpgraded.Errors));
            CollectionAssert.AreEqual(
                fromUpgraded.Catalog.Get("legacy_burst").Effects,
                fromLegacy.Catalog.Get("legacy_burst").Effects);
        }

        [Test]
        public void Legacy_card_with_different_conditions_is_rejected_not_merged()
        {
            var result = Load(@"{
  ""id"": ""two_conditions"", ""name"": ""두 조건"", ""side"": ""Player"", ""category"": ""Execution"",
  ""effects"": [
    { ""kind"": ""damage"", ""value"": 1, ""condition"": { ""kind"": ""FirstToTrigger"", ""successEffectValue"": 3 } },
    { ""kind"": ""damage"", ""value"": 1, ""condition"": { ""kind"": ""WithinNth"", ""n"": 2, ""successEffectValue"": 3 } }
  ]
}", "legacy.json");

            Assert.IsFalse(result.Succeeded);
            Assert.That(result.Errors, Has.Some.Contains("legacy.json"));
            Assert.That(result.Errors, Has.Some.Contains("card 'two_conditions' effects[1]"));
            Assert.That(result.Errors, Has.Some.Contains("different conditions"));
        }

        [Test]
        public void Legacy_explicit_party_member_target_is_rejected()
        {
            var result = Load(@"{
  ""id"": ""pick_member"", ""name"": ""지목"", ""side"": ""Player"", ""category"": ""Execution"",
  ""effects"": [ { ""kind"": ""apply_status"", ""status"": ""block"", ""count"": 2, ""target"": ""PartyMember"" } ]
}", "legacy.json");

            Assert.IsFalse(result.Succeeded);
            Assert.That(result.Errors, Has.Some.Contains("apply_status target 'PartyMember' has no position equivalent"));
        }

        [Test]
        public void Legacy_enemy_card_keeps_its_own_self_and_attacks_the_party()
        {
            var entry = JObject.Parse(@"{
  ""id"": ""guard_jab"", ""name"": ""guard_jab"", ""side"": ""Enemy"", ""category"": ""Execution"",
  ""effects"": [ { ""kind"": ""damage"", ""value"": 3 }, { ""kind"": ""apply_status"", ""status"": ""block"", ""count"": 2 } ]
}");

            CardFormatMigration.Upgrade(entry);

            Assert.AreEqual("FrontOne", (string)entry["targets"]["ally"]);
            Assert.AreEqual("Self", (string)entry["targets"]["enemy"]);
            Assert.AreEqual("Ally", (string)entry["effects"][0]["targetFaction"]);
            Assert.AreEqual("Enemy", (string)entry["effects"][1]["targetFaction"]);
        }
    }
}
