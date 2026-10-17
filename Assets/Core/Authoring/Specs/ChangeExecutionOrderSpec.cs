using System.Collections.Generic;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 하나의 실행 순서를 Payload.Delta만큼 옮긴다. Payload.TargetSide로 진영을 제한한다
    /// (재촉=Player, 유예=Enemy, Any=제한 없음). 조작 봉투에는 공통 숫자 칸이 없으므로 Delta는
    /// 페이로드에 둔다(효과 JSON 설계 P1 경계 사례).</summary>
    public sealed class ChangeExecutionOrderSpec : InterventionSpec
    {
        public ChangeExecutionOrderPayloadSpec Payload;

        public override InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new ChangeExecutionOrderPayload(Payload.Delta, ToTargetSide(Payload.TargetSide));

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "change_execution_order requires a payload.";
            }
        }
    }

    /// <summary>ChangeExecutionOrderPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class ChangeExecutionOrderPayloadSpec
    {
        public int Delta;
        public InterventionTargetSideRef TargetSide;
    }
}
