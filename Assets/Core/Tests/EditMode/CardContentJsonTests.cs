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

            StringAssert.DoesNotContain("energyCost", json);
            StringAssert.DoesNotContain("baseExecutionOrder", json);
            StringAssert.DoesNotContain("effects", json);
            StringAssert.DoesNotContain("grade", json);
            StringAssert.DoesNotContain("tags", json);

            // side·category는 0번 열거값이어도 Include 처방 때문에 반드시 남는다. 이 둘이 생략되면
            // CardContentLoader의 필수 키 검사가 "키가 있다"로 통과해 카드가 조용히 기본값을 갖는다.
            StringAssert.Contains("\"side\": \"Player\"", json);
            StringAssert.Contains("\"category\": \"Execution\"", json);
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
        public void RoundTripsEffectCommonFields()
        {
            var original = new ApplyStatusSpec
            {
                Id = "venom",
                TargetFaction = CardTargetFaction.Enemy,
                EffectValue = 2,
                SuccessEffectValue = 5,
                SkipOnBasic = true,
                Requirement = new EffectRequirementSpec { SourceEffectId = "pay", MinimumConsumed = 2 },
                Scaling = new EffectScalingSpec { SourceEffectId = "pay", PerConsumed = 3 },
                Payload = new ApplyStatusPayloadSpec { Key = new StatusKeyRef { Id = "poison" } }
            };

            var restored = (ApplyStatusSpec)ContentJson.Read<EffectSpec>(ContentJson.Write(original));

            Assert.AreEqual("venom", restored.Id);
            Assert.AreEqual(CardTargetFaction.Enemy, restored.TargetFaction);
            Assert.AreEqual("poison", restored.Payload.Key.Id);
            Assert.AreEqual(2, restored.EffectValue);
            Assert.AreEqual(5, restored.SuccessEffectValue);
            Assert.IsTrue(restored.SkipOnBasic);
            Assert.AreEqual("pay", restored.Requirement.SourceEffectId);
            Assert.AreEqual(2, restored.Requirement.MinimumConsumed);
            Assert.AreEqual("pay", restored.Scaling.SourceEffectId);
            Assert.AreEqual(3, restored.Scaling.PerConsumed);
        }

        [Test]
        public void WritesTheEffectIdAndFactionRightAfterTheKey()
        {
            var json = ContentJson.Write(new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, EffectValue = 4 });

            var key = json.IndexOf("\"key\"", System.StringComparison.Ordinal);
            var id = json.IndexOf("\"id\"", System.StringComparison.Ordinal);
            var faction = json.IndexOf("\"targetFaction\"", System.StringComparison.Ordinal);
            var value = json.IndexOf("\"effectValue\"", System.StringComparison.Ordinal);
            Assert.That(key < id && id < faction && faction < value, json);
        }

        [Test]
        public void ConsumptionModeIsAlwaysWritten()
        {
            var json = ContentJson.Write(new ConsumeStatusSpec
            {
                Id = "pay", TargetFaction = CardTargetFaction.Enemy, EffectValue = 1,
                Payload = new ConsumeStatusPayloadSpec { Key = new StatusKeyRef { Id = "poison" }, Mode = ConsumptionMode.UpTo }
            });

            StringAssert.Contains("\"mode\": \"UpTo\"", json);
        }

        [Test]
        public void RejectsUnknownEffectKeyByName()
        {
            var ex = Assert.Throws<JsonSerializationException>(
                () => ContentJson.Read<EffectSpec>("{ \"key\": \"dmage\", \"effectValue\": 5 }"));

            StringAssert.Contains("dmage", ex.Message);
        }

        [Test]
        public void EveryCatalogEntryHasADistinctKey()
        {
            var keys = EffectSpecCatalog.All().Select(info => info.Create().Key.Id).ToList();

            CollectionAssert.AllItemsAreUnique(keys);
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
                Targets = new CardTargetsSpec { Ally = CardTargetRange.Self, Enemy = CardTargetRange.FrontOne },
                StartCondition = new StartConditionSpec { Kind = ConditionKind.WithinNth, N = 2 },
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, EffectValue = 4 },
                    new ApplyStatusSpec
                    {
                        Id = "guard",
                        TargetFaction = CardTargetFaction.Ally,
                        EffectValue = 1,
                        Payload = new ApplyStatusPayloadSpec { Key = new StatusKeyRef { Id = "block" } }
                    }
                }
            };

            var restored = (ExecutionCardSpec)ContentJson.Read<CardSpec>(ContentJson.Write(original));

            Assert.AreEqual("probing_strike", restored.Id);
            Assert.AreEqual("견제타", restored.Name);
            Assert.AreEqual(4, restored.BaseExecutionOrder);
            Assert.AreEqual(2, restored.Effects.Length);
            Assert.IsInstanceOf<DamageSpec>(restored.Effects[0]);
            Assert.AreEqual(4, ((DamageSpec)restored.Effects[0]).EffectValue);
            Assert.IsInstanceOf<ApplyStatusSpec>(restored.Effects[1]);
            Assert.AreEqual("block", ((ApplyStatusSpec)restored.Effects[1]).Payload.Key.Id);
            Assert.AreEqual(CardTargetRange.Self, restored.Targets.Ally);
            Assert.AreEqual(CardTargetRange.FrontOne, restored.Targets.Enemy);
            Assert.AreEqual(ConditionKind.WithinNth, restored.StartCondition.Kind);
            Assert.AreEqual(2, restored.StartCondition.N);
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
                Targets = new CardTargetsSpec { Enemy = CardTargetRange.FrontOne },
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Id = "hit", TargetFaction = CardTargetFaction.Enemy, EffectValue = 5 }
                }
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
            Assert.AreEqual(before.Effects[0], after.Effects[0]);
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

        // V22(게임 쪽): 저장소 카드 전부가 새 형식으로 읽고-쓰고-다시 읽어도 같은 정규 직렬화를 낸다. 의미 보존은 이것만으로
        // 대신하지 않는다 — 로딩 검증 테스트가 따로 있다.
        [Test]
        public void Every_card_preserves_its_canonical_serialized_definition()
        {
            var paths = System.IO.Directory.GetFiles(System.IO.Path.Combine(TestContent.Root(), "Cards"), "*.json");
            Assert.IsNotEmpty(paths);
            foreach (var path in paths)
            {
                var first = ContentJson.Read<CardSpec>(System.IO.File.ReadAllText(path));
                var canonical = ContentJson.Write(first);
                var second = ContentJson.Read<CardSpec>(canonical);
                Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(
                    Newtonsoft.Json.Linq.JToken.Parse(canonical),
                    Newtonsoft.Json.Linq.JToken.Parse(ContentJson.Write(second))), path);
            }
        }
    }
}
