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
        CardType? Type = null) : Condition; // Type null = any card type (e.g. "any player action card")

    public sealed record SameTarget : Condition;

    public sealed record AllOf(IReadOnlyList<Condition> Conditions) : Condition;
}
