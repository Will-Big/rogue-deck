using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class StarterDeckTests
    {
        [Test]
        public void Has_ten_cards_seven_action_three_fate()
        {
            var cards = StarterDeck.Build();
            Assert.AreEqual(10, cards.Count);
            Assert.AreEqual(7, cards.Count(c => c.Category == CardCategory.Action));
            Assert.AreEqual(3, cards.Count(c => c.Category == CardCategory.Fate));
        }

        [Test]
        public void Contains_expected_counts_by_id()
        {
            var cards = StarterDeck.Build();
            Assert.AreEqual(2, cards.Count(c => c.Id == "slash"));
            Assert.AreEqual(2, cards.Count(c => c.Id == "guard"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "quick_cut"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "heavy_strike"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "cover"));
            Assert.AreEqual(2, cards.Count(c => c.Id == "pull_forward"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "swap_positions"));
        }

        [Test]
        public void Fate_card_cost_matches_its_fate_action_cost()
        {
            var pull = StarterDeck.Build().First(c => c.Id == "pull_forward");
            Assert.AreEqual(CardCategory.Fate, pull.Category);
            Assert.AreEqual(pull.Cost, pull.FateAction.Cost);
        }
    }
}
