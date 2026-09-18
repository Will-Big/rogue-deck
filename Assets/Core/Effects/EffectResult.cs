namespace FateWeaver.Core.Effects
{
    /// <summary>효과 하나를 수행한 결과. 뒤 효과의 결과 참조(requires·scaleBy)가 효과 ID로 읽는다
    /// (전투 실행 계약 스펙 §5). 수행하지 않은 효과도 Applied=false로 기록된다.</summary>
    public sealed record EffectResult(bool Applied, int ConsumedAmount, int DamageDealt)
    {
        public static readonly EffectResult Skipped = new EffectResult(false, 0, 0);
    }
}
