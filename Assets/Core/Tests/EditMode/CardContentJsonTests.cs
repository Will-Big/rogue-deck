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
    }
}
