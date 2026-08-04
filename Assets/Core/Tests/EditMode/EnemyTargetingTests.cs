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
        [Test]
        public void Back_two_returns_up_to_two_distinct_living_enemies_in_formation_order()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Enemies.Add(new Enemy("a", 10));
            state.Enemies.Add(new Enemy("b", 10));
            state.Enemies.Add(new Enemy("c", 10));

            CollectionAssert.AreEqual(
                new[] { "b", "c" },
                EnemyTargeting.SelectRange(state, TargetSelector.BackTwo)
                    .Select(enemy => enemy.Id));
        }

        [TestCase(TargetSelector.FrontTwo)]
        [TestCase(TargetSelector.BackTwo)]
        [TestCase(TargetSelector.All)]
        public void One_living_enemy_range_returns_that_enemy_once(TargetSelector selector)
        {
            var state = new CombatState(TestContent.Statuses());
            var only = new Enemy("only", 10);
            state.Enemies.Add(only);

            var targets = EnemyTargeting.SelectRange(state, selector);

            Assert.AreEqual(1, targets.Count);
            Assert.AreSame(only, targets[0]);
        }

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
            statuses.Register(new PoisonBehavior());
            return statuses;
        }

        private static CombatState TwoEnemies()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("front", 10));
            state.Enemies.Add(new Enemy("back", 10));
            return state;
        }

        [Test]
        public void BackOne_selector_hits_the_living_back_enemy()
        {
            var state = TwoEnemies();
            var def = new CardDefinition("back_hit", "후열 타격", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 3) { TargetSelector = TargetSelector.BackOne } });
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
        public void Enemy_all_selector_damages_every_living_party_member_and_sums_damage_dealt()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Add(new PartyMember("a", "A", 10));
            state.Party.Add(new PartyMember("b", "B", 10));
            var dead = new PartyMember("c", "C", 10);
            dead.Hp = 0; // 생존 대형에서 제외
            state.Party.Add(dead);
            state.Enemies.Add(new Enemy("goblin", 10));

            var def = new CardDefinition("goblin_sweep", "고블린 휩쓸기", Side.Enemy, 4,
                new[] { new EffectData(EffectKeys.Damage, 3) { TargetSelector = TargetSelector.All } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = "goblin" });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(7, state.Party[0].Hp); // a: 10 - 3
            Assert.AreEqual(7, state.Party[1].Hp); // b: 10 - 3
            Assert.AreEqual(0, state.Party[2].Hp); // dead member untouched
            Assert.AreEqual(6, events.OfType<CardResolved>().Single().DamageDealt);
        }

        [Test]
        public void Apply_status_with_selector_targets_back_enemy()
        {
            var state = TwoEnemies();
            var def = new CardDefinition("back_status", "후열 부여", Side.Player, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.TargetEnemy, 2)
                    with { TargetSelector = TargetSelector.BackOne } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            // Block은 카탈로그상 ThisTurn이라 턴 종료에 만료되므로 부여 사실은 수명 만료 이전
            // 세맨틱으로 검증할 수 없다 — 카탈로그가 Permanent로 정한 독으로 재검증한다(카드는
            // 더 이상 수명을 고를 수 없다 — Task 4).
            var state2 = TwoEnemies();
            var def2 = new CardDefinition("back_status2", "후열 부여2", Side.Player, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Poison, StatusApplyTarget.TargetEnemy, 2)
                    with { TargetSelector = TargetSelector.BackOne } });
            state2.Zone.Add(new ExecutionCardInstance(def2) { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(state2, 0);
            // 부여된 독 2가 턴 종료 틱에서 피해 2를 준 뒤 카탈로그의 턴당 성장치(1)만큼 자란다: 2 + 1 = 3.
            Assert.AreEqual(3, state2.Enemies[1].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Party_by_selector_applies_status_to_front_ally()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Add(new PartyMember("a", "A", 20));
            state.Party.Add(new PartyMember("b", "B", 20));
            state.Enemies.Add(new Enemy("goblin", 10));
            // Block은 카탈로그상 ThisTurn이라 턴 종료에 만료되므로, 부여 사실을 만료 이후에도
            // 검증할 수 있는 Permanent 상태(독)로 확인한다 — 카드는 더 이상 수명을 고르지 않는다
            // (Task 4).
            var def = new CardDefinition("cover_front", "전열 엄호", Side.Player, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Poison, StatusApplyTarget.PartyBySelector, 4)
                    with { TargetSelector = TargetSelector.FrontOne } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = "b" });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 부여된 독 4가 턴 종료 틱에서 피해를 준 뒤 카탈로그의 턴당 성장치(1)만큼 자란다: 4 + 1 = 5.
            Assert.AreEqual(5, state.Party[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            Assert.IsFalse(state.Party[1].Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void Block_applications_stack_within_a_turn()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var block3 = new CardDefinition("b3", "방어3", Side.Player, 4,
                new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 3) });
            var block1 = new CardDefinition("b1", "방어1", Side.Player, 5,
                new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 1) });
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
