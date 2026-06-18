using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;

namespace FateWeaver.Core.Events
{
    public enum Outcome { Ongoing, Win, Lose }

    /// <summary>The sole output of resolution. UI replays it; tests assert on it.</summary>
    public abstract record ResolutionEvent;

    public sealed record TurnStarted(int TurnIndex) : ResolutionEvent;

    public sealed record CardResolved(
        string CardId,
        Side Side,
        int DamageDealt,
        string TargetId,
        ConditionTier ConditionTier = ConditionTier.Basic) : ResolutionEvent;

    public sealed record TurnEnded(int TurnIndex, Outcome Outcome) : ResolutionEvent;
}
