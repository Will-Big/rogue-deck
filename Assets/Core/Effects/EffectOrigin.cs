namespace FateWeaver.Core.Effects
{
    /// <summary>효과가 어디서 왔는가(전투 실행 계약 스펙 §7). 카드 효과와 턴 시점 상태 처리는 Primary, 반응
    /// 능력이 낸 효과는 Reaction이다. Reaction 효과가 만든 사건은 기록·사망 정리는 하지만 다른 반응을 부르지 않는다.</summary>
    public enum EffectOrigin
    {
        Primary,
        Reaction
    }
}
