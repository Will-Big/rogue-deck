namespace FateWeaver.Simulation.Run
{
    /// <summary>Marker for per-node authored data (encounter refs, recruit candidates, …).
    /// Concrete payloads live with their node handlers (CombatNodePayload, RecruitHealPayload).</summary>
    public interface IRunNodePayload
    {
    }
}
