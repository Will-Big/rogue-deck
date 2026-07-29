using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class EnemyTargetingTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new BlockBehavior());
            return statuses;
        }

        private static CombatState TwoEnemies()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("front", 10));
            state.Enemies.Add(new Enemy("back", 10));
            return state;
        }

        [Test]
        public void BackMost_selector_hits_the_living_back_enemy()
        {
            var state = TwoEnemies();
            var def = new CardDefinition("back_hit", "후열 타격", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 3) { TargetSelector = TargetSelector.BackMost } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(7, state.Enemies[1].Hp);
        }

        [Test]
        public void All_selector_damages_every_living_enemy_and_sums_damage_dealt()
        {
            var state = TwoEnemies();
            state.Enemies.Add(new Enemy("dead", 0)); // 생존 대형에서 제외
            var def = new CardDefinition("sweep", "휩쓸기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 2) { TargetSelector = TargetSelector.All } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(8, state.Enemies[0].Hp);
            Assert.AreEqual(8, state.Enemies[1].Hp);
            Assert.AreEqual(0, state.Enemies[2].Hp);   // 시체는 건드리지 않음
            Assert.AreEqual(4, events.OfType<CardResolved>().Single().DamageDealt);
        }

        [Test]
        public void Apply_status_with_selector_targets_back_enemy()
        {
            var state = TwoEnemies();
            var def = new CardDefinition("back_status", "후열 부여", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.TargetEnemy, 2)
                    with { TargetSelector = TargetSelector.BackMost } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            // ThisTurn 상태는 턴 종료에 만료되므로 부여 사실은 수명 만료 이전 세맨틱으로 검증할 수
            // 없다 — 대신 만료 전 수치를 남기는 Permanent로 재검증한다.
            var state2 = TwoEnemies();
            var def2 = new CardDefinition("back_status2", "후열 부여2", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Permanent, StatusApplyTarget.TargetEnemy, 2)
                    with { TargetSelector = TargetSelector.BackMost } });
            state2.Zone.Add(new ExecutionCardInstance(def2) { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(state2, 0);
            Assert.AreEqual(2, state2.Enemies[1].Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Party_by_selector_applies_status_to_front_ally()
        {
            var state = new CombatState();
            state.Party.Add(new PartyMember("a", "A", 20));
            state.Party.Add(new PartyMember("b", "B", 20));
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("cover_front", "전열 엄호", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Permanent, StatusApplyTarget.PartyBySelector, 4)
                    with { TargetSelector = TargetSelector.FrontMost } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = "b" });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(4, state.Party[0].Statuses.Get(StatusKeys.Block).Magnitude);
            Assert.IsFalse(state.Party[1].Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Block_applications_stack_within_a_turn()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var block3 = new CardDefinition("b3", "방어3", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                    StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3) });
            var block1 = new CardDefinition("b1", "방어1", Side.Player, 5,
                new[] { EffectData.ApplyStatus(
                    StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 1) });
            var enemyHit = new CardDefinition("jab", "찌르기", Side.Enemy, 6,
                new[] { new EffectData(EffectKeys.Damage, 4) });
            state.Zone.Add(new ExecutionCardInstance(block3) { OwnerId = CombatState.SoloPlayerId });
            state.Zone.Add(new ExecutionCardInstance(block1) { OwnerId = CombatState.SoloPlayerId });
            state.Zone.Add(new ExecutionCardInstance(enemyHit) { OwnerId = "goblin" });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 합산 방어 4가 피해 4를 전부 흡수한다 (교체였다면 방어 1만 남아 3 피해).
            Assert.AreEqual(20, state.Party[0].Hp);
        }
    }
}
