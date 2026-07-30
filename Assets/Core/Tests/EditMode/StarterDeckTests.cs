using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class StarterDeckTests
    {
        private static readonly string[] SelectedIds =
        {
            "probing_strike", "delayed_strike", "quick_cover", "early_guard",
            "breather", "hasten", "toxic_reclaim", "early_onset", "spore_veil",
            "last_drop"
        };

        [Test]
        public void Build_has_the_fixed_ten_card_composition()
        {
            var cards = StarterDeck.Build();
            CollectionAssert.AreEqual(SelectedIds, cards.Select(card => card.Id).ToArray());
            Assert.AreEqual(10, cards.Select(card => card.Id).Distinct().Count());
            Assert.AreEqual(8, cards.Count(card => card.Category == CardCategory.Execution));
            Assert.AreEqual(2, cards.Count(card => card.Category == CardCategory.Intervention));
        }

        [Test]
        public void Every_intervention_card_cost_matches_its_action_cost()
        {
            foreach (var card in StarterDeck.Build().Where(
                         card => card.Category == CardCategory.Intervention))
            {
                Assert.AreEqual(card.EnergyCost, card.InterventionAction.InterventionCost, card.Id);
            }
        }
    }
}
