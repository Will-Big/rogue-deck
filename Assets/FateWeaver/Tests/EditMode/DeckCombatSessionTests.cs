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
        private static EnemyIntent Goblin(int executionOrder, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", executionOrder, damage) }
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

            Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "slash")));

            Assert.AreEqual(2, session.FateEnergy);                 // cost 1 spent
            Assert.IsTrue(session.CurrentOrder.Any(c => c.Def.Id == "slash"));
            Assert.AreEqual(0, session.Hand.Count(c => c.Id == "slash")); // moved to discard
        }

        [Test]
        public void Cannot_play_action_card_without_enough_energy()
        {
            // deck of two counters (cost 2 each); energy 3 -> only one is affordable.
            var session = NewSession(new[] { StarterDeck.Counter(), StarterDeck.Counter() }, Goblin(4, 3));
            Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "counter_stance")));  // 3 -> 1
            Assert.IsFalse(session.PlayExecutionCard(HandIndex(session, "counter_stance"))); // 1 < 2, rejected
            Assert.AreEqual(1, session.FateEnergy);
        }

        [Test]
        public void Quick_cut_pulled_to_the_front_lands_the_first_strike_bonus()
        {
            // Enemy at executionOrder 4 acts before the player's cards (base 5) by default.
            var session = NewSession(new[] { StarterDeck.QuickCut(), StarterDeck.PullForward() }, Goblin(4, 3));
            session.PlayExecutionCard(HandIndex(session, "quick_cut")); // placed at executionOrder 5

            // pull_forward (-2) on quick_cut -> executionOrder 3 -> now first.
            var quickIndex = ZoneIndex(session, "quick_cut");
            Assert.IsTrue(session.PlayInterventionCard(HandIndex(session, "pull_forward"), quickIndex));

            var timeline = session.ResolveTurn();
            Assert.AreEqual(8, DamageOf(timeline, "quick_cut")); // first-strike success
        }

        [Test]
        public void Counter_immediately_after_an_enemy_attack_gets_the_bonus()
        {
            var session = NewSession(new[] { StarterDeck.Counter() }, Goblin(6, 3));
            session.PlayExecutionCard(HandIndex(session, "counter_stance"));

            var timeline = session.ResolveTurn();
            Assert.AreEqual(9, DamageOf(timeline, "counter_stance"));
        }

        [Test]
        public void Cover_before_the_enemy_attack_absorbs_it()
        {
            // cover (base 5) resolves before goblin (6); its "next is enemy attack" bonus -> block 7.
            var session = NewSession(new[] { StarterDeck.Cover() }, Goblin(6, 3));
            session.PlayExecutionCard(HandIndex(session, "cover"));

            int hpBefore = session.State.PlayerHp;
            session.ResolveTurn();
            Assert.AreEqual(hpBefore, session.State.PlayerHp); // block 7 fully absorbs the 3 damage
        }

        [Test]
        public void Begin_next_turn_discards_hand_refills_energy_and_redraws()
        {
            var session = NewSession(StarterDeck.Build(), Goblin(4, 3));
            session.PlayExecutionCard(HandIndex(session, FirstExecutionId(session)));
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
                enemyPolicy: intent, fateEnergyPerTurn: 3, handSize: 5, seed: 1);

        private static int ZoneIndex(DeckCombatSession s, string cardId)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Def.Id == cardId) return i;
            }
            return -1;
        }

        private static string FirstExecutionId(DeckCombatSession s)
            => s.Hand.First(c => c.Category == CardCategory.Execution).Id;
    }
}
