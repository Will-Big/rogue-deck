using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Intervention
{
    /// <summary>실행 순서 변경의 파라미터. Delta는 대상의 ExecutionOrder에 더할 값이고,
    /// TargetSide가 null이 아니면 그 진영의 레일 카드만 대상이 된다(재촉=Player, 유예=Enemy).</summary>
    public sealed record ChangeExecutionOrderPayload(int Delta, Side? TargetSide) : IInterventionPayload;
}
