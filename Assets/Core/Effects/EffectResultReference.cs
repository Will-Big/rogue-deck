namespace FateWeaver.Core.Effects
{
    /// <summary>앞 효과의 실제 소비량이 MinimumConsumed 이상일 때만 이 효과를 수행한다 — "소비했다면 X".
    /// 카드 시작 조건과 달리 카드 도중에 결정된다(전투 실행 계약 스펙 §4·§5).</summary>
    public sealed record EffectResultRequirement(string SourceEffectId, int MinimumConsumed);

    /// <summary>이 효과의 수치에 앞 효과의 실제 소비량 × PerConsumed를 더한다 — "소비 1당 +k".</summary>
    public sealed record EffectResultScaling(string SourceEffectId, int PerConsumed);
}
