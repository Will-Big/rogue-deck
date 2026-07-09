namespace FateWeaver.Core.Status
{
    /// <summary>가속: the holder's cards resolve sooner (executionOrder -= Magnitude). Entity-scoped.</summary>
    public sealed class HasteBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Haste;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyExecutionOrder(int executionOrder, StatusContext ctx)
            => executionOrder - ctx.Instance.Magnitude;
    }
}
