using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>타임라인을 사람이 읽는 여러 줄로 바꾼다. 코어의 출력은 타임라인뿐이므로(규칙 11)
    /// 로그의 원천도 이것 하나다. 상태·카드 이름은 설명 레지스트리에서 가져온다(규칙 10).</summary>
    public static class TimelineTextFormatter
    {
        public static string Format(
            IReadOnlyList<ResolutionEvent> timeline,
            KoreanDescriptionCatalog catalog)
        {
            if (timeline == null || timeline.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var evt in timeline)
            {
                AppendEvent(sb, evt, catalog);
            }

            return sb.ToString();
        }

        private static void AppendEvent(
            StringBuilder sb, ResolutionEvent evt, KoreanDescriptionCatalog catalog)
        {
            switch (evt)
            {
                case TurnStarted e:
                    sb.Append("== ").Append(e.TurnIndex + 1).AppendLine("턴 시작 ==");
                    break;
                case CardResolved e:
                    sb.Append("  ").Append(e.CardId).Append(" 해결");
                    if (e.TargetId != null) sb.Append(" → ").Append(e.TargetId);
                    sb.Append(" (피해 ").Append(e.DamageDealt).AppendLine(")");
                    foreach (var step in e.DamageSteps)
                    {
                        sb.Append("      ").Append(step.HolderId).Append('의')
                          .Append(StatusName(catalog, step.StatusId))
                          .Append(": ").Append(step.Before).Append(" → ").AppendLine(step.After.ToString());
                    }

                    break;
                case CardCancelled e:
                    sb.Append("  ").Append(e.CardId).Append(" 취소 (").Append(e.Reason).AppendLine(")");
                    break;
                case StatusApplied e:
                    sb.Append("  ").Append(e.HolderId).Append("에게 ")
                      .Append(StatusName(catalog, e.StatusId))
                      .Append(" 부여 (count=").Append(e.Count)
                      .Append(", 수치=").Append(e.Magnitude)
                      .AppendLine(e.Stacked ? ", 합산)" : ")");
                    break;
                case StatusExpired e:
                    sb.Append("  ").Append(e.HolderId).Append('의')
                      .Append(StatusName(catalog, e.StatusId)).AppendLine(" 만료");
                    break;
                case StatusTicked e:
                    sb.Append("  ").Append(e.HolderId).Append('의')
                      .Append(StatusName(catalog, e.StatusId))
                      .Append(" 발동 (피해 ").Append(e.Damage)
                      .Append(", 수치 ").Append(e.Magnitude).AppendLine(")");
                    break;
                case StatusTransferred e:
                    sb.Append("  ").Append(StatusName(catalog, e.StatusId))
                      .Append(' ').Append(e.Magnitude).Append(" 이전: ")
                      .Append(e.FromHolderId).Append(" → ").AppendLine(e.ToHolderId);
                    break;
                case DeathsDoorSurvived e:
                    sb.Append("  ").Append(e.MemberId).AppendLine(" 치명 버팀 발동 (HP 1로 유지)");
                    break;
                case PartyMemberDied e:
                    sb.Append("  ").Append(e.MemberId).AppendLine(" 사망");
                    break;
                case EnemyDied e:
                    sb.Append("  ").Append(e.EnemyId).AppendLine(" 처치");
                    break;
                case TurnEnded e:
                    sb.Append("== ").Append(e.TurnIndex + 1).Append("턴 종료 (")
                      .Append(e.Outcome).AppendLine(") ==");
                    break;
                default:
                    sb.Append("  [미처리 이벤트] ").AppendLine(evt.GetType().Name);
                    break;
            }
        }

        private static string StatusName(KoreanDescriptionCatalog catalog, string statusId)
            => catalog.Statuses.Resolve(new StatusKey(statusId));
    }
}
