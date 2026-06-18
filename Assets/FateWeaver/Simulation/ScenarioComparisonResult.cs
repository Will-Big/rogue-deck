namespace FateWeaver.Simulation
{
    public sealed class ScenarioComparisonResult
    {
        public ScenarioDefinition Scenario { get; }
        public ScenarioResult Baseline { get; }
        public ScenarioResult Manipulated { get; }

        public ScenarioComparisonResult(
            ScenarioDefinition scenario,
            ScenarioResult baseline,
            ScenarioResult manipulated)
        {
            Scenario = scenario;
            Baseline = baseline;
            Manipulated = manipulated;
        }

        public int PlayerHpDelta => Manipulated.FinalState.PlayerHp - Baseline.FinalState.PlayerHp;

        public int EnemyHpDelta(string enemyId)
            => FindEnemyHp(Manipulated, enemyId) - FindEnemyHp(Baseline, enemyId);

        private static int FindEnemyHp(ScenarioResult result, string enemyId)
        {
            foreach (var enemy in result.FinalState.Enemies)
            {
                if (enemy.Id == enemyId)
                {
                    return enemy.Hp;
                }
            }

            throw new System.Collections.Generic.KeyNotFoundException("No enemy found for '" + enemyId + "'");
        }
    }
}
