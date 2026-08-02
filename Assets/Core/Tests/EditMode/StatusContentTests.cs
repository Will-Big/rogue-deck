using System.Linq;
using FateWeaver.Core;
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

        private static StatusContentLoadResult Load(params CardContentSource[] sources)
            => StatusContentLoader.Load(sources, AuthoringContext.Default());

        /// <summary>등록된 상태가 하나뿐인 컨텍스트. 완전성 검사(모든 등록 상태가 저작돼야 함)를
        /// 우회해, 단일 상태의 매핑·규칙 변환만 좁게 검증하는 테스트에 쓴다. 완전성 검사 자체는
        /// <see cref="RequiresEveryRegisteredStatusToBeAuthored"/>가 <see cref="AuthoringContext.Default"/>로
        /// 따로 검증한다.</summary>
        private static AuthoringContext OnlyStatus(IStatusBehavior behavior)
        {
            var statuses = new StatusRegistry();
            statuses.Register(behavior);
            return new AuthoringContext(
                CombatRegistries.Effects(), statuses, CombatRegistries.InterventionActions());
        }

        [Test]
        public void LoadsAStatusIntoTheCatalog()
        {
            var result = StatusContentLoader.Load(
                new[]
                {
                    new CardContentSource(
                        "poison.json",
                        "{ \"key\": \"poison\", \"lifetime\": \"Permanent\", \"growthPerTurn\": 1 }")
                },
                OnlyStatus(new PoisonBehavior(growthPerTurn: 1)));

            Assert.IsTrue(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.AreEqual(
                StatusLifetimeKind.Permanent, result.Catalog.LifetimeOf(StatusKeys.Poison));
            Assert.IsFalse(result.Catalog.CountIsDuration(StatusKeys.Poison));
        }

        [Test]
        public void ExposesMultipliersAsCombatRules()
        {
            var result = StatusContentLoader.Load(
                new[]
                {
                    new CardContentSource(
                        "vulnerable.json",
                        "{ \"key\": \"vulnerable\", \"lifetime\": \"Turns\", \"multiplierPercent\": 150 }")
                },
                OnlyStatus(new VulnerableBehavior()));

            Assert.IsTrue(result.Succeeded, string.Join(" | ", result.Errors));
            Assert.AreEqual(15, result.Catalog.Rules.For(StatusKeys.Vulnerable).Apply(10));
            Assert.IsTrue(result.Catalog.CountIsDuration(StatusKeys.Vulnerable));
        }

        [Test]
        public void ReportsADuplicateStatusAcrossFiles()
        {
            const string Block = "{ \"key\": \"block\", \"lifetime\": \"ThisTurn\" }";
            var result = Load(
                new CardContentSource("a.json", Block),
                new CardContentSource("b.json", Block));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("block", result.Errors[0]);
            StringAssert.Contains("b.json", result.Errors[0]);
        }

        [Test]
        [Ignore("Task 5에서 stun 제거 후 활성화")]
        public void ReportsAStatusThatHasNoRegisteredBehavior()
        {
            var result = Load(new CardContentSource(
                "ghost.json", "{ \"key\": \"stun\", \"lifetime\": \"ThisTurn\" }"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("stun", result.Errors[0]);
        }

        [Test]
        public void RequiresEveryRegisteredStatusToBeAuthored()
        {
            var result = Load(new CardContentSource(
                "block.json", "{ \"key\": \"block\", \"lifetime\": \"ThisTurn\" }"));

            Assert.IsFalse(result.Succeeded);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("poison")));
        }

        [Test]
        public void DefaultsCoverEveryRegisteredStatus()
        {
            // AuthoringContext.Default().RegisteredStatusKeys가 아니라 StatusSpecCatalog.All()을
            // 기준으로 삼는다 — stun은 여전히 전투 레지스트리(StatusRegistry)에 남아 있지만
            // (Task 5에서 제거), 이미 Task 2에서 "저작 불가"로 확정돼 StatusSpecCatalog에서
            // 의도적으로 빠져 있다. 저작 기본값은 "저작 가능한 상태"를 전부 덮으면 된다.
            var catalog = StatusContentDefaults.Catalog();

            foreach (var info in StatusSpecCatalog.All())
            {
                Assert.DoesNotThrow(
                    () => catalog.LifetimeOf(info.Key), "상태 '" + info.Key.Id + "'의 기본값이 없다.");
            }
        }
    }
}
