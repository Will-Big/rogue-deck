using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class DeckCombatSessionTests
    {
        private static EnemyIntent Goblin(int initiative, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", initiative, damage) }
            });

        private static int HandIndex(DeckCombatSession s, string id)
        {
            for (int i = 0; i < s.Hand.Count; i++)
            {
                if (s.Hand[i].Id == id) return i;
            }
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> timeline, string cardId)
            => timeline.OfType<CardResolved>().First(e => e.CardId == cardId).DamageDealt;

        [Test]
        public void Playing_an_action_card_places_it_and_spends_energy()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));
            Assert.AreEqual(3, session.FateEnergy);

            Assert.IsTrue(session.PlayActionCard(HandIndex(session, "slash")));

            Assert.AreEqual(2, session.FateEnergy);                 // cost 1 spent
            Assert.IsTrue(session.CurrentOrder.Any(c => c.Def.Id == "slash"));
            Assert.AreEqual(0, session.Hand.Count(c => c.Id == "slash")); // moved to discard
        }

        [Test]
        public void Cannot_play_action_card_without_enough_energy()
        {
            // deck of two heavy strikes (cost 2 each); energy 3 -> only one is affordable.
            var session = NewSession(new[] { StarterDeck.HeavyStrike(), StarterDeck.HeavyStrike() }, Goblin(4, 3));
            Assert.IsTrue(session.PlayActionCard(HandIndex(session, "heavy_strike")));  // 3 -> 1
            Assert.IsFalse(session.PlayActionCard(HandIndex(session, "heavy_strike"))); // 1 < 2, rejected
            Assert.AreEqual(1, session.FateEnergy);
        }

        [Test]
        public void Quick_cut_pulled_to_the_front_lands_the_first_strike_bonus()
        {
            // Enemy at initiative 4 acts before the player's cards (base 5) by default.
            var session = NewSession(new[] { StarterDeck.QuickCut(), StarterDeck.PullForward() }, Goblin(4, 3));
            session.PlayActionCard(HandIndex(session, "quick_cut")); // placed at initiative 5

            // pull_forward (-2) on quick_cut -> initiative 3 -> now first.
            var quickIndex = ZoneIndex(session, "quick_cut");
            Assert.IsTrue(session.PlayFateCard(HandIndex(session, "pull_forward"), quickIndex));

            var timeline = session.ResolveTurn();
            Assert.AreEqual(8, DamageOf(timeline, "quick_cut")); // first-strike success
        }

        [Test]
        public void Heavy_strike_after_an_ally_attack_gets_the_combo_bonus()
        {
            var session = NewSession(new[] { StarterDeck.Slash(), StarterDeck.HeavyStrike() }, new EnemyIntent(
                new List<IReadOnlyList<CardDefinition>>())); // no enemy this turn
            session.PlayActionCard(HandIndex(session, "slash"));        // initiative 5 (placed first)
            session.PlayActionCard(HandIndex(session, "heavy_strike")); // initiative 5 (placed second -> after slash)

            var timeline = session.ResolveTurn();
            Assert.AreEqual(10, DamageOf(timeline, "heavy_strike")); // prev is a player attack -> +5
        }

        [Test]
        public void Cover_before_the_enemy_attack_absorbs_it()
        {
            // cover (base 5) resolves before goblin (6); its "next is enemy attack" bonus -> block 7.
            var session = NewSession(new[] { StarterDeck.Cover() }, Goblin(6, 3));
            session.PlayActionCard(HandIndex(session, "cover"));

            int hpBefore = session.State.PlayerHp;
            session.ResolveTurn();
            Assert.AreEqual(hpBefore, session.State.PlayerHp); // block 7 fully absorbs the 3 damage
        }

        [Test]
        public void Begin_next_turn_discards_hand_refills_energy_and_redraws()
        {
            var session = NewSession(StarterDeck.Build(), Goblin(4, 3));
            session.PlayActionCard(HandIndex(session, FirstActionId(session)));
            session.ResolveTurn();
            Assert.IsTrue(session.CurrentTurnResolved);

            Assert.IsTrue(session.BeginNextTurn());
            Assert.AreEqual(1, session.TurnIndex);
            Assert.AreEqual(3, session.FateEnergy);            // refilled
            Assert.IsFalse(session.CurrentTurnResolved);
            Assert.AreEqual(5, session.Hand.Count);            // fresh hand of 5
        }

        // --- helpers ---

        private static DeckCombatSession NewSession(
            IReadOnlyList<CardDefinition> deck, EnemyIntent intent)
            => new DeckCombatSession(
                deck, playerHp: 30,
                enemies: new[] { new Enemy("goblin", 100) },
                intent: intent, fateEnergyPerTurn: 3, handSize: 5, seed: 1);

        private static int ZoneIndex(DeckCombatSession s, string cardId)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Def.Id == cardId) return i;
            }
            return -1;
        }

        private static string FirstActionId(DeckCombatSession s)
            => s.Hand.First(c => c.Category == CardCategory.Action).Id;
    }
}
