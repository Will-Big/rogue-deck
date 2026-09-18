using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class ConsumeStatusTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());
            effects.Register(new ConsumeStatusHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior());
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new BlockBehavior());
            return statuses;
        }

        private static CombatState OneEnemy(int hp, int poison)
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", hp));
            if (poison > 0)
            {
                state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, poison);
            }
            return state;
        }

        private static EffectData Consume(string id, int amount, ConsumptionMode mode)
            => new EffectData(EffectKeys.ConsumeStatus, 0)
            {
                Id = id,
                TargetFaction = CardTargetFaction.Enemy,
                Payload = new ConsumeStatusPayload(StatusKeys.Poison, amount, mode)
            };

        private static int ConsumedBy(System.Collections.Generic.IEnumerable<ResolutionEvent> events)
            => events.OfType<StatusConsumed>().Sum(e => e.Amount);

        private static System.Collections.Generic.List<ResolutionEvent> Resolve(CombatState state, params EffectData[] effects)
        {
            var def = new CardDefinition("card", "카드", Side.Player, 4, effects)
            {
                AllyTarget = CardTargetRange.Self,
                EnemyTarget = CardTargetRange.FrontOne
            };
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });
            return new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
        }

        [TestCase(2, 3, ConsumptionMode.Exact, 0)]
        [TestCase(3, 3, ConsumptionMode.Exact, 3)]
        [TestCase(2, 3, ConsumptionMode.UpTo, 2)]
        [TestCase(5, 3, ConsumptionMode.UpTo, 3)]
        public void Consumption_preserves_the_authored_payment_rule(
            int available, int requested, ConsumptionMode mode, int expected)
        {
            Assert.AreEqual(expected, ConsumptionRule.Take(available, requested, mode));
        }

        [TestCase(-1, 1)]
        [TestCase(1, 0)]
        public void Consumption_rejects_negative_stock_or_a_non_positive_request(int available, int requested)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => ConsumptionRule.Take(available, requested, ConsumptionMode.UpTo));
        }

        // V15
        [Test]
        public void Up_to_consumes_what_is_available()
        {
            var state = OneEnemy(20, 2);

            var events = Resolve(state, Consume("pay", 3, ConsumptionMode.UpTo));

            Assert.AreEqual(2, ConsumedBy(events));
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison));
        }

        // V14
        [Test]
        public void Exact_consumes_nothing_when_short_and_skips_the_reward()
        {
            var state = OneEnemy(20, 2);

            var events = Resolve(state,
                Consume("pay", 3, ConsumptionMode.Exact),
                new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Enemy, Requirement = new EffectResultRequirement("pay", 1) });

            Assert.AreEqual(0, ConsumedBy(events));
            Assert.AreEqual(0, events.OfType<CardResolved>().Single().DamageDealt); // 보상 피해 없음
            // 남은 독 2는 턴 종료에 틱 피해 2를 주고 1 자란다 — 소비가 0이었다는 뜻이다.
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Exact_consumes_the_full_amount_when_available()
        {
            var state = OneEnemy(20, 3);

            var events = Resolve(state,
                Consume("pay", 3, ConsumptionMode.Exact),
                new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Enemy, Requirement = new EffectResultRequirement("pay", 3) });

            Assert.AreEqual(3, ConsumedBy(events));
            Assert.AreEqual(15, state.Enemies[0].Hp);
        }

        [Test]
        public void Scaling_adds_the_consumed_amount_times_the_factor()
        {
            // 응축 파열 모양: 독 최대 3 소비 → 피해 2 + 소비×2.
            var state = OneEnemy(20, 3);

            var events = Resolve(state,
                Consume("pay", 3, ConsumptionMode.UpTo),
                new EffectData(EffectKeys.Damage, 2) { TargetFaction = CardTargetFaction.Enemy, Scaling = new EffectResultScaling("pay", 2) });

            Assert.AreEqual(12, state.Enemies[0].Hp);   // 2 + 3×2 = 8 피해
            Assert.AreEqual(8, events.OfType<CardResolved>().Single().DamageDealt);
        }

        // V16
        [Test]
        public void A_reward_reads_only_the_effect_it_names()
        {
            var state = OneEnemy(20, 1);

            // 첫 소비가 독 1을 가져가 둘째 소비는 0이다. 보상은 둘째만 본다.
            var events = Resolve(state,
                Consume("first", 1, ConsumptionMode.UpTo),
                Consume("second", 1, ConsumptionMode.UpTo),
                new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Enemy, Requirement = new EffectResultRequirement("second", 1) });

            Assert.AreEqual(1, ConsumedBy(events));
            Assert.AreEqual(20, state.Enemies[0].Hp);
        }

        [Test]
        public void Requirement_skips_the_effect_when_nothing_was_consumed()
        {
            // 독성 환원 모양: 독 1 소비 → 소비했다면 적에게 피해 4.
            var withPoison = OneEnemy(20, 1);
            Resolve(withPoison,
                Consume("pay", 1, ConsumptionMode.UpTo),
                new EffectData(EffectKeys.Damage, 4) { TargetFaction = CardTargetFaction.Enemy, Requirement = new EffectResultRequirement("pay", 1) });
            Assert.AreEqual(16, withPoison.Enemies[0].Hp);

            var without = OneEnemy(20, 0);
            Resolve(without,
                Consume("pay", 1, ConsumptionMode.UpTo),
                new EffectData(EffectKeys.Damage, 4) { TargetFaction = CardTargetFaction.Enemy, Requirement = new EffectResultRequirement("pay", 1) });
            Assert.AreEqual(20, without.Enemies[0].Hp);
        }

        [Test]
        public void Consuming_zero_is_not_a_cancellation()
        {
            var state = OneEnemy(20, 0);

            var events = Resolve(state,
                Consume("pay", 1, ConsumptionMode.UpTo),
                new EffectData(EffectKeys.Damage, 2) { TargetFaction = CardTargetFaction.Enemy });

            Assert.AreEqual(1, events.OfType<CardResolved>().Count()); // 취소 아님
            Assert.AreEqual(18, state.Enemies[0].Hp);                  // 뒤 효과 정상 실행
        }

        // V17: 카드 시작 조건은 차례를 맞을 때 한 번 평가되고, 카드 도중 상태가 바뀌어도 다시 평가하지 않는다.
        [Test]
        public void Start_condition_is_fixed_even_when_the_card_changes_what_it_checked()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("front", 5));
            state.Enemies.Add(new Enemy("back", 100));
            // 대상은 아직 카드 시작 때 한 번 고정되므로(효과 단위 선택은 계획 T3) 두 효과 모두 적 전체를 친다.
            var card = new CardDefinition("card", "카드", Side.Player, 1, new[]
            {
                new EffectData(EffectKeys.Damage, 10) { TargetFaction = CardTargetFaction.Enemy },
                new EffectData(EffectKeys.Damage, 1) { TargetFaction = CardTargetFaction.Enemy, SuccessEffectValue = 9 }
            })
            {
                EnemyTarget = CardTargetRange.All,
                // 시작 때는 뒤에 front의 카드가 있어 Basic이다. 첫 효과가 front를 죽여 그 카드가 실행선에서
                // 빠져도, 둘째 효과는 고정된 Basic 수치를 쓴다.
                StartCondition = new NoFollowingCardOfSide(Side.Enemy)
            };
            var frontCard = new CardDefinition("front_jab", "front_jab", Side.Enemy, 2,
                new[] { new EffectData(EffectKeys.Damage, 1) { TargetFaction = CardTargetFaction.Ally } }) { AllyTarget = CardTargetRange.FrontOne };
            state.Zone.Add(new ExecutionCardInstance(card) { OwnerId = CombatState.SoloPlayerId });
            state.Zone.Add(new ExecutionCardInstance(frontCard) { OwnerId = "front" });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            var resolved = events.OfType<CardResolved>().Single(e => e.CardId == "card");
            Assert.AreEqual(ConditionTier.Basic, resolved.ConditionTier);
            Assert.AreEqual("front_jab", events.OfType<CardRemoved>().Single().CardId);
            Assert.AreEqual(100 - 10 - 1, state.Enemies[1].Hp); // Success였다면 100 - 10 - 9
        }
    }
}
