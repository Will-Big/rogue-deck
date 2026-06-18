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
        CardType Type) : Condition;

    public sealed record SameTarget : Condition;
}
