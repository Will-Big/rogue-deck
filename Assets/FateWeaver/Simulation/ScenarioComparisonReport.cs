using System.Text;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation
{
    public static class ScenarioComparisonReport
    {
        public static string ToMarkdown(ScenarioComparisonResult comparison)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Scenario Compare: " + comparison.Scenario.Name);
            sb.AppendLine();
            AppendResolution(sb, "Baseline Resolution", comparison.Baseline);
            AppendResolution(sb, "Manipulated Resolution", comparison.Manipulated);
            AppendDelta(sb, comparison);
            return sb.ToString();
        }

        private static void AppendResolution(StringBuilder sb, string title, ScenarioResult result)
        {
            sb.AppendLine("## " + title);
            foreach (var evt in result.Timeline)
            {
                var resolved = evt as CardResolved;
                if (resolved == null)
                {
                    continue;
                }

                sb.AppendLine("- " + resolved.CardId
                    + " | " + resolved.Side
                    + " | " + resolved.ConditionTier
                    + " | damage " + resolved.DamageDealt);
            }

            sb.AppendLine();
        }

        private static void AppendDelta(StringBuilder sb, ScenarioComparisonResult comparison)
        {
            sb.AppendLine("## Delta");
            sb.AppendLine("- Player HP delta: " + comparison.PlayerHpDelta);
            foreach (var enemy in comparison.Scenario.Enemies)
            {
                sb.AppendLine("- " + enemy.Id + " HP delta: " + comparison.EnemyHpDelta(enemy.Id));
            }
        }
    }
}
