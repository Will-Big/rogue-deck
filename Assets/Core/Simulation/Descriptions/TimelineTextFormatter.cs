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

        /// <summary>이벤트 1건을 로그 1건으로 바꾼다. Unity는 이벤트마다 이것을 호출해
        /// Debug.Log 1건씩 남긴다 (로그 1건 = 이벤트 1건).</summary>
        public static string FormatEvent(ResolutionEvent evt, KoreanDescriptionCatalog catalog)
        {
            if (evt == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            AppendEvent(sb, evt, catalog);
            return sb.ToString().TrimEnd('\r', '\n');
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
                case HpChanged e:
                    sb.Append("  ").Append(e.HolderId).Append(" HP ")
                      .Append(e.Before).Append(" → ").Append(e.After)
                      .Append(" (").Append(HpSourceName(catalog, e)).AppendLine(")");
                    break;
                case FateEnergyGained e:
                    sb.Append("  다음 턴 운명력 +").Append(e.Amount)
                      .Append(" (").Append(e.SourceCardId).AppendLine(")");
                    break;
                case StatusConsumed e:
                    sb.Append("  ").Append(e.HolderId).Append('의')
                      .Append(StatusName(catalog, e.StatusId))
                      .Append(' ').Append(e.Amount).AppendLine(" 소비");
                    break;
                case CardBuffGranted e:
                    sb.Append("  카드 ").Append(e.CardId).Append("(#").Append(e.CardInstanceId)
                      .Append(")에 ").Append(BuffName(catalog, e.BuffId))
                      .Append(" +").Append(e.Amount).AppendLine(" 부여");
                    break;
                case CardBuffConsumed e:
                    sb.Append("  카드 ").Append(e.CardId).Append("(#").Append(e.CardInstanceId)
                      .Append(")의 ").Append(BuffName(catalog, e.BuffId))
                      .Append(' ').Append(e.Amount).AppendLine(" 소모");
                    break;
                case FormationMoved e:
                    sb.Append("  ").Append(e.MemberId).Append(" 대형 이동: ")
                      .Append(e.FromIndex + 1).Append("열 → ")
                      .Append(e.ToIndex + 1).AppendLine("열");
                    break;
                case CardCancelled e:
                    sb.Append("  ").Append(e.CardId).Append(" 취소 (").Append(e.Reason).Append(')');
                    if (e.DamageDealt > 0)
                    {
                        sb.Append(" — 취소 전 피해 ").Append(e.DamageDealt);
                    }

                    sb.AppendLine();
                    foreach (var step in e.DamageSteps)
                    {
                        sb.Append("      ").Append(step.HolderId).Append('의')
                          .Append(StatusName(catalog, step.StatusId))
                          .Append(": ").Append(step.Before).Append(" → ").AppendLine(step.After.ToString());
                    }

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

        private static string HpSourceName(KoreanDescriptionCatalog catalog, HpChanged e)
            => e.Source == HpChangeSource.StatusTick ? StatusName(catalog, e.SourceId) : e.SourceId;

        /// <summary>버프 이름: 상태 키면 설명 레지스트리, 아니면 카드 버프 상수의 고정 문구.</summary>
        private static string BuffName(KoreanDescriptionCatalog catalog, string buffId)
            => buffId == CardBuffIds.DamageBonus ? "피해 보너스" : StatusName(catalog, buffId);
    }
}
