namespace FateWeaver.Core.Status
{
    /// <summary>둔화: the holder's cards resolve later (initiative += Magnitude). Entity-scoped.</summary>
    public sealed class SlowBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Slow;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyInitiative(int initiative, StatusContext ctx)
            => initiative + ctx.Instance.Magnitude;
    }
}
