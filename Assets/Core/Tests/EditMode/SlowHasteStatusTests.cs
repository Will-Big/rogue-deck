using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class SlowHasteStatusTests
    {
        private static StatusContext Ctx(StatusKey key, int magnitude) =>
            new StatusContext { Instance = new StatusInstance(key, StatusLifetime.Turns(2), magnitude) };

        [Test]
        public void Base_behavior_does_not_change_executionOrder()
        {
            var block = new BlockBehavior();
            Assert.AreEqual(5, block.ModifyExecutionOrder(5, Ctx(StatusKeys.Block, 3)));
        }

        [Test]
        public void Slow_adds_magnitude_to_executionOrder()
        {
            var slow = new SlowBehavior();
            Assert.AreEqual(StatusScope.Entity, slow.Scope);
            Assert.AreEqual(StatusKeys.Slow, slow.Key);
            Assert.AreEqual(8, slow.ModifyExecutionOrder(5, Ctx(StatusKeys.Slow, 3)));
        }

        [Test]
        public void Haste_subtracts_magnitude_from_executionOrder()
        {
            var haste = new HasteBehavior();
            Assert.AreEqual(StatusScope.Entity, haste.Scope);
            Assert.AreEqual(StatusKeys.Haste, haste.Key);
            Assert.AreEqual(2, haste.ModifyExecutionOrder(5, Ctx(StatusKeys.Haste, 3)));
        }
        private static StatusRegistry Registry()
        {
            var r = new StatusRegistry();
            r.Register(new SlowBehavior());
            r.Register(new HasteBehavior());
            r.Register(new StunBehavior());
            return r;
        }

        [Test]
        public void Fold_applies_entity_statuses_only()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            Assert.AreEqual(8, StatusExecutionOrder.ExecutionOrderFor(5, bag, Registry()));

            var bag2 = new StatusBag();
            bag2.Add(StatusKeys.Haste, StatusLifetime.Turns(2), 2);
            Assert.AreEqual(3, StatusExecutionOrder.ExecutionOrderFor(5, bag2, Registry()));
        }

        [Test]
        public void Fold_ignores_card_scoped_and_null_inputs()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Stun, StatusLifetime.UntilConsumed(1)); // card-scoped -> ignored
            Assert.AreEqual(5, StatusExecutionOrder.ExecutionOrderFor(5, bag, Registry()));
            Assert.AreEqual(5, StatusExecutionOrder.ExecutionOrderFor(5, bag, null));
            Assert.AreEqual(5, StatusExecutionOrder.ExecutionOrderFor(5, null, Registry()));
        }

        private static CardDefinition PlayerStrike() => new CardDefinition(
            "p_strike", "찌르기", Side.Player, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) }) { EnergyCost = 0, Category = CardCategory.Execution };

        private static CardDefinition EnemyJab() => new CardDefinition(
            "e_jab", "적찌르기", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) }) { EnergyCost = 0, Category = CardCategory.Execution };

        private static EnemyIntent JabEachTurn() => new EnemyIntent(new IReadOnlyList<CardDefinition>[]
        {
            new[] { EnemyJab() }, new[] { EnemyJab() }
        });

        [Test]
        public void Enemy_slow_raises_next_turn_enemy_card_executionOrder()
        {
            var session = new DeckCombatSession(
                new[] { PlayerStrike() }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);
            session.State.Enemies[0].Statuses.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            session.ResolveTurn();
            session.BeginNextTurn();
            var jab = session.CurrentOrder.First(c => c.Def.Id == "e_jab");
            Assert.AreEqual(8, jab.ExecutionOrder); // base 5 + slow 3
        }

        [Test]
        public void Player_haste_lowers_executionOrder_of_cards_placed_after_it()
        {
            var session = new DeckCombatSession(
                new[] { PlayerStrike() }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);
            session.State.PlayerStatuses.Add(StatusKeys.Haste, StatusLifetime.Turns(2), 3);
            session.PlayExecutionCard(0);
            var strike = session.CurrentOrder.First(c => c.Def.Id == "p_strike");
            Assert.AreEqual(2, strike.ExecutionOrder); // base 5 - haste 3
        }

        [Test]
        public void Playing_slow_card_slows_enemy_next_turn()
        {
            var slowCard = CardSpecMapper.ToDefinition(StarterDeckSpecs.SlowHex());
            var session = new DeckCombatSession(
                new[] { slowCard }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);

            int hand = session.Hand.Select((c, i) => (c, i)).First(x => x.c.Def.Id == "slow_hex").i;
            Assert.IsTrue(session.PlayExecutionCard(hand));
            session.ResolveTurn();
            Assert.IsTrue(session.State.Enemies[0].Statuses.Has(StatusKeys.Slow));
            session.BeginNextTurn();
            var jab = session.CurrentOrder.First(c => c.Def.Id == "e_jab");
            Assert.AreEqual(8, jab.ExecutionOrder); // base 5 + slow 3
        }
    }
}
