using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Conditions
{
    public enum AdjacentDirection
    {
        Previous,
        Next
    }

    public abstract record Condition;

    public sealed record FirstToTrigger : Condition;

    public sealed record WithinNth(int N) : Condition;

    public sealed record BeforeNextEnemyDamageCard : Condition;

    public sealed record AdjacentCardIs(
        AdjacentDirection Direction,
        Side Side) : Condition;

    public sealed record AdjacentCardHasEffect(
        AdjacentDirection Direction,
        Side Side,
        EffectKey EffectKey) : Condition;

    /// <summary>직전에 실행된 카드(ResolutionContext.LastExecutedCard)가 주어진 진영이면 Success.
    /// 실행 이력을 보므로, 차례가 왔지만 효과가 없었던 카드는 직전 카드로 세고, 주인이 죽어 실행선에서
    /// 빠진 카드는 건너뛴다(전투 실행 계약 스펙 §6). 배치(인접 칸)를 보는 AdjacentCardIs와 다르다.</summary>
    public sealed record PreviousExecutedCardIs(
        Side Side) : Condition;

    public sealed record PreviousExecutedCardHasEffect(
        Side Side,
        EffectKey EffectKey) : Condition;

    /// <summary>Success when no card of the given side resolves before this one (e.g. an enemy card that
    /// strikes before any player card acts). Mirror of BeforeNextEnemyDamageCard for an arbitrary side.</summary>
    public sealed record NoPrecedingCardOfSide(Side Side) : Condition;

    /// <summary>Success when no card of the given side resolves after this one.</summary>
    public sealed record NoFollowingCardOfSide(Side Side) : Condition;

    public sealed record AllOf(IReadOnlyList<Condition> Conditions) : Condition;
}
