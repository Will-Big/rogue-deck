namespace FateWeaver.Simulation
{
    public static class ScenarioCliReport
    {
        public static string Build(string scenarioId)
        {
            if (SampleMultiTurnScenarios.TryFind(scenarioId, out var multiTurnScenario))
            {
                var comparison = new MultiTurnRunner().Compare(multiTurnScenario);
                return MultiTurnComparisonReport.ToMarkdown(comparison);
            }

            var singleTurnComparison = new ScenarioRunner().Compare(
                SampleScenarios.Find(scenarioId));
            return ScenarioComparisonReport.ToMarkdown(singleTurnComparison);
        }
    }
}
