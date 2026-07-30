namespace FateWeaver.Core.Status
{
    /// <summary>방어: absorbs incoming damage using its Magnitude (block points), spending block as it
    /// absorbs. Entity-scoped; typically applied as ThisTurn so block resets each turn.
    /// 흡수 층이므로 배율 상태(취약 등)가 모두 접힌 뒤 마지막에 적용된다 — 추가 체력처럼 동작하며
    /// 방어와 취약이 걸린 순서가 결과를 바꾸지 않는다.</summary>
    public sealed class BlockBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Block;
        public override StatusScope Scope => StatusScope.Entity;
        public override bool StacksMagnitude => true;
        public override StatusDamageLayer DamageLayer => StatusDamageLayer.Absorb;

        public override int ModifyIncomingDamage(int damage, StatusContext ctx)
        {
            var absorbed = ctx.Instance.Magnitude < damage ? ctx.Instance.Magnitude : damage;
            ctx.Instance.Magnitude -= absorbed;
            return damage - absorbed;
        }
    }
}
