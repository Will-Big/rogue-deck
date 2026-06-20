using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    /// <summary>Asserts the four balance-doc design invariants against demonstrative scenarios,
    /// using the harness's no-manipulation (baseline) vs scripted (manipulated) comparison.
    /// ① unmanipulated is clearly worse  ② manipulation changes the outcome a lot
    /// ③ action-card combos do not auto-complete  ④ enemy disruption matters.</summary>
    public class DesignInvariantTests
    {
        private static CardResolved Resolved(MultiTurnResult result, string cardId)
            => result.Turns
                .SelectMany(turn => turn.Timeline)
                .OfType<CardResolved>()
                .Single(card => card.CardId == cardId);

        [Test]
        public void Mark_combo_holds_invariants_1_2_and_3()
        {
            var comparison = new MultiTurnRunner().Compare(SampleMultiTurnScenarios.MarkCombo());

            var baselineMark = Resolved(comparison.Baseline, "mark");
            var manipulatedMark = Resolved(comparison.Manipulated, "mark");
            var baselineSlash = Resolved(comparison.Baseline, "slash");
            var manipulatedSlash = Resolved(comparison.Manipulated, "slash");

            // ③ the 2-condition combo does NOT auto-complete: the enemy resolves first, so AllOf fails.
            Assert.AreEqual(ConditionTier.Basic, baselineMark.ConditionTier);
            // ② manipulation (delaying the enemy) completes the combo.
            Assert.AreEqual(ConditionTier.Success, manipulatedMark.ConditionTier);
            // ① unmanipulated is weak; ② manipulated is much stronger.
            Assert.AreEqual(2, baselineSlash.DamageDealt);
            Assert.AreEqual(8, manipulatedSlash.DamageDealt);
            Assert.LessOrEqual(comparison.EnemyHpDelta("goblin"), -6);
        }

        [Test]
        public void Enemy_disruption_changes_the_conditional_reward_invariant_4()
        {
            var comparison = new MultiTurnRunner().Compare(
                SampleMultiTurnScenarios.Chapter8ThreeTurnOpening());

            var baselineQuickCuts = comparison.Baseline.Turns
                .SelectMany(turn => turn.Timeline).OfType<CardResolved>()
                .Where(card => card.CardId.StartsWith("quick_cut")).ToArray();
            var manipulatedQuickCuts = comparison.Manipulated.Turns
                .SelectMany(turn => turn.Timeline).OfType<CardResolved>()
                .Where(card => card.CardId.StartsWith("quick_cut")).ToArray();

            // ④ without manipulation (and with the wrist-cut disruption in play) the reward never lands;
            // with manipulation every quick cut reaches its success reward.
            Assert.IsTrue(baselineQuickCuts.All(card => card.ConditionTier == ConditionTier.Basic));
            Assert.IsTrue(manipulatedQuickCuts.All(card => card.ConditionTier == ConditionTier.Success));
        }
    }
}
