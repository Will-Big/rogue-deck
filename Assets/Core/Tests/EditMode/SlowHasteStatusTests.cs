using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class SlowHasteStatusTests
    {
        // Task 4: slow/haste no longer carry their own strength on the StatusInstance's Magnitude —
        // the catalog is the only source (Content/Statuses: slow +2, haste -2 executionOrder).
        private static readonly StatusContentCatalog Content = TestContent.Statuses();

        private static StatusContext Ctx(StatusKey key) =>
            new StatusContext { Instance = new StatusInstance(key, StatusLifetime.Turns(2)), Content = Content };

        [Test]
        public void Base_behavior_does_not_change_executionOrder()
        {
            var block = new BlockBehavior();
            Assert.AreEqual(5, block.ModifyExecutionOrder(5, Ctx(StatusKeys.Block)));
        }

        [Test]
        public void Slow_adds_the_catalog_delta_to_executionOrder()
        {
            var slow = new SlowBehavior();
            Assert.AreEqual(StatusScope.Entity, slow.Scope);
            Assert.AreEqual(StatusKeys.Slow, slow.Key);
            Assert.AreEqual(7, slow.ModifyExecutionOrder(5, Ctx(StatusKeys.Slow))); // 5 + catalog delta 2
        }

        [Test]
        public void Haste_subtracts_the_catalog_delta_from_executionOrder()
        {
            var haste = new HasteBehavior();
            Assert.AreEqual(StatusScope.Entity, haste.Scope);
            Assert.AreEqual(StatusKeys.Haste, haste.Key);
            Assert.AreEqual(3, haste.ModifyExecutionOrder(5, Ctx(StatusKeys.Haste))); // 5 + catalog delta -2
        }
        /// <summary>Entity 범위 실행 순서 조작과 대비되는, 카드 범위(CardInstance) 상태의 표본 —
        /// 프로덕션의 stun을 대신한다(Task 5에서 제거). 카드 범위 상태가 ExecutionOrderFor의 접힘
        /// 대상에서 빠진다는 것만 검증하면 되므로 상태 자체의 의미는 무관하다.</summary>
        private sealed class CardScopedBehavior : StatusBehavior
        {
            public static readonly StatusKey TestKey = new StatusKey("test_card_scoped");
            public override StatusKey Key => TestKey;
            public override StatusScope Scope => StatusScope.CardInstance;
        }

        private static StatusRegistry Registry()
        {
            var r = new StatusRegistry();
            r.Register(new SlowBehavior());
            r.Register(new HasteBehavior());
            r.Register(new CardScopedBehavior());
            return r;
        }

        [Test]
        public void Fold_applies_entity_statuses_only()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Slow, StatusLifetime.Turns(2));
            Assert.AreEqual(7, StatusExecutionOrder.ExecutionOrderFor(5, bag, Registry(), Content.Rules, Content));

            var bag2 = new StatusBag();
            bag2.Add(StatusKeys.Haste, StatusLifetime.Turns(2));
            Assert.AreEqual(3, StatusExecutionOrder.ExecutionOrderFor(5, bag2, Registry(), Content.Rules, Content));
        }

        [Test]
        public void Fold_ignores_card_scoped_and_null_inputs()
        {
            var bag = new StatusBag();
            bag.Add(CardScopedBehavior.TestKey, StatusLifetime.UntilConsumed(1)); // card-scoped -> ignored
            Assert.AreEqual(5, StatusExecutionOrder.ExecutionOrderFor(5, bag, Registry(), Content.Rules, Content));
            Assert.AreEqual(5, StatusExecutionOrder.ExecutionOrderFor(5, bag, null, Content.Rules, Content));
            Assert.AreEqual(5, StatusExecutionOrder.ExecutionOrderFor(5, null, Registry(), Content.Rules, Content));
        }

        private static CardDefinition PlayerStrike() => new CardDefinition(
            "p_strike", "찌르기", Side.Player, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) }) { EnergyCost = 0, Category = CardCategory.Execution };

        private static CardDefinition EnemyJab() => new CardDefinition(
            "e_jab", "적찌르기", Side.Enemy, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) }) { EnergyCost = 0, Category = CardCategory.Execution };

        private static EnemyIntent JabEachTurn() => new EnemyIntent(new IReadOnlyList<CardDefinition>[]
        {
            new[] { EnemyJab() }, new[] { EnemyJab() }
        });

        [Test]
        public void Enemy_slow_raises_next_turn_enemy_card_executionOrder()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[] { PlayerStrike() }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);
            session.State.Enemies[0].Statuses.Add(StatusKeys.Slow, StatusLifetime.Turns(2));
            session.ResolveTurn();
            session.BeginNextTurn();
            var jab = session.CurrentOrder.First(c => c.Def.Id == "e_jab");
            Assert.AreEqual(7, jab.ExecutionOrder); // base 5 + slow's catalog delta 2
        }

        [Test]
        public void Player_haste_lowers_executionOrder_of_cards_placed_after_it()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[] { PlayerStrike() }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);
            session.State.Party[0].Statuses.Add(StatusKeys.Haste, StatusLifetime.Turns(2));
            session.PlayExecutionCard(0);
            var strike = session.CurrentOrder.First(c => c.Def.Id == "p_strike");
            Assert.AreEqual(3, strike.ExecutionOrder); // base 5 + haste's catalog delta -2
        }

        [Test]
        public void Owned_card_with_owner_id_not_matching_any_party_member_gets_no_status_bonus()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[] { new OwnedCard(PlayerStrike(), "warrior") },
                playerHp: 100,
                enemies: new[] { new Enemy("goblin", 100) },
                enemyPolicy: JabEachTurn(),
                fateEnergyPerTurn: 3,
                handSize: 5,
                seed: 1);
            // OwnerStatusesFor now matches by party member Id symmetrically in solo and party mode
            // (Task 4); "warrior" matches no member of the solo party (whose only member is
            // CombatState.SoloPlayerId), so the solo player's haste never reaches this card.
            session.State.Party[0].Statuses.Add(StatusKeys.Haste, StatusLifetime.Turns(2));

            Assert.IsTrue(session.PlayExecutionCard(0));

            var strike = session.CurrentOrder.First(c => c.Def.Id == "p_strike");
            Assert.AreEqual(5, strike.ExecutionOrder); // base order unmodified: no matching owner statuses
        }

        // 예전 C# 카드 저작 스펙에 있던 slow_hex 카드의 값을 그대로 옮긴 픽스처. Slow는 Turns
        // 종류라 카드가 주는 count는 지속(2턴)뿐이다.
        private static ExecutionCardSpec SlowHexFixture() => new ExecutionCardSpec
        {
            Id = "slow_hex",
            Name = "slow_hex",
            Side = Side.Player,
            Category = CardCategory.Execution,
            EnergyCost = 1,
            BaseExecutionOrder = 3,
            Effects = new EffectSpec[] { new ApplyStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Slow),
                Count = 2,
                Target = StatusApplyTarget.TargetEnemy
            } }
        };

        [Test]
        public void Playing_slow_card_slows_enemy_next_turn()
        {
            var slowCard = CardSpecMapper.ToDefinition(SlowHexFixture());
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[] { slowCard }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);

            int hand = session.Hand.Select((c, i) => (c, i)).First(x => x.c.Def.Id == "slow_hex").i;
            Assert.IsTrue(session.PlayExecutionCard(hand));
            session.ResolveTurn();
            Assert.IsTrue(session.State.Enemies[0].Statuses.Has(StatusKeys.Slow));
            session.BeginNextTurn();
            var jab = session.CurrentOrder.First(c => c.Def.Id == "e_jab");
            Assert.AreEqual(7, jab.ExecutionOrder); // base 5 + slow's catalog delta 2
        }
    }
}
