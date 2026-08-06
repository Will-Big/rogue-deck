using System;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 두 장의 실행 순서를 맞바꾼다. RequireAdjacent가 true면 둘이 해결
    /// 순서상 인접해야 한다(엇갈림).</summary>
    [Serializable]
    public sealed class SwapExecutionOrderSpec : InterventionSpec
    {
        public InterventionTargetSideRef TargetSide;
        public bool RequireAdjacent;

        public override InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new SwapExecutionOrderPayload(ToTargetSide(TargetSide), RequireAdjacent);
    }
}
