namespace FateWeaver.Core.Combat
{
    /// <summary>Why an execution card's effects were cancelled instead of resolving normally.
    /// Recorded on ExecutionCardInstance.CancellationReason (first reason wins). TurnResolver reads
    /// this to emit a single Events.CardCancelled instead of Events.CardResolved for the card.
    /// 주인이 죽은 카드는 차례가 오기 전에 실행선에서 빠지므로 취소 사유가 아니다(Events.CardRemoved).</summary>
    public enum CardCancellationReason
    {
        NoValidTarget,
        StatusIntercepted
    }
}
