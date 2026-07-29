namespace FateWeaver.Core.Status
{
    /// <summary>독 안정 (안정 배양): 이번 턴 종료 독 피해는 그대로, 성장만 금지한다. ThisTurn 수명.</summary>
    public sealed class PoisonStasisBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.PoisonStasis;
        public override StatusScope Scope => StatusScope.Entity;
    }
}
