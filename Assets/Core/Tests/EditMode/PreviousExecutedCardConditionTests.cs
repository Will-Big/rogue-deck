using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>Task 3: PreviousExecutedCardIs / SameTarget skip cancelled cards and look at the
    /// nearest card that actually finished resolution, while AdjacentDirection.Next / NoFollowingCardOfSide
    /// keep looking at the frozen future slot regardless of that slot's eventual cancellation.</summary>
    public class PreviousExecutedCardConditionTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new StunBehavior());
            return r;
        }

        private static CardResolved Resolved(System.Collections.Generic.List<ResolutionEvent> events, string id)
            => events.OfType<CardResolved>().Single(e => e.CardId == id);

        private static ExecutionCardInstance ConditionalCard(
            string id,
            Side side,
            int executionOrder,
            Condition condition,
            int baseDamage,
            int successDamage,
            string targetId = null)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { EffectData.Conditional(EffectKeys.Damage, baseDamage, condition, successDamage) });
            return new ExecutionCardInstance(def) { TargetId = targetId };
        }

        private static ExecutionCardInstance PlainCard(
            string id, Side side, int executionOrder, int damage, string targetId = null)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def) { TargetId = targetId };
        }

        [Test]
        public void Previous_executed_condition_skips_owner_died_card()
        {
            var state = new CombatState();
            state.Party.Clear();
            state.Party.Add(new PartyMember("ally", "Ally", maxHp: 3));
            state.Enemies.Add(new Enemy("goblin", 100));

            // A: enemy attack, resolves, kills "ally" outright.
            var a = PlainCard("a_strike", Side.Enemy, executionOrder: 1, damage: 10);
            // B: owned by ally -> cancelled (OwnerDied) once A kills ally; never actually executes.
            var b = new ExecutionCardInstance(new CardDefinition(
                "b_card", "b_card", Side.Player, 2,
                new[] { new EffectData(EffectKeys.Damage, 1) }))
            { OwnerId = "ally" };
            // C: succeeds only if the "previous executed card" is A (an enemy attack), i.e. B is skipped.
            var c = ConditionalCard("c_card", Side.Player, executionOrder: 3,
                new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage), baseDamage: 0, successDamage: 5);

            state.Zone.Add(a);
            state.Zone.Add(b);
            state.Zone.Add(c);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.AreEqual(CardCancellationReason.OwnerDied, events.OfType<CardCancelled>().Single().Reason);
            var resolvedC = Resolved(events, "c_card");
            Assert.AreEqual(ConditionTier.Success, resolvedC.ConditionTier);
            Assert.AreEqual(5, resolvedC.DamageDealt);
        }

        [Test]
        public void Previous_executed_condition_skips_no_target_and_status_intercepted_cards()
        {
            // --- NoValidTarget case ---
            {
                var state = new CombatState { PlayerHp = 30 };
                state.Enemies.Add(new Enemy("goblin", 100));

                var a = PlainCard("a_hit", Side.Player, executionOrder: 1, damage: 2);
                var b = PlainCard("b_hit", Side.Player, executionOrder: 2, damage: 1, targetId: "no-such-enemy");
                var c = ConditionalCard("c_hit", Side.Player, executionOrder: 3,
                    new PreviousExecutedCardIs(Side.Player), baseDamage: 0, successDamage: 6);

                state.Zone.Add(a);
                state.Zone.Add(b);
                state.Zone.Add(c);

                var events = new TurnResolver(Registry()).Resolve(state, 0);

                Assert.AreEqual(
                    CardCancellationReason.NoValidTarget,
                    events.OfType<CardCancelled>().Single(e => e.CardId == "b_hit").Reason);
                var resolvedC = Resolved(events, "c_hit");
                Assert.AreEqual(ConditionTier.Success, resolvedC.ConditionTier);
                Assert.AreEqual(6, resolvedC.DamageDealt);
            }

            // --- StatusIntercepted case ---
            {
                var state = new CombatState { PlayerHp = 30 };
                state.Enemies.Add(new Enemy("goblin", 100));

                var a = PlainCard("a_hit2", Side.Player, executionOrder: 1, damage: 2);
                var b = PlainCard("b_hit2", Side.Player, executionOrder: 2, damage: 1);
                b.Statuses.Add(StatusKeys.Stun, StatusLifetime.UntilConsumed(1));
                var c = ConditionalCard("c_hit2", Side.Player, executionOrder: 3,
                    new PreviousExecutedCardIs(Side.Player), baseDamage: 0, successDamage: 6);

                state.Zone.Add(a);
                state.Zone.Add(b);
                state.Zone.Add(c);

                var events = new TurnResolver(Registry(), Statuses()).Resolve(state, 0);

                Assert.AreEqual(
                    CardCancellationReason.StatusIntercepted,
                    events.OfType<CardCancelled>().Single(e => e.CardId == "b_hit2").Reason);
                var resolvedC = Resolved(events, "c_hit2");
                Assert.AreEqual(ConditionTier.Success, resolvedC.ConditionTier);
                Assert.AreEqual(6, resolvedC.DamageDealt);
            }
        }

        [Test]
        public void Next_adjacent_condition_keeps_existing_frozen_order_semantics()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 100));

            // A's condition looks at the frozen next slot (B). B later gets cancelled by Stun, but
            // that must not retroactively change A's already-evaluated tier.
            var a = ConditionalCard("a_card", Side.Player, executionOrder: 1,
                new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
                baseDamage: 1, successDamage: 9);
            var b = PlainCard("b_card", Side.Enemy, executionOrder: 2, damage: 3);
            b.Statuses.Add(StatusKeys.Stun, StatusLifetime.UntilConsumed(1));

            state.Zone.Add(a);
            state.Zone.Add(b);

            var events = new TurnResolver(Registry(), Statuses()).Resolve(state, 0);

            var resolvedA = Resolved(events, "a_card");
            Assert.AreEqual(ConditionTier.Success, resolvedA.ConditionTier);
            Assert.AreEqual(9, resolvedA.DamageDealt);
            Assert.AreEqual(
                CardCancellationReason.StatusIntercepted,
                events.OfType<CardCancelled>().Single(e => e.CardId == "b_card").Reason);
        }

        [Test]
        public void Same_target_uses_last_executed_player_card()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblinA", 100));

            var a = PlainCard("a_mark", Side.Player, executionOrder: 1, damage: 1, targetId: "goblinA");
            // b targets a nonexistent enemy -> cancelled (NoValidTarget), never actually resolves.
            var b = PlainCard("b_mark", Side.Player, executionOrder: 2, damage: 1, targetId: "phantom");
            var c = ConditionalCard("c_strike", Side.Player, executionOrder: 3,
                new SameTarget(), baseDamage: 0, successDamage: 8, targetId: "goblinA");

            state.Zone.Add(a);
            state.Zone.Add(b);
            state.Zone.Add(c);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            Assert.AreEqual(
                CardCancellationReason.NoValidTarget,
                events.OfType<CardCancelled>().Single().Reason);
            var resolvedC = Resolved(events, "c_strike");
            Assert.AreEqual(ConditionTier.Success, resolvedC.ConditionTier);
            Assert.AreEqual(8, resolvedC.DamageDealt);
        }

        [Test]
        public void No_preceding_ignores_cancelled_cards_but_no_following_keeps_frozen_future_slots()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 100));

            var q = ConditionalCard("q_card", Side.Player, executionOrder: 1,
                new NoFollowingCardOfSide(Side.Enemy), baseDamage: 0, successDamage: 3);
            var y = PlainCard("y_card", Side.Enemy, executionOrder: 2, damage: 2);
            y.CancellationReason = CardCancellationReason.NoValidTarget; // pre-cancelled before the turn even starts
            var p = ConditionalCard("p_card", Side.Player, executionOrder: 3,
                new NoPrecedingCardOfSide(Side.Enemy), baseDamage: 0, successDamage: 7);

            state.Zone.Add(q);
            state.Zone.Add(y);
            state.Zone.Add(p);

            var events = new TurnResolver(Registry()).Resolve(state, 0);

            // Y still occupies a future enemy slot positionally, even though it's already cancelled ->
            // NoFollowingCardOfSide(Enemy) on Q must still see it and stay at Basic.
            var resolvedQ = Resolved(events, "q_card");
            Assert.AreEqual(ConditionTier.Basic, resolvedQ.ConditionTier);
            Assert.AreEqual(0, resolvedQ.DamageDealt);

            Assert.AreEqual(
                CardCancellationReason.NoValidTarget,
                events.OfType<CardCancelled>().Single(e => e.CardId == "y_card").Reason);

            // Y never actually preceded anything (it was cancelled) -> NoPrecedingCardOfSide(Enemy)
            // on P succeeds.
            var resolvedP = Resolved(events, "p_card");
            Assert.AreEqual(ConditionTier.Success, resolvedP.ConditionTier);
            Assert.AreEqual(7, resolvedP.DamageDealt);
        }
    }
}
