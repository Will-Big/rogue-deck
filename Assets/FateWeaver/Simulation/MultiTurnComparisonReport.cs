using System.Text;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation
{
    public static class MultiTurnComparisonReport
    {
        public static string ToMarkdown(MultiTurnComparisonResult comparison)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Multi-Turn Scenario Compare: " + comparison.Scenario.Name);
            sb.AppendLine();
            AppendRun(sb, "Baseline", comparison.Baseline);
            AppendRun(sb, "Manipulated", comparison.Manipulated);
            AppendDelta(sb, comparison);
            return sb.ToString();
        }

        private static void AppendRun(StringBuilder sb, string title, MultiTurnResult result)
        {
            sb.AppendLine("## " + title);
            foreach (var turn in result.Turns)
            {
                sb.AppendLine("### Turn " + (turn.TurnIndex + 1));
                sb.AppendLine("- Initial order: " + Order(turn.InitialOrder));
                sb.AppendLine("- Resolved order: " + Order(turn.ManipulatedOrder));
                sb.AppendLine("- Fate spent: " + turn.FatePlayResult.FateEnergySpent);

                foreach (var evt in turn.Timeline)
                {
                    if (evt is CardResolved card)
                    {
                        sb.AppendLine("- " + card.CardId
                            + " | " + card.Side
                            + " | " + card.ConditionTier
                            + " | damage " + card.DamageDealt);
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine("### Final State");
            sb.AppendLine("- Player HP: " + result.FinalState.PlayerHp);
            foreach (var enemy in result.FinalState.Enemies)
            {
                sb.AppendLine("- " + enemy.Id + " HP: " + enemy.Hp);
            }

            sb.AppendLine();
        }

        private static string Order(System.Collections.Generic.IReadOnlyList<OrderCardSummary> cards)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" -> ");
                }

                sb.Append(cards[i].CardId);
                sb.Append("(");
                sb.Append(cards[i].Initiative);
                sb.Append(")");
            }

            return sb.ToString();
        }

        private static void AppendDelta(StringBuilder sb, MultiTurnComparisonResult comparison)
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
