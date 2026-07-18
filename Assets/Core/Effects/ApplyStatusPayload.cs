using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Parameters for the apply_status effect. Magnitude rides on EffectData.EffectValue.</summary>
    public sealed record ApplyStatusPayload(
        StatusKey Key,
        StatusLifetime Lifetime,
        StatusApplyTarget Target) : IEffectPayload;
}
