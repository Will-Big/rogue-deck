namespace FateWeaver.Core.Effects
{
    /// <summary>피해 한 번의 공통 속성(전투 실행 계약 스펙 §7). 어느 원인(카드·상태·반응)의 피해든 가질 수 있다 —
    /// 특정 상태 전용 경로로 표현하지 않는다. 값은 데이터가 정한다(상태는 Content/Statuses/*.json의 damage).
    /// Piercing: 흡수 층(방어)을 건너뛴다. IgnoresMultipliers: 받는 쪽 배율 층(취약 등)을 건너뛴다.</summary>
    public sealed record DamageTraits(bool Piercing, bool IgnoresMultipliers)
    {
        /// <summary>속성 없는 보통 피해: 배율과 흡수를 모두 거친다.</summary>
        public static readonly DamageTraits Normal = new DamageTraits(false, false);
    }
}
