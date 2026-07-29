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
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new BlockBehavior());
            return statuses;
        }

        private static CombatState OneEnemy(int hp, int poison)
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", hp));
            if (poison > 0)
            {
                state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, poison);
            }
            return state;
        }

        [Test]
        public void Consume_clamps_to_available_magnitude_and_records_on_card()
        {
            var state = OneEnemy(20, 2);
            var def = new CardDefinition("drain", "흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 0) }
            });
            var card = new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
            state.Zone.Add(card);

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(2, card.ConsumedStatusAmount);
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void Consumed_stacks_feed_pending_damage_bonus_into_a_later_damage_effect()
        {
            // 응축 파열 모양: 독 최대 3 소비 → 피해 2 + 소비×2.
            var state = OneEnemy(20, 3);
            var def = new CardDefinition("burst", "파열", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 2) },
                new EffectData(EffectKeys.Damage, 2)
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(12, state.Enemies[0].Hp);   // 2 + 3×2 = 8 피해
            Assert.AreEqual(8, events.OfType<CardResolved>().Single().DamageDealt);
        }

        [Test]
        public void SkipOnBasic_effect_fires_only_when_condition_succeeds()
        {
            // 독성 환원 모양: 독 1 소비 → 소비했다면 자신에게 방어 4.
            EffectData[] BuildEffects() => new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 1, 0) },
                EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Permanent, StatusApplyTarget.Self, 4)
                    with { Condition = new ConsumedStatusAtLeast(1), SuccessEffectValue = 4, SkipOnBasic = true }
            };

            // 독이 있으면: 소비 성공 → 방어 4.
            var withPoison = OneEnemy(20, 1);
            withPoison.Zone.Add(new ExecutionCardInstance(
                new CardDefinition("reclaim", "환원", Side.Player, 4, BuildEffects()))
                { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(withPoison, 0);
            Assert.AreEqual(4, withPoison.Party[0].Statuses.Get(StatusKeys.Block).Magnitude);

            // 독이 없으면: 소비 0 → 효과 통째로 건너뜀 (방어 상태 자체가 없음).
            var without = OneEnemy(20, 0);
            without.Zone.Add(new ExecutionCardInstance(
                new CardDefinition("reclaim", "환원", Side.Player, 4, BuildEffects()))
                { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(without, 0);
            Assert.IsFalse(without.Party[0].Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Consuming_zero_is_not_a_cancellation()
        {
            var state = OneEnemy(20, 0);
            var def = new CardDefinition("drain", "흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 1, 0) },
                new EffectData(EffectKeys.Damage, 2)
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(1, events.OfType<CardResolved>().Count()); // 취소 아님
            Assert.AreEqual(18, state.Enemies[0].Hp);                  // 뒤 효과 정상 실행
        }
    }
}
