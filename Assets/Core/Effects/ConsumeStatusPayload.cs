using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>consume_status 파라미터: 대상 적의 상태 수치를 최대 MaxAmount만큼 제거한다.
    /// 소비량은 카드 인스턴스에 기록되고(ConsumedStatusAtLeast 조건이 읽음),
    /// 소비량 × DamageBonusPerConsumed가 이 카드의 뒤 피해 효과에 보너스로 적립된다.</summary>
    public sealed record ConsumeStatusPayload(
        StatusKey Key, int MaxAmount, int DamageBonusPerConsumed) : IEffectPayload;
}
