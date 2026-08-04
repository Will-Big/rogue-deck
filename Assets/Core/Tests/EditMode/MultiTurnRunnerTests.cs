using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class MultiTurnRunnerTests
    {
        private static ZoneCardSpec Strike(string id, int damage, int executionOrder = 1)
            => new ZoneCardSpec(id, id, Side.Player, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });

        private static ZoneCardSpec EnemyHit(string id, int damage, int executionOrder = 1)
            => new ZoneCardSpec(id, id, Side.Enemy, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });

        private static TurnScript Turn(params ZoneCardSpec[] cards)
            => new TurnScript(fateEnergy: 3, zoneCards: cards, interventionPlays: new InterventionPlaySpec[0]);

        [Test]
        public void Enemy_and_player_hp_carry_across_turns()
        {
            var scenario = new MultiTurnScenario(
                "carry", playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 20) },
                turns: new[]
                {
                    Turn(Strike("s1", 5)),                            // enemy 20 -> 15
                    Turn(EnemyHit("e1", 3, 1), Strike("s2", 5, 2))    // player 30 -> 27, enemy 15 -> 10
                });

            var result = new MultiTurnRunner(TestContent.Statuses()).Run(scenario);

            Assert.AreEqual(2, result.Turns.Count);
            Assert.AreEqual(10, result.FinalState.Enemies[0].Hp);
            Assert.AreEqual(27, result.FinalState.Party[0].Hp);
            Assert.AreEqual(Outcome.Ongoing, result.Outcome);
        }

        [Test]
        public void Each_turn_resolves_its_own_zone()
        {
            var scenario = new MultiTurnScenario(
                "zones", playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 50) },
                turns: new[] { Turn(Strike("alpha", 4)), Turn(Strike("beta", 4)) });

            var result = new MultiTurnRunner(TestContent.Statuses()).Run(scenario);

            var turn0 = result.Turns[0].Timeline.OfType<CardResolved>().Select(e => e.CardId).ToArray();
            var turn1 = result.Turns[1].Timeline.OfType<CardResolved>().Select(e => e.CardId).ToArray();
            CollectionAssert.Contains(turn0, "alpha");
            CollectionAssert.DoesNotContain(turn0, "beta");
            CollectionAssert.Contains(turn1, "beta");
            CollectionAssert.DoesNotContain(turn1, "alpha");
        }

        [Test]
        public void Loop_stops_when_player_dies()
        {
            var scenario = new MultiTurnScenario(
                "lethal", playerHp: 3,
                enemies: new[] { new EnemySpec("goblin", 20) },
                turns: new[]
                {
                    Turn(EnemyHit("e1", 5)),  // player 3 -> dead => Lose
                    Turn(Strike("s2", 5))     // must NOT run
                });

            var result = new MultiTurnRunner(TestContent.Statuses()).Run(scenario);

            Assert.AreEqual(1, result.Turns.Count);  // stopped after turn 0
            Assert.AreEqual(Outcome.Lose, result.Outcome);
        }

        [Test]
        public void Chapter_8_three_turn_compare_keeps_baseline_basic_and_manipulated_successful()
        {
            var scenario = SampleMultiTurnScenarios.Chapter8ThreeTurnOpening();

            var comparison = new MultiTurnRunner(TestContent.Statuses()).Compare(scenario);

            Assert.AreEqual(3, comparison.Baseline.Turns.Count);
            Assert.AreEqual(3, comparison.Manipulated.Turns.Count);

            var baselineQuickCuts = comparison.Baseline.Turns
                .SelectMany(turn => turn.Timeline)
                .OfType<CardResolved>()
                .Where(card => card.CardId.StartsWith("quick_cut"))
                .ToArray();
            var manipulatedQuickCuts = comparison.Manipulated.Turns
                .SelectMany(turn => turn.Timeline)
                .OfType<CardResolved>()
                .Where(card => card.CardId.StartsWith("quick_cut"))
                .ToArray();

            Assert.AreEqual(3, baselineQuickCuts.Length);
            Assert.AreEqual(3, manipulatedQuickCuts.Length);
            Assert.IsTrue(baselineQuickCuts.All(card => card.ConditionTier == Core.Conditions.ConditionTier.Basic));
            Assert.IsTrue(manipulatedQuickCuts.All(card => card.ConditionTier == Core.Conditions.ConditionTier.Success));
            Assert.LessOrEqual(comparison.EnemyHpDelta("goblin"), -24);
        }

        [Test]
        public void Multi_turn_comparison_report_includes_each_turn_and_final_delta()
        {
            var comparison = new MultiTurnRunner(TestContent.Statuses()).Compare(
                SampleMultiTurnScenarios.Chapter8ThreeTurnOpening());

            var markdown = MultiTurnComparisonReport.ToMarkdown(comparison);

            StringAssert.Contains("# Multi-Turn Scenario Compare: Chapter 8 Three-Turn Opening", markdown);
            StringAssert.Contains("## Baseline", markdown);
            StringAssert.Contains("## Manipulated", markdown);
            StringAssert.Contains("### Turn 1", markdown);
            StringAssert.Contains("### Turn 2", markdown);
            StringAssert.Contains("### Turn 3", markdown);
            StringAssert.Contains("- goblin HP delta: -24", markdown);
        }

        [Test]
        public void Multi_turn_sample_registry_finds_chapter_8_scenario_by_id()
        {
            var scenario = SampleMultiTurnScenarios.Find("chapter-8-three-turn-opening");

            Assert.AreEqual("Chapter 8 Three-Turn Opening", scenario.Name);
            Assert.AreEqual(3, scenario.Turns.Count);
            Assert.IsTrue(SampleMultiTurnScenarios.TryFind(
                "chapter-8-three-turn-opening", out var found));
            Assert.AreEqual(scenario.Id, found.Id);
            Assert.IsFalse(SampleMultiTurnScenarios.TryFind("unknown", out _));
        }

        [Test]
        public void Cli_report_routes_multi_turn_id_to_multi_turn_comparison()
        {
            var markdown = ScenarioCliReport.Build("chapter-8-three-turn-opening", TestContent.Statuses());

            StringAssert.StartsWith(
                "# Multi-Turn Scenario Compare: Chapter 8 Three-Turn Opening",
                markdown);
            StringAssert.Contains("### Turn 3", markdown);
        }

        [Test]
        public void Cli_report_keeps_existing_single_turn_route()
        {
            var markdown = ScenarioCliReport.Build("quick-cut-swap", TestContent.Statuses());

            StringAssert.StartsWith("# Scenario Compare: Quick Cut Swap", markdown);
        }
    }
}
