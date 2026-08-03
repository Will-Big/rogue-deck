using System.Linq;
using FateWeaver.Core;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class StatusContentTests
    {
        [Test]
        public void RoundTripsEveryRegisteredStatusSpecKind()
        {
            foreach (var spec in StatusContentDefaults.Specs())
            {
                var restored = ContentJson.Read<StatusSpec>(ContentJson.Write(spec));

                Assert.AreEqual(spec.GetType(), restored.GetType(), spec.Key.Id);
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
                StatusContentDefaults.Specs().Select(spec => spec.Key.Id).ToList());
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
                OnlyStatus(new PoisonBehavior()));

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

        /// <summary>행동은 있지만 저작 카탈로그에는 없는 가짜 상태 — Task 5 이전에는 stun이 이
        /// 시나리오의 실제 표본이었다. stun이 완전히 제거된 지금은 프로덕션 레지스트리에 그런
        /// 상태가 하나도 없어야 정상이므로(등록된 상태가 하나라도 저작되지 않으면 로드가 거부된다),
        /// 검증 로직 자체는 이 가짜 상태로 계속 지킨다.</summary>
        private sealed class UncontentedBehavior : StatusBehavior
        {
            public static readonly StatusKey TestKey = new StatusKey("test_uncontented");
            public override StatusKey Key => TestKey;
            public override StatusScope Scope => StatusScope.Entity;
        }

        [Test]
        public void RejectsACardThatAppliesAStatusWithNoAuthoredContent()
        {
            var spec = new ApplyStatusSpec
            {
                Status = StatusKeyRef.Of(UncontentedBehavior.TestKey),
                Count = 1,
                Target = StatusApplyTarget.TargetEnemy
            };

            var errors = spec.Validate(OnlyStatus(new UncontentedBehavior())).ToList();

            Assert.IsTrue(errors.Any(e => e.Contains("test_uncontented")));
        }

        // --- Task 4: 코어가 상태 카탈로그에서 수명과 세기를 읽는다 --------------------------------

        /// <summary>상태 콘텐츠를 실은 CombatState와 apply_status 효과 하나짜리 카드를 조립하고,
        /// 그 카드의 효과만 해결한다(EndOfTurnMaintenance는 돌리지 않는다 — 독의 자기 성장 틱이나
        /// Turns 상태의 EndOfTurn() 감소가 "카드가 준 count가 곧바로 무엇이 되는지"를 가리기
        /// 때문이다. 검증 대상은 부여 시점의 값이지, 틱을 거친 값이 아니다).</summary>
        private static class CombatFixture
        {
            public static CombatState WithStatusContent()
            {
                var state = new CombatState();
                state.AddSoloPlayer(20);
                state.Enemies.Add(new Enemy("enemy", 20));
                return state;
            }

            public static CardDefinition ApplyStatusCard(string statusId, int count)
                => new CardDefinition("fixture_card", "fixture_card", Side.Player, 1,
                    new[]
                    {
                        EffectData.ApplyStatus(new StatusKey(statusId), StatusApplyTarget.TargetEnemy, count)
                    })
                    { Category = CardCategory.Execution };

            public static void Resolve(CombatState state, CardDefinition cardDef)
            {
                var card = new ExecutionCardInstance(cardDef) { OwnerId = CombatState.SoloPlayerId };
                var effects = CombatRegistries.Effects();
                var statuses = CombatRegistries.Statuses();

                foreach (var effect in cardDef.Effects)
                {
                    var ctx = new EffectContext
                    {
                        Card = card,
                        State = state,
                        Effect = effect,
                        EffectValue = effect.EffectValue,
                        StatusRegistry = statuses
                    };
                    effects.Resolve(effect.Key).Apply(ctx);
                }
            }
        }

        [Test]
        public void CardCountBecomesMagnitudeForAPermanentStatus()
        {
            var state = CombatFixture.WithStatusContent();
            var card = CombatFixture.ApplyStatusCard("poison", count: 3);

            CombatFixture.Resolve(state, card);

            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void CardCountBecomesDurationForATurnsStatus()
        {
            var state = CombatFixture.WithStatusContent();
            var card = CombatFixture.ApplyStatusCard("slow", count: 3);

            CombatFixture.Resolve(state, card);
            var instance = state.Enemies[0].Statuses.Get(StatusKeys.Slow);

            Assert.AreEqual(3, instance.Count);
            Assert.AreEqual(StatusLifetimeKind.Turns, instance.Kind);
        }

        [Test]
        public void SlowStrengthComesFromTheStatusNotTheCard()
        {
            var state = CombatFixture.WithStatusContent();
            CombatFixture.Resolve(state, CombatFixture.ApplyStatusCard("slow", count: 2));

            Assert.AreEqual(
                2, state.StatusContent.ExecutionOrderDeltaOf(StatusKeys.Slow));
        }
    }
}
