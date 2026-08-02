using System;

namespace FateWeaver.Core.Authoring.Statuses
{
    /// <summary>보유자의 카드 실행 순서를 옮기는 상태 (둔화 +, 가속 −).
    /// 세기는 상태가 소유하고 카드는 지속 턴만 준다.</summary>
    [Serializable]
    public sealed class ExecutionOrderStatusSpec : StatusSpec
    {
        public int ExecutionOrderDelta;
    }
}
