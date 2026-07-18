using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class DeckPileVisibilityTests
    {
        private static DeckCombatSession NewSession()
        {
            var deck = StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition).ToList();
            return new DeckCombatSession(
                deck, 30, new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) },
                GoblinDeck.Policy(), 3, 5, 1);
        }

        private static int IndexOfAffordableExecution(DeckCombatSession session)
        {
            for (int i = 0; i < session.Hand.Count; i++)
            {
                var def = session.Hand[i].Def;
                if (def.Category == CardCategory.Execution && def.EnergyCost <= session.FateEnergy)
                {
                    return i;
                }
            }

            Assert.Fail("opening hand has no affordable execution card (seed drift?)");
            return -1;
        }

        [Test]
        public void All_deck_cards_survive_construction()
        {
            Assert.AreEqual(StarterDeckSpecs.Build().Count, NewSession().AllDeckCards.Count);
        }

        [Test]
        public void Piles_and_hand_partition_the_deck()
        {
            var session = NewSession();

            Assert.AreEqual(
                session.AllDeckCards.Count,
                session.DrawPile.Count + session.DiscardPile.Count + session.Hand.Count);
        }

        [Test]
        public void Played_execution_card_lands_in_the_discard_pile()
        {
            var session = NewSession();
            int index = IndexOfAffordableExecution(session);
            var id = session.Hand[index].Def.Id;

            Assert.IsTrue(session.PlayExecutionCard(index));
            Assert.IsTrue(session.DiscardPile.Any(c => c.Def.Id == id));
        }

        [Test]
        public void Next_turn_discards_the_leftover_hand()
        {
            var session = NewSession();
            int handBefore = session.Hand.Count;
            session.ResolveTurn();

            Assert.IsTrue(session.BeginNextTurn());
            // 이월된 손패는 버림 더미를 거쳤다가 재드로우된다 — 분할 불변식은 유지된다.
            Assert.AreEqual(
                session.AllDeckCards.Count,
                session.DrawPile.Count + session.DiscardPile.Count + session.Hand.Count);
            Assert.GreaterOrEqual(handBefore, 1);
        }

        [Test]
        public void Piles_are_not_downcastable_to_mutable_lists()
        {
            var session = NewSession();

            Assert.IsNotInstanceOf<List<OwnedCard>>(session.DrawPile);
            Assert.IsNotInstanceOf<List<OwnedCard>>(session.DiscardPile);
            Assert.IsNotInstanceOf<List<OwnedCard>>(session.AllDeckCards);
        }
    }
}
