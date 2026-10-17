namespace FateWeaver.Core.Effects
{
    /// <summary>EffectData가 싣고 다니는 효과 종류별 매개변수 묶음. 공통 스칼라 수치 외에 매개변수가
    /// 필요한 효과 키는 저마다 자기 페이로드 레코드를 선언한다. 공통 모델에는 효과별 필드를
    /// 늘리지 않는다(AGENTS.md 규칙 9). 효과의 세기 숫자("얼마나")는 페이로드가 아니라
    /// EffectData.EffectValue에 둔다 — 조건 성공 수치 교체와 scaling 가산이 그 칸에만 적용된다.
    /// 페이로드에는 그 종류만 쓰는 선택값("무엇을, 어떤 식으로")만 둔다.</summary>
    public interface IEffectPayload
    {
    }
}
