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

        private static ActionCardInstance Card(string id, Side side, int initiative, int damage)
        {
            var def = new CardDefinition(id, id, side, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ActionCardInstance(def);
        }

        [Test]
        public void StatusBag_merges_stacks_and_removes()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Vulnerable);
            bag.Add(StatusKeys.Vulnerable, 2);

            Assert.IsTrue(bag.Has(StatusKeys.Vulnerable));
            Assert.AreEqual(3, bag.Get(StatusKeys.Vulnerable).Stacks);
            Assert.IsTrue(bag.Remove(StatusKeys.Vulnerable));
            Assert.IsFalse(bag.Has(StatusKeys.Vulnerable));
        }

        [Test]
        public void Vulnerable_target_takes_50_percent_more_incoming_damage()
        {
            var state = new CombatState { PlayerHp = 30 };
            var enemy = new Enemy("goblin", 20);
            enemy.Statuses.Add(StatusKeys.Vulnerable);
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4)); // 4 -> 6

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var resolved = (CardResolved)events[1];

            Assert.AreEqual(6, resolved.DamageDealt);
            Assert.AreEqual(14, enemy.Hp);
        }

        [Test]
        public void Stunned_card_resolution_is_nullified()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 20));
            var card = Card("strike", Side.Player, 1, 5);
            card.Statuses.Add(StatusKeys.Stun);
            state.Zone.Add(card);

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var resolved = (CardResolved)events[1];

            Assert.AreEqual(0, resolved.DamageDealt);
            Assert.AreEqual(20, state.Enemies[0].Hp); // unchanged
        }

        [Test]
        public void Without_status_registry_incoming_damage_is_unmodified()
        {
            var state = new CombatState { PlayerHp = 30 };
            var enemy = new Enemy("goblin", 20);
            enemy.Statuses.Add(StatusKeys.Vulnerable);
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4));

            // no StatusRegistry -> raw damage, vulnerable ignored (backward compatible)
            var events = new TurnResolver(Effects()).Resolve(state, 0);
            var resolved = (CardResolved)events[1];

            Assert.AreEqual(4, resolved.DamageDealt);
            Assert.AreEqual(16, enemy.Hp);
        }
    }
}
