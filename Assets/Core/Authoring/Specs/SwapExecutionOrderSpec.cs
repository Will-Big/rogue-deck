using System.Collections.Generic;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 두 장의 실행 순서를 맞바꾼다. Payload.RequireAdjacent가 true면 둘이 해결
    /// 순서상 인접해야 한다(엇갈림).</summary>
    public sealed class SwapExecutionOrderSpec : InterventionSpec
    {
        public SwapExecutionOrderPayloadSpec Payload;

        public override InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new SwapExecutionOrderPayload(ToTargetSide(Payload.TargetSide), Payload.RequireAdjacent);

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "swap_execution_order requires a payload.";
            }
        }
    }

    /// <summary>SwapExecutionOrderPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class SwapExecutionOrderPayloadSpec
    {
        public InterventionTargetSideRef TargetSide;
        public bool RequireAdjacent;
    }
}
