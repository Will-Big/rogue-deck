namespace FateWeaver.Core.Status
{
    /// <summary>독 잠복 (조기 발병): 이번 턴 종료에 독이 발동하지 않는다. ThisTurn 수명으로 부여.</summary>
    public sealed class PoisonDormantBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.PoisonDormant;
        public override StatusScope Scope => StatusScope.Entity;
    }
}
