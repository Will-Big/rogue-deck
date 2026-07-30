namespace FateWeaver.Core.Status
{
    /// <summary>약화: the holder deals less damage, by the multiplier in this combat's StatusRules
    /// (default 75%). Folded on the acting side before the target's incoming statuses, so a weak
    /// attacker hitting a vulnerable target floors twice. count is remaining turns, not intensity.
    /// 피해 최소 1을 보장하지 않는다 — floor(1 x 0.75) = 0은 의도된 결과다.</summary>
    public sealed class WeakBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Weak;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyOutgoingDamage(int damage, StatusContext ctx)
            => ctx.Rules.For(Key).Apply(damage);
    }
}
