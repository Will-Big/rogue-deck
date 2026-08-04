using NUnit.Framework;
using FateWeaver.Core.Cards;

namespace FateWeaver.Tests
{
    public class StarterDeckTests
    {
        [Test]
        public void StarterDeckHasTenDistinctCards()
        {
            var deck = TestContent.Content().Decks.Get("starter");

            Assert.AreEqual(10, deck.Count);
            CollectionAssert.AllItemsAreUnique(deck);
        }

        [Test]
        public void EveryInterventionCardCostMatchesItsActionCost()
        {
            foreach (var card in TestContent.StarterDeckCards())
            {
                if (card.Category != CardCategory.Intervention)
                {
                    continue;
                }

                Assert.AreEqual(
                    card.EnergyCost, card.InterventionAction.InterventionCost, card.Id);
            }
        }
    }
}
