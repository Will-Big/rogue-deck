namespace FateWeaver.Core.Effects
{
    /// <summary>Per-effect-kind parameter block carried by EffectData. Each effect key that needs
    /// parameters beyond the shared scalar declares its own payload record; the common model never
    /// grows per-effect fields (AGENTS.md rule 9).</summary>
    public interface IEffectPayload
    {
    }
}
