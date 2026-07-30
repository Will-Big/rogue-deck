using System.Linq;
using NUnit.Framework;
using FateWeaver.Simulation.Generated;

namespace FateWeaver.Tests
{
    public class GeneratedCardsTests
    {
        [Test]
        public void Generated_snapshots_have_the_fixed_deck_and_complete_pool()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "probing_strike", "delayed_strike", "quick_cover", "early_guard",
                    "breather", "hasten", "toxic_reclaim", "early_onset",
                    "spore_veil", "last_drop"
                },
                GeneratedCards.StarterDeck().Select(card => card.Id).ToArray());
            Assert.AreEqual(22, GeneratedCards.StarterPool().Count);
            Assert.AreEqual(
                22,
                GeneratedCards.StarterPool().Select(card => card.Id).Distinct().Count());
        }
    }
}
