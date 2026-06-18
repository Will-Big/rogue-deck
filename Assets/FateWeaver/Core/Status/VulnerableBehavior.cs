namespace FateWeaver.Core.Status
{
    /// <summary>취약: the holder takes 50% more damage. Applies regardless of damage source
    /// (entity incoming hook) — more robust than a per-card "+50%". Stack scaling deferred.</summary>
    public sealed class VulnerableBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Vulnerable;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyIncomingDamage(int damage, StatusContext ctx)
            => (damage * 3) / 2;
    }
}
