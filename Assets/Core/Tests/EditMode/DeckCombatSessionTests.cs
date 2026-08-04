using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
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
                if (s.Hand[i].Def.Id == id) return i;
            }
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> timeline, string cardId)
            => timeline.OfType<CardResolved>().First(e => e.CardId == cardId).DamageDealt;

        [Test]
        public void Turn_starts_with_enemy_intent_only_and_player_cards_stay_in_hand()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            CollectionAssert.AreEqual(new[] { "goblin_jab" }, session.CurrentOrder.Select(c => c.Def.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "slash" }, session.Hand.Select(c => c.Def.Id).ToArray());
        }

        [Test]
        public void Playing_an_execution_card_places_it_and_spends_energy()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));
            Assert.AreEqual(3, session.FateEnergy);

            Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "slash")));

            Assert.AreEqual(2, session.FateEnergy);                 // cost 1 spent
            Assert.IsTrue(session.CurrentOrder.Any(c => c.Def.Id == "slash"));
            Assert.AreEqual(0, session.Hand.Count(c => c.Def.Id == "slash")); // moved to discard
        }

        [Test]
        public void Cannot_play_execution_card_without_enough_energy()
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
            // Enemy at executionOrder 5 acts after the player's cards (base 5) by default.
            var session = NewSession(new[] { StarterDeck.QuickCut(), StarterDeck.PullForward() }, Goblin(5, 3));
            session.PlayExecutionCard(HandIndex(session, "quick_cut")); // placed at executionOrder 5

            // pull_forward (-1) on quick_cut -> executionOrder 4 -> now first.
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

            int hpBefore = session.State.Party[0].Hp;
            session.ResolveTurn();
            Assert.AreEqual(hpBefore, session.State.Party[0].Hp); // block 7 fully absorbs the 3 damage
        }

        [Test]
        public void Solo_session_player_is_an_explicit_party_member()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            Assert.AreEqual(1, session.State.Party.Count);
            Assert.AreEqual(CombatState.SoloPlayerId, session.State.Party[0].Id);
            Assert.AreEqual(30, session.State.Party[0].Hp);
            Assert.AreEqual(30, session.State.Party[0].MaxHp);
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
            CollectionAssert.AreEqual(new[] { "goblin_jab" }, session.CurrentOrder.Select(c => c.Def.Id).ToArray());
        }

        // --- helpers ---

        [Test]
        public void Describe_targeting_answers_none_for_execution_cards()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            Assert.AreEqual(TargetKind.None,
                session.DescribeTargeting(HandIndex(session, "slash")).Kind);
        }

        [Test]
        public void Describe_targeting_answers_one_rail_card_for_pull_forward()
        {
            var session = NewSession(new[] { StarterDeck.PullForward() }, Goblin(4, 3));

            var req = session.DescribeTargeting(HandIndex(session, "pull_forward"));

            Assert.AreEqual(TargetKind.RailCard, req.Kind);
            Assert.AreEqual(1, req.Count);
        }

        [Test]
        public void Describe_targeting_answers_two_rail_cards_for_swap()
        {
            var session = NewSession(new[] { StarterDeck.SwapPositions() }, Goblin(4, 3));

            var req = session.DescribeTargeting(HandIndex(session, "swap_positions"));

            Assert.AreEqual(TargetKind.RailCard, req.Kind);
            Assert.AreEqual(2, req.Count);
            Assert.IsFalse(req.AllowDuplicates);
        }

        [Test]
        public void Swap_with_the_same_target_twice_is_rejected_without_spending_anything()
        {
            var session = NewSession(
                new[] { StarterDeck.QuickCut(), StarterDeck.SwapPositions() }, Goblin(5, 3));
            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            int energyBefore = session.FateEnergy;
            int handBefore = session.Hand.Count;
            var orderBefore = session.CurrentOrder.Select(c => c.InstanceId).ToArray();
            int quickIndex = ZoneIndex(session, "quick_cut");

            bool played = session.PlayInterventionCard(
                HandIndex(session, "swap_positions"), quickIndex, quickIndex);

            Assert.IsFalse(played);
            Assert.AreEqual(energyBefore, session.FateEnergy);
            Assert.AreEqual(handBefore, session.Hand.Count);
            CollectionAssert.AreEqual(orderBefore,
                session.CurrentOrder.Select(c => c.InstanceId).ToArray());
        }

        [Test]
        public void Describe_targeting_answers_none_for_out_of_range_indexes()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            Assert.AreEqual(TargetKind.None, session.DescribeTargeting(-1).Kind);
            Assert.AreEqual(TargetKind.None, session.DescribeTargeting(99).Kind);
        }

        private static DeckCombatSession NewSession(
            IReadOnlyList<CardDefinition> deck, EnemyIntent intent)
            => new DeckCombatSession(TestContent.Statuses(),
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
            => s.Hand.First(c => c.Def.Category == CardCategory.Execution).Def.Id;
    }
}
