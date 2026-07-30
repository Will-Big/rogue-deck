namespace FateWeaver.Core.Status
{
    /// <summary>피해 계산에서 상태가 접히는 단계. 배율 층이 모두 접히고 버림된 뒤 흡수 층이
    /// 적용된다 (취약을 먼저 곱하고 방어가 추가 체력처럼 마지막에 흡수한다).
    /// 뺄셈이거나 자기 수치를 소모하는 상태는 배율 층에 넣지 않는다.</summary>
    public enum StatusDamageLayer
    {
        Multiplier,
        Absorb
    }
}
