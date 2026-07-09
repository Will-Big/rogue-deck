using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class SampleScenarioTests
    {
        [Test]
        public void Sample_registry_finds_known_scenarios_by_id()
        {
            Assert.AreEqual("Quick Cut Swap", SampleScenarios.Find("quick-cut-swap").Name);
            Assert.AreEqual("Reward Nullified", SampleScenarios.Find("reward-nullified").Name);
            Assert.AreEqual(
                "Chapter 8 Auto-Combo Guard",
                SampleScenarios.Find("chapter-8-auto-combo-guard").Name);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "quick-cut-swap",
                    "reward-nullified",
                    "chapter-8-auto-combo-guard"
                },
                SampleScenarios.All.Select(s => s.Id).ToArray());
        }

        [Test]
        public void Reward_nullified_sample_shows_enemy_disruption_reduces_success_reward()
        {
            var comparison = new ScenarioRunner().Compare(SampleScenarios.RewardNullified());
            var baselineQuickCut = comparison.Baseline.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "quick_cut");
            var manipulatedQuickCut = comparison.Manipulated.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "quick_cut");

            Assert.AreEqual(ConditionTier.Success, baselineQuickCut.ConditionTier);
            Assert.AreEqual(10, baselineQuickCut.DamageDealt);
            Assert.AreEqual(2, comparison.Baseline.FinalState.Enemies[0].Hp);

            Assert.AreEqual(ConditionTier.Basic, manipulatedQuickCut.ConditionTier);
            Assert.AreEqual(2, manipulatedQuickCut.DamageDealt);
            Assert.AreEqual(10, comparison.Manipulated.FinalState.Enemies[0].Hp);
            Assert.AreEqual(8, comparison.EnemyHpDelta("goblin"));
        }

        [Test]
        public void Chapter_8_guard_requires_intervention_to_restore_the_mark_chain_combo()
        {
            var comparison = new ScenarioRunner().Compare(
                SampleScenarios.Chapter8AutoComboGuard());
            var baselineMark = comparison.Baseline.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "mark_target");
            var manipulatedMark = comparison.Manipulated.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "mark_target");
            var baselineChain = comparison.Baseline.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "chain_slash");
            var manipulatedChain = comparison.Manipulated.Timeline.OfType<CardResolved>()
                .Single(e => e.CardId == "chain_slash");

            Assert.AreEqual(ConditionTier.Basic, baselineMark.ConditionTier);
            Assert.AreEqual(ConditionTier.Success, manipulatedMark.ConditionTier);
            Assert.AreEqual(6, baselineChain.DamageDealt);
            Assert.AreEqual(12, manipulatedChain.DamageDealt);
            Assert.AreEqual(-6, comparison.EnemyHpDelta("goblin"));
        }
    }
}
