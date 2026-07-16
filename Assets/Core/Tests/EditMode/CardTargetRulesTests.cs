using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Presentation;

namespace FateWeaver.Tests
{
    public class CardTargetRulesTests
    {
        private static CardDefinition Card(string id)
        {
            var def = StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)
                .FirstOrDefault(card => card.Id == id);
            Assert.IsNotNull(def, "starter deck is missing card: " + id);
            return def;
        }

        [Test]
        public void Execution_card_needs_no_rail_targets()
        {
            Assert.AreEqual(0, CardTargetRules.RequiredRailTargets(Card("slash")));
        }

        [Test]
        public void Single_target_intervention_needs_one_rail_target()
        {
            Assert.AreEqual(1, CardTargetRules.RequiredRailTargets(Card("pull_forward")));
        }

        [Test]
        public void Swap_intervention_needs_two_rail_targets()
        {
            Assert.AreEqual(2, CardTargetRules.RequiredRailTargets(Card("swap_positions")));
        }

        [Test]
        public void Null_definition_needs_no_rail_targets()
        {
            Assert.AreEqual(0, CardTargetRules.RequiredRailTargets(null));
        }
    }
}
