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

    /// <summary>Success when the immediately-previous card to actually finish resolution (i.e. the
    /// current ResolutionContext.LastExecutedCard, which skips cancelled cards) matches the given
    /// side. Replaces AdjacentCardIs(Previous, ...) for authored content: it looks at the
    /// nearest EXECUTED card rather than the raw adjacent zone slot, so a card cancelled between two
    /// others (OwnerDied / NoValidTarget / StatusIntercepted) is skipped over.</summary>
    public sealed record PreviousExecutedCardIs(
        Side Side) : Condition;

    public sealed record PreviousExecutedCardHasEffect(
        Side Side,
        EffectKey EffectKey) : Condition;

    public sealed record SameTarget : Condition;

    /// <summary>Success when no card of the given side resolves before this one (e.g. an enemy card that
    /// strikes before any player card acts). Mirror of BeforeNextEnemyDamageCard for an arbitrary side.</summary>
    public sealed record NoPrecedingCardOfSide(Side Side) : Condition;

    /// <summary>Success when no card of the given side resolves after this one.</summary>
    public sealed record NoFollowingCardOfSide(Side Side) : Condition;

    public sealed record AllOf(IReadOnlyList<Condition> Conditions) : Condition;
}
