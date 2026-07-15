using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class PartyMemberTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        [Test]
        public void Lethal_damage_is_survived_once_at_one_hp()
        {
            var member = new PartyMember("hero", "Hero", maxHp: 10, surviveCharges: 1);

            var outcome = member.TakeDamage(15);

            Assert.AreEqual(DamageOutcome.DeathsDoor, outcome);
            Assert.AreEqual(1, member.Hp);
            Assert.AreEqual(0, member.SurviveCharges);
            Assert.IsTrue(member.IsAlive);
        }

        [Test]
        public void Second_lethal_damage_kills()
        {
            var member = new PartyMember("hero", "Hero", maxHp: 10, surviveCharges: 1);
            member.TakeDamage(15); // spends the only survive charge, HP -> 1

            var outcome = member.TakeDamage(5);

            Assert.AreEqual(DamageOutcome.Died, outcome);
            Assert.IsFalse(member.IsAlive);
        }

        [Test]
        public void Outcome_is_ongoing_while_any_party_member_lives()
        {
            var state = new CombatState();
            state.Party.Clear();
            state.Party.Add(new PartyMember("a", "A", maxHp: 10));
            state.Party.Add(new PartyMember("b", "B", maxHp: 10));
            state.Enemies.Add(new Enemy("goblin", 12));

            state.Party[0].TakeDamage(10); // A dies, B still alive

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.IsFalse(state.Party[0].IsAlive);
            Assert.IsTrue(state.Party[1].IsAlive);
            Assert.AreEqual(Outcome.Ongoing, ((TurnEnded)events[^1]).Outcome);
        }

        [Test]
        public void Status_bags_are_isolated_between_party_members()
        {
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);

            a.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, magnitude: 4);
            a.Statuses.Add(StatusKeys.Haste, StatusLifetime.Turns(1));

            Assert.IsTrue(a.Statuses.Has(StatusKeys.Block));
            Assert.IsTrue(a.Statuses.Has(StatusKeys.Haste));
            Assert.IsFalse(b.Statuses.Has(StatusKeys.Block));
            Assert.IsFalse(b.Statuses.Has(StatusKeys.Haste));
            Assert.AreEqual(0, b.Statuses.All.Count);
        }

        [Test]
        public void End_of_turn_clears_this_turn_statuses_for_every_party_member()
        {
            var state = new CombatState();
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            a.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, magnitude: 4);
            b.Statuses.Add(StatusKeys.Slow, StatusLifetime.ThisTurn);
            state.Party.Add(a);
            state.Party.Add(b);
            state.Enemies.Add(new Enemy("goblin", 12));

            new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.IsFalse(a.Statuses.Has(StatusKeys.Block));
            Assert.IsFalse(b.Statuses.Has(StatusKeys.Slow));
        }

        [Test]
        public void Legacy_player_shim_keeps_existing_single_player_tests()
        {
            var state = new CombatState { PlayerHp = 30 };

            Assert.AreEqual(1, state.Party.Count);
            Assert.AreEqual(CombatState.LegacyPlayerId, state.Party[0].Id);
            Assert.AreEqual(30, state.Party[0].Hp);
            Assert.AreEqual(30, state.PlayerHp);
            Assert.AreSame(state.Party[0].Statuses, state.PlayerStatuses);

            state.PlayerStatuses.Add(StatusKeys.Haste, StatusLifetime.Turns(1));
            Assert.IsTrue(state.Party[0].Statuses.Has(StatusKeys.Haste));

            state.PlayerHp -= 10;
            Assert.AreEqual(20, state.Party[0].Hp);
        }
    }
}
