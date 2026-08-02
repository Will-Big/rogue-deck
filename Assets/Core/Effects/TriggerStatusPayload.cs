using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>trigger_status 파라미터: 대상 적의 상태 틱(OnTurnEnd)을 지금 발동시키고,
    /// behavior.SuppressThisTurn을 호출해 이번 턴 종료의 같은 발동을 막는다 — 총 발동 횟수는
    /// 유지하고 시점만 앞당긴다 (조기 발병). 어떤 마커를 쓰는지는 상태 자신만 안다.</summary>
    public sealed record TriggerStatusPayload(StatusKey Key) : IEffectPayload;
}
