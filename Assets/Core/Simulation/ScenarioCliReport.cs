using FateWeaver.Core.Authoring.Statuses;

namespace FateWeaver.Simulation
{
    public static class ScenarioCliReport
    {
        public static string Build(string scenarioId, StatusContentCatalog statusContent)
        {
            if (SampleMultiTurnScenarios.TryFind(scenarioId, out var multiTurnScenario))
            {
                var comparison = new MultiTurnRunner(statusContent).Compare(multiTurnScenario);
                return MultiTurnComparisonReport.ToMarkdown(comparison);
            }

            var singleTurnComparison = new ScenarioRunner(statusContent).Compare(
                SampleScenarios.Find(scenarioId));
            return ScenarioComparisonReport.ToMarkdown(singleTurnComparison);
        }
    }
}
