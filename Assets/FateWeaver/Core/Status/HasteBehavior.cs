namespace FateWeaver.Core.Status
{
    /// <summary>가속: the holder's cards resolve sooner (initiative -= Magnitude). Entity-scoped.</summary>
    public sealed class HasteBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Haste;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyInitiative(int initiative, StatusContext ctx)
            => initiative - ctx.Instance.Magnitude;
    }
}
