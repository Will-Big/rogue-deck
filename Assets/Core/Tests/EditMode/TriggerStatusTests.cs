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
            statuses.Register(new PoisonBehavior());
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            return statuses;
        }

        private static EffectData Trigger() => new EffectData(EffectKeys.TriggerStatus, 0)
        {
            Payload = new TriggerStatusPayload(StatusKeys.Poison)
        };

        [Test]
        public void Early_onset_ticks_now_and_suppresses_the_turn_end_tick()
        {
            // 조기 발병 모양: 독 1 부여 → 즉시 발동 → 이번 턴 종료에는 발동 없음.
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("early_onset", "조기 발병", Side.Player, 3, new[]
            {
                EffectData.ApplyStatus(StatusKeys.Poison, StatusApplyTarget.TargetEnemy, 1),
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
            // 마커(PoisonDormant)는 ThisTurn이라 EndOfTurn 정리에서 사라진다 — TurnResolver.Resolve로
            // 턴 전체를 돌리면 심어졌는지 확인할 수 없으므로, 핸들러를 직접 호출해 정리 전 상태를 본다.
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            var enemy = new Enemy("goblin", 20);
            state.Enemies.Add(enemy);
            var effect = Trigger();
            var card = new ExecutionCardInstance(
                new CardDefinition("t", "발동", Side.Player, 3, new[] { effect }))
                { OwnerId = CombatState.SoloPlayerId };
            var ctx = new EffectContext
            {
                Card = card, State = state, Effect = effect, EffectValue = 0, StatusRegistry = Statuses()
            };

            new TriggerStatusHandler().Apply(ctx);

            Assert.AreEqual(20, enemy.Hp);
            Assert.IsEmpty(ctx.ExtraEvents.OfType<StatusTicked>().ToList());
            Assert.IsNull(card.CancellationReason); // 취소 아님
            Assert.IsTrue(enemy.Statuses.Has(StatusKeys.PoisonDormant)); // 선점 잠복 마커가 실제로 심어졌다
        }
    }
}
