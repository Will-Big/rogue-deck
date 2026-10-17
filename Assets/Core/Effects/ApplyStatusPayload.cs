using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>apply_status 효과의 매개변수. 카드가 내는 숫자 하나는 EffectData.EffectValue에
    /// 싣는다(그래야 조건부 SuccessEffectValue 교체가 적용된다) — 그 값과 어긋날 수 있는 두 번째
    /// 필드로 여기에 중복해 두지 않는다. 그 숫자의 뜻(세기인가 지속인가)과 결과 StatusLifetime의
    /// 종류는 부여 시점에 전투의 StatusContentCatalog에서 Key로 찾는다. 카드는 수명을 고르지 않는다.</summary>
    public sealed record ApplyStatusPayload(StatusKey Key) : IEffectPayload;
}
