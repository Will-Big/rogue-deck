using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;

namespace FateWeaver.Core.Events
{
    public enum Outcome { Ongoing, Win, Lose }

    /// <summary>The sole output of resolution. UI replays it; tests assert on it.</summary>
    public abstract record ResolutionEvent;

    public sealed record TurnStarted(int TurnIndex) : ResolutionEvent;

    public sealed record CardResolved(
        int InstanceId,
        string OwnerId,
        string CardId,
        Side Side,
        int DamageDealt,
        string TargetId,
        ConditionTier ConditionTier = ConditionTier.Basic) : ResolutionEvent
    {
        /// <summary>Compat constructor for pre-Task-3 callers that don't track card identity. Real
        /// resolution (TurnResolver) always uses the primary constructor with the card's actual
        /// InstanceId/OwnerId; this exists only so older unit tests keep compiling unchanged.</summary>
        public CardResolved(
            string cardId,
            Side side,
            int damageDealt,
            string targetId,
            ConditionTier conditionTier = ConditionTier.Basic)
            : this(-1, null, cardId, side, damageDealt, targetId, conditionTier)
        {
        }
    }

    /// <summary>A placed execution card that did not complete. Effects applied before cancellation
    /// persist, and their independent state-change events may follow this single cancellation event.
    /// Reason distinguishes why (see CardCancellationReason).</summary>
    public sealed record CardCancelled(
        int InstanceId,
        string CardId,
        string OwnerId,
        CardCancellationReason Reason) : ResolutionEvent;

    /// <summary>A party member's HP reached zero or below and they had no SurviveCharges left to
    /// absorb the hit.</summary>
    public sealed record PartyMemberDied(string MemberId) : ResolutionEvent;

    /// <summary>A party member spent one SurviveCharges charge to steady at 1 HP instead of dying.</summary>
    public sealed record DeathsDoorSurvived(string MemberId) : ResolutionEvent;

    /// <summary>An enemy's HP reached zero or below (from card effects or a status tick).</summary>
    public sealed record EnemyDied(string EnemyId) : ResolutionEvent;

    /// <summary>상태 행동의 턴 종료 틱이 보유자에게 발동했다 (예: 독 피해). Damage는 이번 틱이 준
    /// 피해, Magnitude는 틱 이후의 상태 수치다.</summary>
    public sealed record StatusTicked(
        string HolderId, string StatusId, int Damage, int Magnitude) : ResolutionEvent;

    /// <summary>사망한 보유자의 상태가 다른 보유자에게 이전되었다 (예: 사후 전염의 독 이전).</summary>
    public sealed record StatusTransferred(
        string FromHolderId, string ToHolderId, string StatusId, int Magnitude) : ResolutionEvent;

    public sealed record TurnEnded(int TurnIndex, Outcome Outcome) : ResolutionEvent;
}
