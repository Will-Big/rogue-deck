using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class PartyTargetingTests
    {
        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new BlockBehavior());
            r.Register(new VulnerableBehavior());
            return r;
        }

        private static ExecutionCardInstance Card(string id, Side side, EffectData effect)
            => new ExecutionCardInstance(new CardDefinition(id, id, side, 1, new[] { effect }));

        // --- Strict explicit ally / self resolution ----------------------------------------------

        [Test]
        public void Dead_explicit_ally_does_not_fall_back_to_owner_or_front()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            var c = new PartyMember("c", "C", maxHp: 10);
            b.Hp = 0; // the explicit target is dead
            state.Party.Add(a);
            state.Party.Add(b);
            state.Party.Add(c);

            var effect = EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.PartyMember, magnitude: 4);
            var card = Card("aid", Side.Player, effect);
            card.OwnerId = "a";
            card.TargetId = "b";
            var ctx = new EffectContext { Card = card, State = state, Effect = effect, EffectValue = 4 };

            new ApplyStatusHandler().Apply(ctx);

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
            Assert.IsFalse(a.Statuses.Has(StatusKeys.Block), "must not fall back to the owner");
            Assert.IsFalse(state.Party[0].Statuses.Has(StatusKeys.Block), "must not fall back to the front");
        }

        [Test]
        public void Missing_owner_in_multi_party_self_effect_does_not_fall_back_to_front()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            state.Party.Add(a);
            state.Party.Add(b);

            var effect = EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: 4);
            var card = Card("guard", Side.Player, effect);
            // OwnerId intentionally left null.
            var ctx = new EffectContext { Card = card, State = state, Effect = effect, EffectValue = 4 };

            new ApplyStatusHandler().Apply(ctx);

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
            Assert.IsFalse(a.Statuses.Has(StatusKeys.Block), "must not fall back to the front member");
            Assert.IsFalse(b.Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Enemy_self_without_owner_uses_the_only_enemy_for_legacy_runners()
        {
            var state = new CombatState();
            state.Enemies.Add(new Enemy("goblin", 20));

            var effect = EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: 3);
            var card = Card("crude_guard", Side.Enemy, effect);
            var ctx = new EffectContext { Card = card, State = state, Effect = effect, EffectValue = 3 };

            new ApplyStatusHandler().Apply(ctx);

            Assert.IsNull(card.CancellationReason);
            Assert.IsTrue(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Enemy_self_without_owner_cancels_when_multiple_enemies_exist()
        {
            var state = new CombatState();
            state.Enemies.Add(new Enemy("a", 20));
            state.Enemies.Add(new Enemy("b", 20));

            var effect = EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: 3);
            var card = Card("crude_guard", Side.Enemy, effect);
            var ctx = new EffectContext { Card = card, State = state, Effect = effect, EffectValue = 3 };

            new ApplyStatusHandler().Apply(ctx);

            Assert.AreEqual(CardCancellationReason.NoValidTarget, card.CancellationReason);
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            Assert.IsFalse(state.Enemies[1].Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Single_party_member_self_effect_resolves_without_an_owner_id()
        {
            var state = new CombatState();
            var hero = new PartyMember("hero", "Hero", maxHp: 10); // not the solo "player" id
            state.Party.Add(hero);
            state.Enemies.Add(new Enemy("goblin", 20));

            var effect = EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: 4);
            var card = Card("guard", Side.Player, effect);
            // OwnerId left null: with only one party member, Self resolves unambiguously — symmetric
            // with Enemy_self_without_owner_uses_the_only_enemy_for_legacy_runners above.
            var ctx = new EffectContext { Card = card, State = state, Effect = effect, EffectValue = 4 };

            new ApplyStatusHandler().Apply(ctx);

            Assert.IsNull(card.CancellationReason);
            Assert.IsTrue(hero.Statuses.Has(StatusKeys.Block));
        }

        // --- AllPartyMembers independence + per-member damage folding ----------------------------

        [Test]
        public void All_party_status_creates_independent_instances()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 20);
            var b = new PartyMember("b", "B", maxHp: 20);
            state.Party.Add(a);
            state.Party.Add(b);
            state.Enemies.Add(new Enemy("goblin", 20));

            var applyEffect = EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.AllPartyMembers, magnitude: 5);
            var applyCard = Card("guard_all", Side.Player, applyEffect);
            var applyCtx = new EffectContext { Card = applyCard, State = state, Effect = applyEffect, EffectValue = 5 };
            new ApplyStatusHandler().Apply(applyCtx);

            Assert.IsTrue(a.Statuses.Has(StatusKeys.Block));
            Assert.IsTrue(b.Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(5, a.Statuses.Get(StatusKeys.Block).Magnitude);
            Assert.AreEqual(5, b.Statuses.Get(StatusKeys.Block).Magnitude);

            // Consume A's block via an enemy attack that targets the front (A); B's must remain untouched.
            var damageEffect = new EffectData(EffectKeys.Damage, 5) { TargetSelector = TargetSelector.FrontOne };
            var damageCard = Card("smash", Side.Enemy, damageEffect);
            var damageCtx = new EffectContext { Card = damageCard, State = state, Effect = damageEffect, EffectValue = 5, StatusRegistry = Statuses() };
            new DamageHandler().Apply(damageCtx);

            Assert.AreEqual(20, a.Hp); // fully absorbed
            Assert.AreEqual(0, a.Statuses.Get(StatusKeys.Block).Magnitude); // A's charge spent
            Assert.AreEqual(5, b.Statuses.Get(StatusKeys.Block).Magnitude); // B's instance is independent
        }

        [Test]
        public void Block_and_vulnerable_on_a_do_not_modify_damage_to_b()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 20);
            var b = new PartyMember("b", "B", maxHp: 20);
            state.Party.Add(a);
            state.Party.Add(b);

            a.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            a.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, magnitude: 10);

            var damageEffect = new EffectData(EffectKeys.Damage, 4) { TargetSelector = TargetSelector.BackOne };
            var card = Card("smash", Side.Enemy, damageEffect);
            var ctx = new EffectContext { Card = card, State = state, Effect = damageEffect, EffectValue = 4, StatusRegistry = Statuses() };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual("b", ctx.TargetId);
            Assert.AreEqual(4, ctx.DamageDealt); // unmodified by A's vulnerable/block
            Assert.AreEqual(16, b.Hp);
            Assert.AreEqual(20, a.Hp); // A untouched
            Assert.AreEqual(10, a.Statuses.Get(StatusKeys.Block).Magnitude); // A's block untouched
        }

        // --- Independent formations -----------------------------------------------------------

        [Test]
        public void Position_selector_ignores_dead_members_without_reindexing_the_other_side()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            var c = new PartyMember("c", "C", maxHp: 10);
            a.Hp = 0; // front member dead
            state.Party.Add(a);
            state.Party.Add(b);
            state.Party.Add(c);
            state.Enemies.Add(new Enemy("e1", 10));
            state.Enemies.Add(new Enemy("e2", 10));

            var frontOne = PartyTargeting.Select(state, TargetSelector.FrontOne);
            Assert.AreEqual("b", frontOne.Id, "position selection skips the dead front member");

            // The player formation change above must have no bearing on enemy-formation indexing.
            var damageEffect = new EffectData(EffectKeys.Damage, 3);
            var card = Card("slash", Side.Player, damageEffect);
            card.TargetId = "e2";
            var ctx = new EffectContext { Card = card, State = state, Effect = damageEffect, EffectValue = 3 };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual(10, state.Enemies[0].Hp, "e1 untouched");
            Assert.AreEqual(7, state.Enemies[1].Hp, "e2 hit at its original index");
            Assert.AreEqual("e2", ctx.TargetId);
        }

        // --- PartyTargetRules ----------------------------------------------------------------------

        [Test]
        public void RequiresExplicitAllyTarget_is_true_only_for_partymember_status_effects()
        {
            var partyMemberCard = new CardDefinition("aid", "aid", Side.Player, 1,
                new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.PartyMember, 3) });
            var selfCard = new CardDefinition("guard", "guard", Side.Player, 1,
                new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3) });
            var allCard = new CardDefinition("guard_all", "guard_all", Side.Player, 1,
                new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.AllPartyMembers, 3) });
            var damageCard = new CardDefinition("slash", "slash", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 3) });

            Assert.IsTrue(PartyTargetRules.RequiresExplicitAllyTarget(partyMemberCard));
            Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(selfCard));
            Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(allCard));
            Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(damageCard));
        }

        [Test]
        public void IsValidExplicitAllyTarget_rejects_dead_or_missing_ids()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            b.Hp = 0;
            state.Party.Add(a);
            state.Party.Add(b);

            Assert.IsTrue(PartyTargetRules.IsValidExplicitAllyTarget(state, "a"));
            Assert.IsFalse(PartyTargetRules.IsValidExplicitAllyTarget(state, "b"));
            Assert.IsFalse(PartyTargetRules.IsValidExplicitAllyTarget(state, "no-such-id"));
        }
    }
}
