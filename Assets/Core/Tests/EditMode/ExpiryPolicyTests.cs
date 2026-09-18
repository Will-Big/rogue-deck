using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>상태 만료는 공통 턴 시점 방문으로 처리한다(전투 실행 계약 스펙 §8, 계획 T6). 상태 이름을 보지 않고
    /// 정책만 평가하며, 시점에 들어설 때 있던 상태만 그 시점에서 만료된다.</summary>
    public class ExpiryPolicyTests
    {
        private static readonly StatusKey GuardAtStart = new StatusKey("test_guard_at_start");

        /// <summary>턴 시작마다 보유자에게 방어 수치를 준다(테스트 전용).</summary>
        private sealed class GuardAtStartBehavior : StatusBehavior
        {
            public override StatusKey Key => GuardAtStart;
            public override StatusScope Scope => StatusScope.Entity;

            public override void OnTurnStart(StatusTickContext ctx)
                => ctx.HolderBag.Stack(StatusKeys.Block, StatusLifetime.Of(ExpiryPolicy.PhaseVisits(CombatPhase.Prepare, 1)), ctx.Instance.Magnitude);
        }

        [Test]
        public void Refresh_keeps_the_original_application_order()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            bag.Add(StatusKeys.Block, StatusLifetime.ThisTurn, 2);
            bag.Add(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            CollectionAssert.AreEqual(new[] { StatusKeys.Poison, StatusKeys.Block },
                bag.All.Select(s => s.Key).ToArray());
            Assert.AreEqual(3, bag.Get(StatusKeys.Poison).Magnitude, "재부여는 값을 갱신한다");
        }

        [Test]
        public void Refresh_updates_the_same_instance_in_place()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(1));
            var first = bag.Get(StatusKeys.Vulnerable);
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(3));

            Assert.AreSame(first, bag.Get(StatusKeys.Vulnerable));
            Assert.AreEqual(3, first.Count);
        }

        [Test]
        public void Legacy_lifetimes_convert_to_cleanup_visits()
        {
            Assert.AreEqual(ExpiryPolicy.PhaseVisits(CombatPhase.Cleanup, 1), StatusLifetime.ThisTurn.Expiry);
            Assert.AreEqual(ExpiryPolicy.PhaseVisits(CombatPhase.Cleanup, 2), StatusLifetime.Turns(2).Expiry);
            Assert.AreEqual(ExpiryPolicy.Permanent, StatusLifetime.Permanent.Expiry);
            Assert.AreEqual(ExpiryMode.UntilConsumed, StatusLifetime.UntilConsumed(2).Expiry.Mode);
        }

        [Test]
        public void A_status_expires_only_when_its_own_phase_is_visited()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            bag.Add(StatusKeys.Block, StatusLifetime.Of(ExpiryPolicy.PhaseVisits(CombatPhase.Prepare, 1)), 5);

            CollectionAssert.IsEmpty(StatusLifetimePolicy.VisitAll(bag, CombatPhase.Prepare, null)
                .Where(k => k == StatusKeys.Vulnerable));
            Assert.IsFalse(bag.Has(StatusKeys.Block), "방어는 준비 시점에 만료");
            Assert.IsTrue(bag.Has(StatusKeys.Vulnerable));

            Assert.IsEmpty(StatusLifetimePolicy.VisitAll(bag, CombatPhase.Cleanup, null));
            Assert.AreEqual(1, bag.Get(StatusKeys.Vulnerable).Count, "남은 방문이 하나 준다");
            CollectionAssert.AreEqual(new[] { StatusKeys.Vulnerable }, StatusLifetimePolicy.VisitAll(bag, CombatPhase.Cleanup, null).ToArray());
        }

        [Test]
        public void A_status_gained_while_a_phase_is_being_visited_is_not_expired_by_that_visit()
        {
            var bag = new StatusBag();
            var prepareOnce = StatusLifetime.Of(ExpiryPolicy.PhaseVisits(CombatPhase.Prepare, 1));
            bag.Add(StatusKeys.Block, prepareOnce, 5);

            var expired = StatusLifetimePolicy.VisitAll(bag, CombatPhase.Prepare,
                key => bag.Add(StatusKeys.Haste, prepareOnce));

            CollectionAssert.AreEqual(new[] { StatusKeys.Block }, expired.ToArray());
            Assert.IsTrue(bag.Has(StatusKeys.Haste), "시점 진입 때 없던 상태는 그 방문에서 만료되지 않는다");
        }

        [Test]
        public void Block_is_authored_to_expire_at_the_next_prepare()
        {
            var catalog = TestContent.Statuses();
            Assert.AreEqual(ExpiryPolicy.PhaseVisits(CombatPhase.Prepare, 1), catalog.LifetimeFor(StatusKeys.Block, 4).Expiry);
            Assert.IsFalse(catalog.CountIsDuration(StatusKeys.Block), "방어의 카드 숫자는 세기다");
            Assert.AreEqual(ExpiryPolicy.PhaseVisits(CombatPhase.Cleanup, 2), catalog.LifetimeFor(StatusKeys.Vulnerable, 2).Expiry);
        }

        [Test]
        public void Block_survives_turn_resolution_and_expires_at_the_next_prepare()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("a", 10));
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition("guard", "guard", Side.Player, 1,
                new[] { EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4) })
            {
                AllyTarget = CardTargetRange.Self
            })
            {
                OwnerId = CombatState.SoloPlayerId
            });
            var resolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses(), CombatRegistries.Reactions());

            var turn = resolver.Resolve(state, 0);
            Assert.IsTrue(player.Statuses.Has(StatusKeys.Block), "턴 정리(Cleanup)는 방어를 건드리지 않는다");
            Assert.IsEmpty(turn.OfType<StatusExpired>());

            var prepare = resolver.Prepare(state);
            Assert.IsFalse(player.Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(StatusKeys.Block.Id, prepare.OfType<StatusExpired>().Single().StatusId);
        }

        // V19: 준비 시점에 기존 방어가 만료된 뒤 턴 시작 상태가 준 새 방어는 유지된다.
        [Test]
        public void Block_gained_at_turn_start_survives_the_prepare_that_came_before_it()
        {
            var statuses = CombatRegistries.Statuses();
            statuses.Register(new GuardAtStartBehavior());
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(10);
            player.Statuses.Stack(StatusKeys.Block, StatusLifetime.Of(ExpiryPolicy.PhaseVisits(CombatPhase.Prepare, 1)), 7);
            player.Statuses.Add(GuardAtStart, StatusLifetime.Permanent, 3);
            state.Enemies.Add(new Enemy("a", 10));
            var resolver = new TurnResolver(CombatRegistries.Effects(), statuses, CombatRegistries.Reactions());

            resolver.Prepare(state);
            resolver.StartTurn(state);

            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Block).Magnitude, "7은 만료, 턴 시작의 3은 유지");
        }

        // V20: 턴 종료 독으로 양측이 전멸하면 그 시점 처리를 모두 마친 뒤 패배다.
        [Test]
        public void Turn_end_poison_wiping_both_sides_is_a_defeat()
        {
            var state = new CombatState(TestContent.Statuses());
            var player = state.AddSoloPlayer(1);
            player.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            var enemy = new Enemy("a", 1);
            enemy.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            state.Enemies.Add(enemy);

            var events = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses(), CombatRegistries.Reactions())
                .Resolve(state, 0);

            Assert.AreEqual(2, events.OfType<StatusTicked>().Count(), "양쪽 틱이 모두 수행된다");
            Assert.AreEqual(Outcome.Lose, events.OfType<TurnEnded>().Single().Outcome);
        }

        // D7: 턴 시작 시점의 상태 처리도 Primary 기원 — 그 사망에 사망 시 반응(전염)이 발동한다.
        [Test]
        public void Turn_start_status_deaths_run_death_abilities()
        {
            var statuses = CombatRegistries.Statuses();
            statuses.Register(new StartTickBehavior());
            var state = new CombatState(TestContent.PlainStatuses(StartTickBehavior.TickKey, StatusKeys.Poison, StatusKeys.Contagion));
            state.AddSoloPlayer(10);
            var victim = new Enemy("victim", 2);
            victim.Statuses.Add(StartTickBehavior.TickKey, StatusLifetime.Permanent, 5);
            victim.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 2);
            victim.Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));
            state.Enemies.Add(victim);
            state.Enemies.Add(new Enemy("next", 10));
            var resolver = new TurnResolver(CombatRegistries.Effects(), statuses, CombatRegistries.Reactions());

            var events = resolver.StartTurn(state);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "victim"));
            Assert.AreEqual("next", events.OfType<StatusTransferred>().Single().ToHolderId);
        }

        /// <summary>턴 시작마다 보유자에게 수치만큼 피해를 준다(테스트 전용).</summary>
        private sealed class StartTickBehavior : StatusBehavior
        {
            public static readonly StatusKey TickKey = new StatusKey("test_start_tick");
            public override StatusKey Key => TickKey;
            public override StatusScope Scope => StatusScope.Entity;
            public override void OnTurnStart(StatusTickContext ctx) => ctx.DealDamage(ctx.Instance.Magnitude);
        }
    }
}
