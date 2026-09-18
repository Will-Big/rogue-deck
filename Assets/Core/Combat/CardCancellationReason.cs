namespace FateWeaver.Core.Combat
{
    /// <summary>Why an execution card's effects were cancelled instead of resolving normally.
    /// Recorded on ExecutionCardInstance.CancellationReason before the card's turn (first reason wins);
    /// CardExecutor then emits a single Events.CardCancelled instead of Events.CardResolved.
    /// 효과가 대상을 찾지 못하는 것은 취소가 아니다(그 효과만 미적용, 스펙 §2). 주인이 죽은 카드는 차례가 오기 전에
    /// 실행선에서 빠지므로 취소 사유가 아니다(Events.CardRemoved).</summary>
    public enum CardCancellationReason
    {
        StatusIntercepted
    }
}
