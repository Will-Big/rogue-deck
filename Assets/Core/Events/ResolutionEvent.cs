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
        /// <summary>이 카드가 준 피해가 상태로 어떻게 바뀌었는지의 단계별 내역. 상태가 관여하지
        /// 않았으면 빈 목록이다. 이벤트를 새로 끼워 넣지 않으려고 페이로드로 싣는다.</summary>
        public System.Collections.Generic.IReadOnlyList<DamageStep> DamageSteps { get; init; }
            = System.Array.Empty<DamageStep>();

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
        CardCancellationReason Reason) : ResolutionEvent
    {
        /// <summary>취소 전에 이미 적용된 효과가 준 실제 피해와 그 단계 내역. 취소가 피해를
        /// 되돌리지 않으므로 로그에서도 사라지면 안 된다. 효과 실행 전에 취소된 카드는 기본값
        /// (0, 빈 목록)이다.</summary>
        public int DamageDealt { get; init; }
        public System.Collections.Generic.IReadOnlyList<DamageStep> DamageSteps { get; init; }
            = System.Array.Empty<DamageStep>();
    }

    /// <summary>A party member's HP reached zero or below and they had no SurviveCharges left to
    /// absorb the hit.</summary>
    public sealed record PartyMemberDied(string MemberId) : ResolutionEvent;

    /// <summary>A party member spent one SurviveCharges charge to steady at 1 HP instead of dying.</summary>
    public sealed record DeathsDoorSurvived(string MemberId) : ResolutionEvent;

    /// <summary>An enemy's HP reached zero or below (from card effects or a status tick).</summary>
    public sealed record EnemyDied(string EnemyId) : ResolutionEvent;

    /// <summary>상태가 보유자에게 부여되었다. Magnitude는 획득 훅(손상 등)을 거친 최종 값이고,
    /// Stacked는 기존 인스턴스에 합산되었는지다(방어·독).</summary>
    public sealed record StatusApplied(
        string HolderId, string StatusId, int Count, int Magnitude, bool Stacked) : ResolutionEvent;

    /// <summary>상태의 수명이 다해 보유자에게서 사라졌다 (ThisTurn 소멸 또는 Turns 소진).</summary>
    public sealed record StatusExpired(string HolderId, string StatusId) : ResolutionEvent;

    /// <summary>상태 행동의 턴 종료 틱이 보유자에게 발동했다 (예: 독 피해). Damage는 이번 틱이 준
    /// 피해, Magnitude는 틱 이후의 상태 수치다.</summary>
    public sealed record StatusTicked(
        string HolderId, string StatusId, int Damage, int Magnitude) : ResolutionEvent;

    /// <summary>사망한 보유자의 상태가 다른 보유자에게 이전되었다 (예: 사후 전염의 독 이전).</summary>
    public sealed record StatusTransferred(
        string FromHolderId, string ToHolderId, string StatusId, int Magnitude) : ResolutionEvent;

    /// <summary>HP 변화의 원인 종류. 새 원인(회복 등)이 생기면 멤버를 추가한다.</summary>
    public enum HpChangeSource { CardDamage, StatusTick }

    /// <summary>보유자의 HP가 실제로 바뀌었다. Before/After는 치명 버팀 클램프 이후의 실측값이고,
    /// SourceId는 원인 카드 id(CardDamage) 또는 상태 키(StatusTick)다. HP가 안 바뀐 명중은
    /// 남기지 않는다.</summary>
    public sealed record HpChanged(
        string HolderId, int Before, int After, HpChangeSource Source, string SourceId) : ResolutionEvent;

    /// <summary>다음 플레이어 턴에 지급될 운명력이 적립되었다 (증류). 실제 지급(턴 시작 리필)과
    /// 지출은 세션 영역이라 이 타임라인에 없다 — 개입 로그 확장에서 다룬다.</summary>
    public sealed record FateEnergyGained(string SourceCardId, int Amount) : ResolutionEvent;

    /// <summary>효과가 보유자의 상태 수치를 능동 소비했다 (수명 만료·자동 소진과 구분).</summary>
    public sealed record StatusConsumed(string HolderId, string StatusId, int Amount) : ResolutionEvent;

    public sealed record TurnEnded(int TurnIndex, Outcome Outcome) : ResolutionEvent;
}
