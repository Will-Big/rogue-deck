using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Intervention
{
    /// <summary>실행 순서 교환의 파라미터. RequireAdjacent가 true면 두 대상이 해결 순서상 서로
    /// 인접해야 한다(엇갈림).</summary>
    public sealed record SwapExecutionOrderPayload(Side? TargetSide, bool RequireAdjacent) : IInterventionPayload;
}
