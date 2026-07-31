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
            var json = ContentJson.Write(new CardSpec
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
            var json = ContentJson.Write(new CardSpec { Id = "x", Name = "x" });

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
                Value = 3,
                Lifetime = StatusLifetimeKind.Turns,
                LifetimeCount = 2,
                Target = StatusApplyTarget.TargetEnemy,
                Selector = TargetSelectorRef.BackMost,
                Condition = new ConditionSpec
                {
                    Kind = ConditionKind.WithinNth, N = 2, SuccessEffectValue = 5, SkipOnBasic = true
                }
            };

            var restored = (ApplyStatusSpec)ContentJson.Read<EffectSpec>(ContentJson.Write(original));

            Assert.AreEqual("poison", restored.Status.Id);
            Assert.AreEqual(3, restored.Value);
            Assert.AreEqual(StatusLifetimeKind.Turns, restored.Lifetime);
            Assert.AreEqual(2, restored.LifetimeCount);
            Assert.AreEqual(StatusApplyTarget.TargetEnemy, restored.Target);
            Assert.AreEqual(TargetSelectorRef.BackMost, restored.Selector);
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
            var original = new CardSpec
            {
                Id = "probing_strike",
                Name = "견제타",
                Side = Side.Player,
                Category = CardCategory.Execution,
                EnergyCost = 1,
                BaseExecutionOrder = 4,
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Value = 4, Selector = TargetSelectorRef.FrontMost },
                    new ApplyStatusSpec
                    {
                        Status = new StatusKeyRef { Id = "block" },
                        Value = 1,
                        Lifetime = StatusLifetimeKind.ThisTurn,
                        Target = StatusApplyTarget.Self
                    }
                }
            };

            var restored = ContentJson.Read<CardSpec>(ContentJson.Write(original));

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
            var original = new CardSpec
            {
                Id = "hasten",
                Name = "재촉",
                Side = Side.Player,
                Category = CardCategory.Intervention,
                EnergyCost = 1,
                Intervention = new InterventionKeyRef { Id = "change_execution_order" },
                InterventionEffectValue = -2,
                InterventionTargetSide = InterventionTargetSideRef.Player,
                InterventionRequireAdjacent = true
            };

            var restored = ContentJson.Read<CardSpec>(ContentJson.Write(original));

            Assert.AreEqual("change_execution_order", restored.Intervention.Id);
            Assert.AreEqual(-2, restored.InterventionEffectValue);
            Assert.AreEqual(InterventionTargetSideRef.Player, restored.InterventionTargetSide);
            Assert.IsTrue(restored.InterventionRequireAdjacent);
        }

        [Test]
        public void RoundTrippedCardProducesAnIdenticalDefinition()
        {
            var original = new CardSpec
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
            Assert.AreEqual(before.Effects.Count, after.Effects.Count);
            Assert.AreEqual(before.Effects[0].Key, after.Effects[0].Key);
            Assert.AreEqual(before.Effects[0].EffectValue, after.Effects[0].EffectValue);
        }
    }
}
