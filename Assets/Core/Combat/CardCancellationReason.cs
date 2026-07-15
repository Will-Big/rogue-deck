namespace FateWeaver.Core.Combat
{
    /// <summary>Why an execution card's effects were cancelled instead of resolving normally.
    /// Recorded on ExecutionCardInstance.CancellationReason (first reason wins); the resulting
    /// event flow (e.g. CardCancelled) is built on top of this in a later task.</summary>
    public enum CardCancellationReason
    {
        NoValidTarget,
        OwnerDied,
        StatusIntercepted
    }
}
