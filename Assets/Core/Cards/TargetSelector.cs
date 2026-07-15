namespace FateWeaver.Core.Cards
{
    /// <summary>Position-based target selector evaluated against the living members of a formation
    /// at execution time (e.g. an enemy attack picking a party member). Dead members are skipped
    /// without shifting the formation's underlying indices.</summary>
    public enum TargetSelector
    {
        FrontMost,
        SecondFromFront,
        BackMost,
        Random
    }
}
