using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CardContentJsonTests
    {
        [Test]
        public void WritesEnumsAsNamesAndCamelCaseKeys()
        {
            var json = ContentJson.Write(new ExecutionCardSpec
            {
                Id = "slash",
                Name = "베기",
                Side = Side.Enemy,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 4
            });

            StringAssert.Contains("\"id\": \"slash\"", json);
            StringAssert.Contains("\"side\": \"Enemy\"", json);
        }

        [Test]
        public void OmitsDefaultValuedMembers()
        {
            var json = ContentJson.Write(new ExecutionCardSpec { Id = "x", Name = "x" });

            StringAssert.DoesNotContain("interventionEffectValue", json);
        }

        [Test]
        public void RoundTripsEveryRegisteredEffectSpecKind()
        {
            foreach (var info in EffectSpecCatalog.All())
            {
                var original = info.Create();
                var json = ContentJson.Write(original);
                var restored = ContentJson.Read<EffectSpec>(json);

                Assert.AreEqual(info.SpecType, restored.GetType(), info.DisplayName);
                Assert.AreEqual(original.Key, restored.Key, info.DisplayName);
            }
        }

        [Test]
        public void RoundTripsSpecParametersAndCondition()
        {
            var original = new ApplyStatusSpec
            {
                Status = new StatusKeyRef { Id = "poison" },
                Count = 2,
                Target = StatusApplyTarget.TargetEnemy,
                Selector = TargetSelectorRef.BackOne,
                Condition = new ConditionSpec
                {
                    Kind = ConditionKind.WithinNth, N = 2, SuccessEffectValue = 5, SkipOnBasic = true
                }
            };

            var restored = (ApplyStatusSpec)ContentJson.Read<EffectSpec>(ContentJson.Write(original));

            Assert.AreEqual("poison", restored.Status.Id);
            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual(StatusApplyTarget.TargetEnemy, restored.Target);
            Assert.AreEqual(TargetSelectorRef.BackOne, restored.Selector);
            Assert.AreEqual(ConditionKind.WithinNth, restored.Condition.Kind);
            Assert.AreEqual(2, restored.Condition.N);
            Assert.AreEqual(5, restored.Condition.SuccessEffectValue);
            Assert.IsTrue(restored.Condition.SkipOnBasic);
        }

        [Test]
        public void RejectsUnknownEffectKindByName()
        {
            var ex = Assert.Throws<JsonSerializationException>(
                () => ContentJson.Read<EffectSpec>("{ \"kind\": \"dmage\", \"value\": 5 }"));

            StringAssert.Contains("dmage", ex.Message);
        }

        [Test]
        public void EveryCatalogEntryHasADistinctKind()
        {
            var kinds = EffectSpecCatalog.All().Select(info => info.Create().Key.Id).ToList();

            CollectionAssert.AllItemsAreUnique(kinds);
        }

        [Test]
        public void RoundTripsAnExecutionCardWithMultipleEffects()
        {
            var original = new ExecutionCardSpec
            {
                Id = "probing_strike",
                Name = "견제타",
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 4,
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Value = 4, Selector = TargetSelectorRef.FrontOne },
                    new ApplyStatusSpec
                    {
                        Status = new StatusKeyRef { Id = "block" },
                        Count = 1,
                        Target = StatusApplyTarget.Self
                    }
                }
            };

            var restored = (ExecutionCardSpec)ContentJson.Read<CardSpec>(ContentJson.Write(original));

            Assert.AreEqual("probing_strike", restored.Id);
            Assert.AreEqual("견제타", restored.Name);
            Assert.AreEqual(4, restored.BaseExecutionOrder);
            Assert.AreEqual(2, restored.Effects.Length);
            Assert.IsInstanceOf<DamageSpec>(restored.Effects[0]);
            Assert.AreEqual(4, ((DamageSpec)restored.Effects[0]).Value);
            Assert.IsInstanceOf<ApplyStatusSpec>(restored.Effects[1]);
            Assert.AreEqual("block", ((ApplyStatusSpec)restored.Effects[1]).Status.Id);
        }

        [Test]
        public void RoundTripsAnInterventionCardIncludingTargetRestrictions()
        {
            var original = new InterventionCardSpec
            {
                Id = "hasten",
                Name = "재촉",
                Side = Side.Player,
                Category = CardCategory.Intervention,
                EnergyCost = 1,
                Intervention = new SwapExecutionOrderSpec
                {
                    TargetSide = InterventionTargetSideRef.Player,
                    RequireAdjacent = true
                }
            };

            var restored = (InterventionCardSpec)ContentJson.Read<CardSpec>(ContentJson.Write(original));
            var swap = (SwapExecutionOrderSpec)restored.Intervention;

            Assert.AreEqual(InterventionTargetSideRef.Player, swap.TargetSide);
            Assert.IsTrue(swap.RequireAdjacent);
        }

        [Test]
        public void RoundTrippedCardProducesAnIdenticalDefinition()
        {
            var original = new ExecutionCardSpec
            {
                Id = "delayed_strike",
                Name = "늦춘 일격",
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 5,
                Effects = new EffectSpec[] { new DamageSpec { Value = 5 } }
            };

            var before = CardSpecMapper.ToDefinition(original);
            var after = CardSpecMapper.ToDefinition(
                ContentJson.Read<CardSpec>(ContentJson.Write(original)));

            Assert.AreEqual(before.Id, after.Id);
            Assert.AreEqual(before.Name, after.Name);
            Assert.AreEqual(before.BaseExecutionOrder, after.BaseExecutionOrder);
            Assert.AreEqual(before.EnergyCost, after.EnergyCost);
            Assert.AreEqual(before.Category, after.Category);
            Assert.AreEqual(before.Effects.Count, after.Effects.Count);
            Assert.AreEqual(before.Effects[0].Key, after.Effects[0].Key);
            Assert.AreEqual(before.Effects[0].EffectValue, after.Effects[0].EffectValue);
        }

        [Test]
        public void Category_picks_the_concrete_card_spec_type()
        {
            var execution = ContentJson.Read<CardSpec>(
                "{\"id\":\"a\",\"name\":\"a\",\"side\":\"Player\",\"category\":\"Execution\"}");
            var intervention = ContentJson.Read<CardSpec>(
                "{\"id\":\"b\",\"name\":\"b\",\"side\":\"Player\",\"category\":\"Intervention\"}");

            Assert.IsInstanceOf<ExecutionCardSpec>(execution);
            Assert.IsInstanceOf<InterventionCardSpec>(intervention);
        }

        [Test]
        public void Execution_card_rejects_intervention_keys()
        {
            Assert.Throws<JsonSerializationException>(() => ContentJson.Read<CardSpec>(
                "{\"id\":\"a\",\"name\":\"a\",\"side\":\"Player\",\"category\":\"Execution\","
                + "\"intervention\":\"lock\"}"));
        }

        [Test]
        public void Intervention_kind_picks_the_concrete_intervention_spec()
        {
            var spec = (InterventionCardSpec)ContentJson.Read<CardSpec>(
                "{\"id\":\"d\",\"name\":\"d\",\"side\":\"Player\",\"category\":\"Intervention\","
                + "\"energyCost\":1,\"intervention\":{\"kind\":\"change_execution_order\","
                + "\"delta\":1,\"targetSide\":\"Enemy\"}}");

            var change = (ChangeExecutionOrderSpec)spec.Intervention;
            Assert.AreEqual(1, change.Delta);
            Assert.AreEqual(InterventionTargetSideRef.Enemy, change.TargetSide);
        }

        [Test]
        public void Swap_spec_rejects_a_parameter_it_does_not_own()
        {
            Assert.Throws<JsonSerializationException>(() => ContentJson.Read<CardSpec>(
                "{\"id\":\"c\",\"name\":\"c\",\"side\":\"Player\",\"category\":\"Intervention\","
                + "\"energyCost\":1,\"intervention\":{\"kind\":\"swap_execution_order\","
                + "\"delta\":1}}"));
        }

        [Test]
        public void Repository_cards_round_trip_byte_identically()
        {
            var directory = System.IO.Path.Combine(TestContent.Root(), "Cards");

            foreach (var path in System.IO.Directory.GetFiles(directory, "*.json"))
            {
                var original = System.IO.File.ReadAllText(path);
                var rewritten = ContentJson.Write(ContentJson.Read<CardSpec>(original));

                Assert.AreEqual(
                    Normalize(original), Normalize(rewritten),
                    System.IO.Path.GetFileName(path) + "의 왕복이 원본과 다르다.");
            }
        }

        /// <summary>줄바꿈과 파일 끝 공백만 맞춘다. 키 순서·들여쓰기·값은 그대로 비교한다 —
        /// 그것이 이 테스트가 잠그려는 것이기 때문이다.</summary>
        private static string Normalize(string json)
            => json.Replace("\r\n", "\n").TrimEnd();
    }
}
