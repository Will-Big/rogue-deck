namespace FateWeaver.Core.Intervention
{
    /// <summary>액션 종류별 파라미터 블록. 이것이 있어서 InterventionActionData는 액션이 늘어도
    /// 필드가 자라지 않는다(AGENTS.md 규칙 9). 효과 쪽 IEffectPayload와 같은 형태이며, 같은
    /// 이유로 비어 있다 — 공통으로 꺼낼 것이 없다.</summary>
    public interface IInterventionPayload
    {
    }
}
