namespace FateWeaver.Core.Status
{
    /// <summary>둔화: the holder's cards resolve later (executionOrder += the status's own delta, from
    /// the combat's StatusContentCatalog — cards only give this status a duration). Entity-scoped.</summary>
    public sealed class SlowBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Slow;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyExecutionOrder(int executionOrder, StatusContext ctx)
            => executionOrder + ctx.Content.ExecutionOrderDeltaOf(Key);
    }
}
