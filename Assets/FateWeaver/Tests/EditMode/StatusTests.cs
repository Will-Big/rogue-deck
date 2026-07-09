using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class StatusTests
    {
        private static EffectRegistry Effects()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new StunBehavior());
            r.Register(new VulnerableBehavior());
            r.Register(new RewardNullifiedBehavior());
            return r;
        }

        private static ExecutionCardInstance Card(string id, Side side, int executionOrder, int damage)
        {
            var def = new CardDefinition(id, id, side, CardType.Attack, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void StatusBag_add_refresh_remove()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            Assert.IsTrue(bag.Has(StatusKeys.Vulnerable));
            Assert.AreEqual(2, bag.Get(StatusKeys.Vulnerable).Count);

            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(5)); // re-apply refreshes
            Assert.AreEqual(5, bag.Get(StatusKeys.Vulnerable).Count);

            Assert.IsTrue(bag.Remove(StatusKeys.Vulnerable));
            Assert.IsFalse(bag.Has(StatusKeys.Vulnerable));
        }

        [Test]
        public void EndOfTurn_drops_thisturn_ticks_turns_keeps_permanent()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Stun, StatusLifetime.ThisTurn);
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            bag.Add(StatusKeys.RewardNullified, StatusLifetime.Permanent);

            bag.EndOfTurn();
            Assert.IsFalse(bag.Has(StatusKeys.Stun));                  // ThisTurn dropped
            Assert.AreEqual(1, bag.Get(StatusKeys.Vulnerable).Count);  // 2 -> 1
            Assert.IsTrue(bag.Has(StatusKeys.RewardNullified));        // permanent kept

            bag.EndOfTurn();
            Assert.IsFalse(bag.Has(StatusKeys.Vulnerable));            // 1 -> 0, removed
        }

        [Test]
        public void Vulnerable_turns_based_modifies_hit_without_being_consumed()
        {
            var state = new CombatState { PlayerHp = 30 };
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4)); // 4 -> 6

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(24, enemy.Hp);
            // not consumed by the hit; ticked down once by end-of-turn (2 -> 1)
            Assert.AreEqual(1, enemy.Statuses.Get(StatusKeys.Vulnerable).Count);
        }

        [Test]
        public void Vulnerable_until_consumed_applies_once_then_is_gone()
        {
            var state = new CombatState { PlayerHp = 30 };
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.UntilConsumed(1));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike1", Side.Player, 1, 4)); // 6 (consumes the charge)
            state.Zone.Add(Card("strike2", Side.Player, 2, 4)); // 4 (vulnerable gone)

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(4, ((CardResolved)events[2]).DamageDealt);
            Assert.IsFalse(enemy.Statuses.Has(StatusKeys.Vulnerable));
        }

        [Test]
        public void Stun_until_consumed_nullifies_one_resolution_then_is_gone()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 20));
            var card = Card("strike", Side.Player, 1, 5);
            card.Statuses.Add(StatusKeys.Stun, StatusLifetime.UntilConsumed(1));
            state.Zone.Add(card);

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(0, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.IsFalse(card.Statuses.Has(StatusKeys.Stun));
        }

        [Test]
        public void Without_status_registry_incoming_damage_is_unmodified()
        {
            var state = new CombatState { PlayerHp = 30 };
            var enemy = new Enemy("goblin", 20);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(1));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4));

            var events = new TurnResolver(Effects()).Resolve(state, 0); // no StatusRegistry
            Assert.AreEqual(4, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(16, enemy.Hp);
        }
    }
}
