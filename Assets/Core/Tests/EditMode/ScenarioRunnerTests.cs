using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class ScenarioRunnerTests
    {
        [Test]
        public void Sample_swap_scenario_turns_quick_cut_into_success()
        {
            var result = new ScenarioRunner(TestContent.Statuses()).Run(SampleScenarios.QuickCutSwap());
            var quickCut = result.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "quick_cut");

            Assert.AreEqual(ConditionTier.Success, quickCut.ConditionTier);
            Assert.AreEqual(10, quickCut.DamageDealt);
            Assert.AreEqual(2, result.FinalState.Enemies[0].Hp);
            Assert.AreEqual(1, result.InterventionPlayResult.AppliedCount);
        }

        [Test]
        public void Markdown_report_includes_order_fate_resolution_and_final_state()
        {
            var result = new ScenarioRunner(TestContent.Statuses()).Run(SampleScenarios.QuickCutSwap());
            var markdown = ScenarioReport.ToMarkdown(result);

            StringAssert.Contains("# Scenario: Quick Cut Swap", markdown);
            StringAssert.Contains("## Initial Order", markdown);
            StringAssert.Contains("enemy_jab", markdown);
            StringAssert.Contains("## Intervention Plays", markdown);
            StringAssert.Contains("swap_execution_order", markdown);
            StringAssert.Contains("## Resolution", markdown);
            StringAssert.Contains("quick_cut | Player | Success | damage 10", markdown);
            StringAssert.Contains("## Final State", markdown);
            StringAssert.Contains("Player HP: 29", markdown);
            StringAssert.Contains("goblin HP: 2", markdown);
        }
    }
}
