using FateWeaver.Core.Authoring.Statuses;

namespace FateWeaver.Core.Status
{
    /// <summary>손상: the holder gains less block, by the multiplier in this combat's StatusRules
    /// (default 75%). Folded where the block is gained, on the holder RECEIVING it — not on the acting
    /// card's owner. count is remaining turns, not intensity.</summary>
    public sealed class DamagedBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Damaged;
        public override StatusScope Scope => StatusScope.Entity;

        public override StatusSpec NewSpec() => new MultiplierStatusSpec();

        public override int ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx)
            => gained == StatusKeys.Block ? ctx.Rules.For(Key).Apply(magnitude) : magnitude;
    }
}
