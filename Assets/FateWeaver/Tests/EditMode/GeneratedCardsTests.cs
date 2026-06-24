using System.Linq;
using NUnit.Framework;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Generated;

namespace FateWeaver.Tests
{
    public class GeneratedCardsTests
    {
        [Test]
        public void Generated_starter_deck_matches_updated_slash_and_counter_specs()
        {
            var cards = GeneratedCards.StarterDeck();
            var slash = cards.First(c => c.Id == "slash");
            var counter = cards.First(c => c.Id == "counter_stance");

            Assert.AreEqual(4, slash.BaseInitiative);
            Assert.AreEqual(4, slash.Effects.Single().Amount);
            Assert.AreEqual("반격", counter.Name);
            Assert.AreEqual(7, counter.BaseInitiative);
            Assert.AreEqual(4, counter.Effects.Single().Amount);
            Assert.AreEqual(ConditionKind.PrevIsEnemyAttack, counter.Effects.Single().Condition);
            Assert.AreEqual(9, counter.Effects.Single().SuccessAmount);
        }
    }
}
