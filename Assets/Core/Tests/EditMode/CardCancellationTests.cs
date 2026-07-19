using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Tests
{
    /// <summary>Task 3: execution-card cancellation events (CardCancelled) and the per-effect death
    /// sweep (PartyMemberDied / DeathsDoorSurvived), including the owner-death cascade that cancels a
    /// dead party member's still-pending cards.</summary>
    public class CardCancellationTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static ExecutionCardInstance Card(
            string id,
            Side side,
            int executionOrder,
            int damage,
            string ownerId = null,
            string targetId = null,
            int instanceId = -1)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def)
            {
                OwnerId = ownerId,
                TargetId = targetId,
                InstanceId = instanceId
            };
        }

        [Test]
        public void Owner_death_marks_only_pending_cards_owned_by_that_member()
        {
            var state = new CombatState();
            state.Party.Clear();
            state.Party.Add(new PartyMember("warrior", "Warrior", maxHp: 5));
            state.Party.Add(new PartyMember("mage", "Mage", maxHp: 5));
            state.Enemies.Add(new Enemy("goblin", 100));

            // Enemy attack (FrontMost default target) kills the warrior outright.
            var enemyStrike = Card("enemy_strike", Side.Enemy, executionOrder: 1, damage: 10, instanceId: 1);
            var warriorCard = Card("warrior_card", Side.Player, executionOrder: 2, damage: 3, ownerId: "warrior", instanceId: 2);
            var mageCard = Card("mage_card", Side.Player, executionOrder: 3, damage: 3, ownerId: "mage", instanceId: 3);
            var ownerlessCard = Card("ownerless_card", Side.Player, executionOrder: 4, damage: 3, ownerId: null, instanceId: 4);

            state.Zone.Add(enemyStrike);
            state.Zone.Add(warriorCard);
            state.Zone.Add(mageCard);
            state.Zone.Add(ownerlessCard);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var cancelledWarriorCard = events.OfType<CardCancelled>().Single(e => e.CardId == "warrior_card");
            Assert.AreEqual(CardCancellationReason.OwnerDied, cancelledWarriorCard.Reason);
            Assert.AreEqual("warrior", cancelledWarriorCard.OwnerId);

            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "mage_card"));
            Assert.IsTrue(events.OfType<CardResolved>().Any(e => e.CardId == "ownerless_card"));
            Assert.IsFalse(events.OfType<CardCancelled>().Any(e => e.CardId == "mage_card"));
            Assert.IsFalse(events.OfType<CardCancelled>().Any(e => e.CardId == "ownerless_card"));
        }

        [Test]
        public void Cancelled_card_emits_no_card_resolved_event()
        {
            var state = new CombatState { PlayerHp = 30 };
            // No enemies -> the player card's target can never resolve.
            var strike = Card("strike", Side.Player, executionOrder: 1, damage: 4);
            state.Zone.Add(strike);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsFalse(events.OfType<CardResolved>().Any());
            var cancelled = events.OfType<CardCancelled>().Single();
            Assert.AreEqual(CardCancellationReason.NoValidTarget, cancelled.Reason);
        }

        [Test]
        public void Card_cancelled_event_contains_instance_owner_and_reason()
        {
            var state = new CombatState { PlayerHp = 30 };
            var card = Card("sealed_blade", Side.Player, executionOrder: 1, damage: 4, ownerId: "warrior", instanceId: 42);
            // Pre-cancelled before resolution starts (step 6, part 1 of the death-sweep order).
            card.CancellationReason = CardCancellationReason.StatusIntercepted;
            state.Zone.Add(card);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var cancelled = events.OfType<CardCancelled>().Single();
            Assert.AreEqual(42, cancelled.InstanceId);
            Assert.AreEqual("sealed_blade", cancelled.CardId);
            Assert.AreEqual("warrior", cancelled.OwnerId);
            Assert.AreEqual(CardCancellationReason.StatusIntercepted, cancelled.Reason);
        }

        [Test]
        public void Hp_reaching_exactly_one_without_spending_a_charge_emits_no_deaths_door_event()
        {
            var state = new CombatState();
            state.Party.Clear();
            var hero = new PartyMember("hero", "Hero", maxHp: 5, surviveCharges: 1);
            state.Party.Add(hero);

            var strike = Card("goblin_jab", Side.Enemy, executionOrder: 1, damage: 4); // 5 -> 1, no charge spent
            state.Zone.Add(strike);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.AreEqual(1, hero.Hp);
            Assert.AreEqual(1, hero.SurviveCharges);
            Assert.IsFalse(events.OfType<DeathsDoorSurvived>().Any());
            Assert.IsFalse(events.OfType<PartyMemberDied>().Any());
        }

        [Test]
        public void Charge_decrease_emits_deaths_door_even_when_hp_was_already_one_before_a_later_effect()
        {
            var state = new CombatState();
            state.Party.Clear();
            var hero = new PartyMember("hero", "Hero", maxHp: 5, surviveCharges: 1);
            state.Party.Add(hero);

            // Two effects on one card: first lands hero exactly on 1 HP (no charge spent), second
            // would be lethal and must spend the charge even though HP was already 1 going in.
            var def = new CardDefinition("double_strike", "double_strike", Side.Enemy, 1,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 4),
                    new EffectData(EffectKeys.Damage, 1)
                });
            state.Zone.Add(new ExecutionCardInstance(def));

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.AreEqual(1, hero.Hp);
            Assert.AreEqual(0, hero.SurviveCharges);
            var survived = events.OfType<DeathsDoorSurvived>().Single();
            Assert.AreEqual("hero", survived.MemberId);
            Assert.IsFalse(events.OfType<PartyMemberDied>().Any());
        }

        [Test]
        public void Kill_then_no_target_emits_cancellation_before_death_and_owner_cancellation()
        {
            var state = new CombatState();
            state.Party.Clear();
            var memberA = new PartyMember("a", "A", maxHp: 5, surviveCharges: 0);
            state.Party.Add(memberA);
            state.Enemies.Add(new Enemy("goblin", 100));

            var killThenCancel = new CardDefinition(
                "kill_then_cancel",
                "kill_then_cancel",
                Side.Enemy,
                1,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 5),
                    new EffectData(EffectKeys.Damage, 1)
                });
            state.Zone.Add(new ExecutionCardInstance(killThenCancel) { InstanceId = 1, OwnerId = "goblin" });
            state.Zone.Add(Card(
                "a_pending",
                Side.Player,
                executionOrder: 2,
                damage: 3,
                ownerId: memberA.Id,
                instanceId: 2));

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var relevant = events
                .Where(e => e is CardCancelled || e is PartyMemberDied)
                .ToArray();
            Assert.AreEqual(3, relevant.Length);
            Assert.AreEqual(typeof(CardCancelled), relevant[0].GetType());
            var current = (CardCancelled)relevant[0];
            Assert.AreEqual("kill_then_cancel", current.CardId);
            Assert.AreEqual(CardCancellationReason.NoValidTarget, current.Reason);

            Assert.AreEqual(typeof(PartyMemberDied), relevant[1].GetType());
            var died = (PartyMemberDied)relevant[1];
            Assert.AreEqual(memberA.Id, died.MemberId);

            Assert.AreEqual(typeof(CardCancelled), relevant[2].GetType());
            var pending = (CardCancelled)relevant[2];
            Assert.AreEqual("a_pending", pending.CardId);
            Assert.AreEqual(CardCancellationReason.OwnerDied, pending.Reason);
            Assert.IsFalse(events.OfType<CardResolved>().Any(e => e.CardId == "kill_then_cancel"));
            Assert.AreEqual(1, events.OfType<CardCancelled>().Count(e => e.CardId == "kill_then_cancel"));
        }

        [Test]
        public void Duplicate_card_ids_are_distinguished_by_instance_id()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 100));

            var first = Card("slash", Side.Player, executionOrder: 1, damage: 3, instanceId: 10);
            var second = Card("slash", Side.Player, executionOrder: 2, damage: 5, instanceId: 20);
            state.Zone.Add(first);
            state.Zone.Add(second);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            var resolved = events.OfType<CardResolved>().Where(e => e.CardId == "slash").ToArray();
            Assert.AreEqual(2, resolved.Length);
            Assert.IsTrue(resolved.Any(e => e.InstanceId == 10 && e.DamageDealt == 3));
            Assert.IsTrue(resolved.Any(e => e.InstanceId == 20 && e.DamageDealt == 5));
        }
    }
}
