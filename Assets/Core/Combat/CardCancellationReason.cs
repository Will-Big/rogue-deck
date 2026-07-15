namespace FateWeaver.Core.Combat
{
    /// <summary>Why an execution card's effects were cancelled instead of resolving normally.
    /// Recorded on ExecutionCardInstance.CancellationReason (first reason wins). TurnResolver reads
    /// this to emit a single Events.CardCancelled instead of Events.CardResolved for the card.</summary>
    public enum CardCancellationReason
    {
        NoValidTarget,
        OwnerDied,
        StatusIntercepted
    }
}
