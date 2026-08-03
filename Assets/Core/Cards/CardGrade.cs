namespace FateWeaver.Core.Cards
{
    /// <summary>카드 등급. 카드 풀의 후보 구성에만 쓰이며 전투 규칙에는 관여하지 않는다.
    /// None은 등급 개념이 없는 카드(검증용 fixture 등)의 정상 상태다.</summary>
    public enum CardGrade
    {
        None,
        Common,
        Advanced,
        Rare,
        Other
    }
}
