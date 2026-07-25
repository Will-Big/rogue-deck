using System.Text;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation
{
    public static class ScenarioReport
    {
        public static string ToMarkdown(ScenarioResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Scenario: " + result.Scenario.Name);
            sb.AppendLine();
            AppendOrder(sb, "Initial Order", result.InitialOrder);
            AppendInterventionPlays(sb, result);
            AppendOrder(sb, "Manipulated Order", result.ManipulatedOrder);
            AppendResolution(sb, result);
            AppendFinalState(sb, result);
            return sb.ToString();
        }

        private static void AppendOrder(StringBuilder sb, string title, System.Collections.Generic.IReadOnlyList<OrderCardSummary> order)
        {
            sb.AppendLine("## " + title);
            for (int i = 0; i < order.Count; i++)
            {
                var card = order[i];
                sb.AppendLine((i + 1) + ". " + card.CardId + " | " + card.Side + " | execution order " + card.ExecutionOrder);
            }

            sb.AppendLine();
        }

        private static void AppendInterventionPlays(StringBuilder sb, ScenarioResult result)
        {
            sb.AppendLine("## Intervention Plays");
            foreach (var play in result.Scenario.InterventionPlays)
            {
                var line = "- " + play.Intervention.Key + " -> " + play.TargetCardId;
                if (play.SecondaryTargetCardId != null)
                {
                    line += " / " + play.SecondaryTargetCardId;
                }

                line += " (intervention cost " + play.Intervention.InterventionCost + ")";
                sb.AppendLine(line);
            }

            sb.AppendLine("- Applied: " + result.InterventionPlayResult.AppliedCount
                + ", Rejected: " + result.InterventionPlayResult.RejectedIndex
                + ", Spent: " + result.InterventionPlayResult.FateEnergySpent);
            sb.AppendLine();
        }

        private static void AppendResolution(StringBuilder sb, ScenarioResult result)
        {
            sb.AppendLine("## Resolution");
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

        private static void AppendFinalState(StringBuilder sb, ScenarioResult result)
        {
            sb.AppendLine("## Final State");
            sb.AppendLine("- Player HP: " + result.FinalState.Party[0].Hp);
            foreach (var enemy in result.FinalState.Enemies)
            {
                sb.AppendLine("- " + enemy.Id + " HP: " + enemy.Hp);
            }
        }
    }
}
