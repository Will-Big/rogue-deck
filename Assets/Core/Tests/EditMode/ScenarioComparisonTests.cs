using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class ScenarioComparisonTests
    {
        [Test]
        public void Compare_runs_baseline_without_intervention_plays_and_scripted_with_intervention_plays()
        {
            var comparison = new ScenarioRunner().Compare(SampleScenarios.QuickCutSwap());

            var baselineQuickCut = comparison.Baseline.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "quick_cut");
            var manipulatedQuickCut = comparison.Manipulated.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "quick_cut");

            Assert.AreEqual(ConditionTier.Basic, baselineQuickCut.ConditionTier);
            Assert.AreEqual(2, baselineQuickCut.DamageDealt);
            Assert.AreEqual(10, comparison.Baseline.FinalState.Enemies[0].Hp);

            Assert.AreEqual(ConditionTier.Success, manipulatedQuickCut.ConditionTier);
            Assert.AreEqual(10, manipulatedQuickCut.DamageDealt);
            Assert.AreEqual(2, comparison.Manipulated.FinalState.Enemies[0].Hp);

            Assert.AreEqual(0, comparison.PlayerHpDelta);
            Assert.AreEqual(-8, comparison.EnemyHpDelta("goblin"));
        }

        [Test]
        public void Comparison_report_includes_baseline_manipulated_and_hp_deltas()
        {
            var comparison = new ScenarioRunner().Compare(SampleScenarios.QuickCutSwap());
            var markdown = ScenarioComparisonReport.ToMarkdown(comparison);

            StringAssert.Contains("# Scenario Compare: Quick Cut Swap", markdown);
            StringAssert.Contains("## Baseline Resolution", markdown);
            StringAssert.Contains("quick_cut | Player | Basic | damage 2", markdown);
            StringAssert.Contains("## Manipulated Resolution", markdown);
            StringAssert.Contains("quick_cut | Player | Success | damage 10", markdown);
            StringAssert.Contains("## Delta", markdown);
            StringAssert.Contains("Player HP delta: 0", markdown);
            StringAssert.Contains("goblin HP delta: -8", markdown);
        }
    }
}
