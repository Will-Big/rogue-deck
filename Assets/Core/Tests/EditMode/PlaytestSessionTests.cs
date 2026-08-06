using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class PlaytestSessionTests
    {
        [Test]
        public void Player_can_swap_selected_cards_and_resolve_the_manipulated_turn()
        {
            var session = new PlaytestSession(SampleScenarios.QuickCutSwap(), TestContent.Statuses());

            CollectionAssert.AreEqual(
                new[] { "enemy_jab", "quick_cut" },
                session.CurrentOrder.Select(card => card.Def.Id).ToArray());

            var interventionResult = session.ApplyInterventionAction(
                new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, new SwapExecutionOrderPayload(TargetSide: null, RequireAdjacent: false)),
                "enemy_jab",
                "quick_cut");

            Assert.AreEqual(1, interventionResult.AppliedCount);
            Assert.AreEqual(2, session.State.FateEnergy);
            CollectionAssert.AreEqual(
                new[] { "quick_cut", "enemy_jab" },
                session.CurrentOrder.Select(card => card.Def.Id).ToArray());

            var timeline = session.Resolve();
            var quickCut = timeline.OfType<CardResolved>().Single(card => card.CardId == "quick_cut");
            Assert.AreEqual(ConditionTier.Success, quickCut.ConditionTier);
            Assert.AreEqual(10, quickCut.DamageDealt);
            Assert.IsTrue(session.IsResolved);
        }
    }
}
