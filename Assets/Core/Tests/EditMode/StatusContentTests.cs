using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class StatusContentTests
    {
        [Test]
        public void RoundTripsEveryRegisteredStatusSpecKind()
        {
            foreach (var info in StatusSpecCatalog.All())
            {
                var original = info.Create();
                var restored = ContentJson.Read<StatusSpec>(ContentJson.Write(original));

                Assert.AreEqual(info.SpecType, restored.GetType(), info.Key.Id);
            }
        }

        [Test]
        public void RoundTripsPoisonGrowth()
        {
            var original = new PoisonStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Poison),
                Lifetime = StatusLifetimeKind.Permanent,
                GrowthPerTurn = 2
            };

            var restored = (PoisonStatusSpec)ContentJson.Read<StatusSpec>(ContentJson.Write(original));

            Assert.AreEqual("poison", restored.Key.Id);
            Assert.AreEqual(StatusLifetimeKind.Permanent, restored.Lifetime);
            Assert.AreEqual(2, restored.GrowthPerTurn);
        }

        [Test]
        public void MultiplierSpecBecomesAStatusRule()
        {
            var spec = new MultiplierStatusSpec
            {
                Key = StatusKeyRef.Of(StatusKeys.Vulnerable),
                Lifetime = StatusLifetimeKind.Turns,
                MultiplierPercent = 150
            };

            Assert.AreEqual(150, spec.ToRule().MultiplierPercent);
            Assert.AreEqual(15, spec.ToRule().Apply(10));
        }

        [Test]
        public void EveryCatalogEntryHasADistinctKey()
        {
            CollectionAssert.AllItemsAreUnique(
                StatusSpecCatalog.All().Select(info => info.Key.Id).ToList());
        }

        [Test]
        public void RejectsAnUnknownStatusKeyByName()
        {
            var ex = Assert.Throws<Newtonsoft.Json.JsonSerializationException>(
                () => ContentJson.Read<StatusSpec>("{ \"key\": \"psion\" }"));

            StringAssert.Contains("psion", ex.Message);
        }
    }
}
