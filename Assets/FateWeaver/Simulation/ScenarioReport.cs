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
            AppendFatePlays(sb, result);
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
                sb.AppendLine((i + 1) + ". " + card.CardId + " | " + card.Side + " | initiative " + card.Initiative);
            }

            sb.AppendLine();
        }

        private static void AppendFatePlays(StringBuilder sb, ScenarioResult result)
        {
            sb.AppendLine("## Fate Plays");
            foreach (var play in result.Scenario.FatePlays)
            {
                var line = "- " + play.Action.Key + " -> " + play.TargetCardId;
                if (play.SecondaryTargetCardId != null)
                {
                    line += " / " + play.SecondaryTargetCardId;
                }

                line += " (cost " + play.Action.Cost + ")";
                sb.AppendLine(line);
            }

            sb.AppendLine("- Applied: " + result.FatePlayResult.AppliedCount
                + ", Rejected: " + result.FatePlayResult.RejectedIndex
                + ", Spent: " + result.FatePlayResult.FateEnergySpent);
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
            sb.AppendLine("- Player HP: " + result.FinalState.PlayerHp);
            foreach (var enemy in result.FinalState.Enemies)
            {
                sb.AppendLine("- " + enemy.Id + " HP: " + enemy.Hp);
            }
        }
    }
}
