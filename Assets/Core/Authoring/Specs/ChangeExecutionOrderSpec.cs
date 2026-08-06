using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 하나의 실행 순서를 Delta만큼 옮긴다. TargetSide로 진영을 제한한다
    /// (재촉=Player, 유예=Enemy, Any=제한 없음).</summary>
    [Serializable]
    public sealed class ChangeExecutionOrderSpec : InterventionSpec
    {
        public int Delta;
        public InterventionTargetSideRef TargetSide;

        public override InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new ChangeExecutionOrderPayload(Delta, ToTargetSide(TargetSide));
    }
}
