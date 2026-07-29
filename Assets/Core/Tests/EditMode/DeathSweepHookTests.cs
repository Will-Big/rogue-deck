using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class DeathSweepHookTests
    {
        private static readonly StatusKey RecorderKey = new StatusKey("death_recorder_test");

        private sealed class DeathRecorderBehavior : StatusBehavior
        {
            public readonly List<string> DiedHolders = new List<string>();
            public override StatusKey Key => RecorderKey;
            public override StatusScope Scope => StatusScope.Entity;
            public override void OnHolderDied(StatusDeathContext ctx)
                => DiedHolders.Add(ctx.HolderId);
        }

        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            return effects;
        }

        [Test]
        public void Enemy_killed_by_card_emits_enemy_died_and_dispatches_hook()
        {
            var recorder = new DeathRecorderBehavior();
            var statuses = new StatusRegistry();
            statuses.Register(recorder);

            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 3));
            state.Enemies[0].Statuses.Add(RecorderKey, StatusLifetime.Permanent);

            var def = new CardDefinition("slash", "베기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), statuses).Resolve(state, 0);

            var died = events.OfType<EnemyDied>().Single();
            Assert.AreEqual("goblin", died.EnemyId);
            // CardResolved 다음에 사망 이벤트가 따른다.
            Assert.Greater(events.IndexOf(died), events.FindIndex(e => e is CardResolved));
            CollectionAssert.AreEqual(new[] { "goblin" }, recorder.DiedHolders);
        }

        [Test]
        public void Enemy_killed_by_turn_end_tick_emits_enemy_died_before_turn_ended()
        {
            var recorder = new DeathRecorderBehavior();
            var statuses = new StatusRegistry();
            statuses.Register(recorder);
            statuses.Register(new LethalTickBehavior());

            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 2));
            state.Enemies[0].Statuses.Add(LethalTickBehavior.TickKey, StatusLifetime.Permanent, 5);
            state.Enemies[0].Statuses.Add(RecorderKey, StatusLifetime.Permanent);

            var events = new TurnResolver(new EffectRegistry(), statuses).Resolve(state, 0);

            var died = events.OfType<EnemyDied>().Single();
            Assert.Less(events.IndexOf(died), events.FindIndex(e => e is TurnEnded));
            CollectionAssert.AreEqual(new[] { "goblin" }, recorder.DiedHolders);
            // 틱 사망까지 반영된 뒤 결과가 계산된다 (마지막 적 사망 → 승리).
            Assert.AreEqual(Outcome.Win, events.OfType<TurnEnded>().Single().Outcome);
        }

        [Test]
        public void Party_member_killed_by_turn_end_tick_emits_party_died_before_turn_ended_and_loses()
        {
            var recorder = new DeathRecorderBehavior();
            var statuses = new StatusRegistry();
            statuses.Register(recorder);
            statuses.Register(new LethalTickBehavior());

            var state = new CombatState();
            var member = state.AddSoloPlayer(3);
            member.Statuses.Add(LethalTickBehavior.TickKey, StatusLifetime.Permanent, 5);
            member.Statuses.Add(RecorderKey, StatusLifetime.Permanent);
            state.Enemies.Add(new Enemy("goblin", 10));

            var events = new TurnResolver(new EffectRegistry(), statuses).Resolve(state, 0);

            var died = events.OfType<PartyMemberDied>().Single();
            Assert.AreEqual(CombatState.SoloPlayerId, died.MemberId);
            Assert.Less(events.IndexOf(died), events.FindIndex(e => e is TurnEnded));
            CollectionAssert.AreEqual(new[] { CombatState.SoloPlayerId }, recorder.DiedHolders);
            Assert.AreEqual(Outcome.Lose, events.OfType<TurnEnded>().Single().Outcome);
        }

        private sealed class LethalTickBehavior : StatusBehavior
        {
            public static readonly StatusKey TickKey = new StatusKey("lethal_tick_test");
            public override StatusKey Key => TickKey;
            public override StatusScope Scope => StatusScope.Entity;
            public override void OnTurnEnd(StatusTickContext ctx)
                => ctx.DealDamage(ctx.Instance.Magnitude);
        }
    }
}
