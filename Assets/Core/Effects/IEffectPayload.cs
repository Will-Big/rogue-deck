namespace FateWeaver.Core.Effects
{
    /// <summary>EffectData가 싣고 다니는 효과 종류별 매개변수 묶음. 공통 스칼라 수치 외에 매개변수가
    /// 필요한 효과 키는 저마다 자기 페이로드 레코드를 선언한다. 공통 모델에는 효과별 필드를
    /// 늘리지 않는다(AGENTS.md 규칙 9).</summary>
    public interface IEffectPayload
    {
    }
}
