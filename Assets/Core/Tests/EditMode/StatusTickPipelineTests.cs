using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class StatusTickPipelineTests
    {
        private static readonly StatusKey TickKey = new StatusKey("tick_test");
        private static readonly StatusKey MarkerKey = new StatusKey("tick_marker_test");

        /// <summary>독과 같은 모양의 테스트 전용 틱: 마커가 있을 때만 Magnitude만큼 피해.</summary>
        private sealed class MarkerGatedTickBehavior : StatusBehavior
        {
            public override StatusKey Key => TickKey;
            public override StatusScope Scope => StatusScope.Entity;

            public override void OnTurnEnd(StatusTickContext ctx)
            {
                if (!ctx.HolderBag.Has(MarkerKey)) return;
                ctx.DealDamage(ctx.Instance.Magnitude);
                ctx.Events.Add(new StatusTicked(
                    ctx.HolderId, Key.Id, ctx.Instance.Magnitude, ctx.Instance.Magnitude));
            }
        }

        private sealed class MarkerBehavior : StatusBehavior
        {
            public override StatusKey Key => MarkerKey;
            public override StatusScope Scope => StatusScope.Entity;
        }

        private static StatusRegistry Registry()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new MarkerGatedTickBehavior());
            statuses.Register(new MarkerBehavior());
            return statuses;
        }

        [Test]
        public void Turn_end_tick_damages_enemy_and_emits_event_before_turn_ended()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            state.Enemies[0].Statuses.Add(TickKey, StatusLifetime.Permanent, 3);
            // ThisTurn 마커가 틱 시점에 아직 살아 있어야 한다 (틱이 수명 만료보다 먼저).
            state.Enemies[0].Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);

            var events = new TurnResolver(new EffectRegistry(), Registry()).Resolve(state, 0);

            Assert.AreEqual(7, state.Enemies[0].Hp);
            var tick = events.OfType<StatusTicked>().Single();
            Assert.AreEqual("goblin", tick.HolderId);
            Assert.AreEqual(3, tick.Damage);
            Assert.Less(events.IndexOf(tick), events.FindIndex(e => e is TurnEnded));
            // 수명 만료는 틱 이후: 마커는 턴이 끝난 뒤에는 제거되어 있다.
            Assert.IsFalse(state.Enemies[0].Statuses.Has(MarkerKey));
        }

        [Test]
        public void Dead_holder_is_excluded_from_ticks()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("dead", 0));   // 이미 사망
            state.Enemies[0].Statuses.Add(TickKey, StatusLifetime.Permanent, 3);
            state.Enemies[0].Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);

            var events = new TurnResolver(new EffectRegistry(), Registry()).Resolve(state, 0);

            Assert.AreEqual(0, state.Enemies[0].Hp);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
        }

        [Test]
        public void Party_ticks_run_before_enemy_ticks()
        {
            var state = new CombatState(TestContent.Statuses());
            var member = state.AddSoloPlayer(20);
            member.Statuses.Add(TickKey, StatusLifetime.Permanent, 1);
            member.Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);
            state.Enemies.Add(new Enemy("goblin", 10));
            state.Enemies[0].Statuses.Add(TickKey, StatusLifetime.Permanent, 1);
            state.Enemies[0].Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);

            var ticks = new TurnResolver(new EffectRegistry(), Registry())
                .Resolve(state, 0).OfType<StatusTicked>().ToList();

            CollectionAssert.AreEqual(
                new[] { CombatState.SoloPlayerId, "goblin" },
                ticks.Select(t => t.HolderId).ToArray());
        }
    }
}
