using FateWeaver.Core.Combat;

namespace FateWeaver.Simulation
{
    public sealed class MultiTurnComparisonResult
    {
        public MultiTurnScenario Scenario { get; }
        public MultiTurnResult Baseline { get; }
        public MultiTurnResult Manipulated { get; }

        public int PlayerHpDelta
            => Manipulated.FinalState.Party[0].Hp - Baseline.FinalState.Party[0].Hp;

        public MultiTurnComparisonResult(
            MultiTurnScenario scenario,
            MultiTurnResult baseline,
            MultiTurnResult manipulated)
        {
            Scenario = scenario;
            Baseline = baseline;
            Manipulated = manipulated;
        }

        public int EnemyHpDelta(string enemyId)
            => EnemyHp(Manipulated.FinalState, enemyId) - EnemyHp(Baseline.FinalState, enemyId);

        private static int EnemyHp(CombatState state, string enemyId)
        {
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id == enemyId)
                {
                    return enemy.Hp;
                }
            }

            throw new System.Collections.Generic.KeyNotFoundException(
                "No enemy found for '" + enemyId + "'");
        }
    }
}
