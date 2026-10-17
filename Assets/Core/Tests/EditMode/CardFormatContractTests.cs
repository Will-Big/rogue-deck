using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>카드 JSON의 로딩 계약(전투 실행 계약 스펙 §4, 검증 V21). 잘못된 카드는 조용히 생략되거나
    /// 전투 중 취소되지 않고, 파일·카드·효과 경로가 담긴 로딩 오류가 된다. 카드 JSON은 형식 버전이 없다
    /// (효과 JSON 설계 P3).</summary>
    public class CardFormatContractTests
    {
        /// <summary>계획 T2a의 fixture. 게임 카드가 아니다.</summary>
        private const string PaymentFixture = @"{
  ""id"": ""payment_fixture"",
  ""name"": ""소비 보상 검증"",
  ""side"": ""Player"",
  ""category"": ""Execution"",
  ""energyCost"": 1,
  ""baseExecutionOrder"": 5,
  ""targets"": { ""enemy"": ""FrontOne"" },
  ""effects"": [
    { ""key"": ""consume_status"", ""id"": ""pay"", ""targetFaction"": ""Enemy"", ""effectValue"": 1,
      ""payload"": { ""key"": ""poison"", ""mode"": ""Exact"" } },
    { ""key"": ""grant_next_turn_fate"", ""id"": ""reward"", ""effectValue"": 1,
      ""requirement"": { ""sourceEffectId"": ""pay"", ""minimumConsumed"": 1 } }
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
                Mutate(card => Effect(card, 1)["requirement"]["sourceEffectId"] = "missing"),
                "effects[1] (id 'reward'): requirement refers to 'missing', which is not an earlier effect");

        [Test]
        public void Rejects_a_self_reference()
            => AssertRejected(
                Mutate(card => Effect(card, 1)["requirement"]["sourceEffectId"] = "reward"),
                "effects[1] (id 'reward'): requirement refers to itself");

        [Test]
        public void Rejects_a_reference_to_an_effect_that_consumes_nothing()
            => AssertRejected(
                Mutate(card =>
                {
                    ((JArray)card["effects"]).Insert(0, JObject.Parse(
                        @"{ ""key"": ""damage"", ""id"": ""hit"", ""targetFaction"": ""Enemy"", ""effectValue"": 1 }"));
                    Effect(card, 2)["requirement"]["sourceEffectId"] = "hit";
                }),
                "requirement refers to 'hit' (damage), which produces no consumed amount");

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
                "effects[0] (id 'reward'): requirement refers to 'pay', which is not an earlier effect");

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
    }
}
