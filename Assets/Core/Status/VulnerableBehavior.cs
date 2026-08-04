using FateWeaver.Core.Authoring.Statuses;

namespace FateWeaver.Core.Status
{
    /// <summary>취약: the holder takes more damage, by the multiplier in this combat's StatusRules
    /// (default 150%). Applies regardless of damage source (entity incoming hook) — more robust than a
    /// per-card "+50%". count is remaining turns, not intensity: stacking extends duration.</summary>
    public sealed class VulnerableBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Vulnerable;
        public override StatusScope Scope => StatusScope.Entity;

        public override StatusSpec NewSpec() => new MultiplierStatusSpec();

        public override int ModifyIncomingDamage(int damage, StatusContext ctx)
            => ctx.Rules.For(Key).Apply(damage);
    }
}
