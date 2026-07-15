namespace FateWeaver.Core.Status
{
    /// <summary>Card-scoped marker queried by the TurnResolver: when present on a player card,
    /// its condition-success reward is forced down to the basic tier. No active hook — the
    /// resolver inspects this status directly (see TurnResolver.ResolveTier).</summary>
    public sealed class RewardSuppressionBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.RewardNullified;
        public override StatusScope Scope => StatusScope.CardInstance;
    }
}
