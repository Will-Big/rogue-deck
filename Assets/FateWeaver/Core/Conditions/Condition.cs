using System.Collections.Generic;
using FateWeaver.Core.Cards;

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

    public sealed record BeforeNextEnemyAttack : Condition;

    public sealed record AdjacentCardIs(
        AdjacentDirection Direction,
        Side Side,
        CardType? Type = null) : Condition; // Type null = any card type (e.g. "any player execution card")

    public sealed record SameTarget : Condition;

    /// <summary>Success when no card of the given side resolves before this one (e.g. an enemy card that
    /// strikes before any player card acts). Mirror of BeforeNextEnemyAttack for an arbitrary side.</summary>
    public sealed record NoPrecedingCardOfSide(Side Side) : Condition;

    /// <summary>Success when no card of the given side resolves after this one.</summary>
    public sealed record NoFollowingCardOfSide(Side Side) : Condition;

    public sealed record AllOf(IReadOnlyList<Condition> Conditions) : Condition;
}
