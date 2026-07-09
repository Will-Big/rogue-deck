namespace FateWeaver.Core.Status
{
    /// <summary>방어: absorbs incoming damage using its Magnitude (block points), spending block as it
    /// absorbs. Entity-scoped; typically applied as ThisTurn so block resets each turn.</summary>
    public sealed class BlockBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Block;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyIncomingDamage(int damage, StatusContext ctx)
        {
            var absorbed = ctx.Instance.Magnitude < damage ? ctx.Instance.Magnitude : damage;
            ctx.Instance.Magnitude -= absorbed;
            return damage - absorbed;
        }
    }
}
