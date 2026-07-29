using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class TriggerStatusTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new ApplyStatusHandler());
            effects.Register(new TriggerStatusHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            return statuses;
        }

        private static EffectData Trigger() => new EffectData(EffectKeys.TriggerStatus, 0)
        {
            Payload = new TriggerStatusPayload(StatusKeys.Poison, StatusKeys.PoisonDormant)
        };

        [Test]
        public void Early_onset_ticks_now_and_suppresses_the_turn_end_tick()
        {
            // 조기 발병 모양: 독 1 부여 → 즉시 발동 → 이번 턴 종료에는 발동 없음.
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("early_onset", "조기 발병", Side.Player, 3, new[]
            {
                EffectData.ApplyStatus(
                    StatusKeys.Poison, StatusLifetime.Permanent, StatusApplyTarget.TargetEnemy, 1),
                Trigger()
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 즉시 발동: 피해 1 + 성장 → 독 2. 턴 종료 발동 없음 → HP는 19 유지, 독은 2 유지.
            Assert.AreEqual(19, state.Enemies[0].Hp);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            var tick = events.OfType<StatusTicked>().Single();  // 즉시 발동분 1회뿐
            Assert.Greater(events.IndexOf(tick), events.FindIndex(e => e is CardResolved));
            Assert.AreEqual(1, events.OfType<CardResolved>().Single().DamageDealt);
            // 다음 턴에는 정상 발동 (잠복 마커는 ThisTurn으로 소멸).
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.PoisonDormant));
        }

        [Test]
        public void Trigger_without_the_status_only_plants_the_marker()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("t", "발동", Side.Player, 3, new[] { Trigger() });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
            Assert.AreEqual(1, events.OfType<CardResolved>().Count()); // 취소 아님
        }
    }
}
