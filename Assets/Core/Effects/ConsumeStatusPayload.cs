using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>consume_status 파라미터: 대상 적의 상태 수치를 Amount만큼 Mode 방식으로 제거한다.
    /// 실제 소비량은 이 효과의 결과(EffectResult.ConsumedAmount)가 되며, 뒤 효과가 효과 ID로 읽는다
    /// (전투 실행 계약 스펙 §5).</summary>
    public sealed record ConsumeStatusPayload(
        StatusKey Key, int Amount, ConsumptionMode Mode) : IEffectPayload;
}
