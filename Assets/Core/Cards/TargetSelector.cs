namespace FateWeaver.Core.Cards
{
    /// <summary>Position-based target selector evaluated against the living members of a formation
    /// at execution time (e.g. an enemy attack picking a party member). Dead members are skipped
    /// without shifting the formation's underlying indices.</summary>
    public enum TargetSelector
    {
        FrontOne,
        FrontTwo,
        BackOne,
        BackTwo,

        /// <summary>생존 유닛 전부, 다중 대상 효과 전용 (단일 대상 EnemyTargeting.Select/PartyTargeting.Select는
        /// null을 반환한다 — 호출자가 SelectAll류를 대신 써야 한다).</summary>
        All
    }
}
