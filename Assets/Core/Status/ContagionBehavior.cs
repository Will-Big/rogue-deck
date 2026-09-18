namespace FateWeaver.Core.Status
{
    /// <summary>전염 (사후 전염): 보유자가 독 상태로 사망하면 남은 독 전량을 현재 적군 앞 하나(생존)에게
    /// 이전한다. 유효 기간은 부여 수명(Turns)으로 표현한다. 이 행동은 상태 등록(수명·저작)만 맡고, 사망 때의
    /// 이전은 사망 사건에 반응하는 능력(ContagionReaction)이 한다.</summary>
    public sealed class ContagionBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Contagion;
        public override StatusScope Scope => StatusScope.Entity;
    }
}
