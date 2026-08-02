using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>Parameters for the apply_status effect. The one number a card contributes rides on
    /// EffectData.EffectValue (so conditional SuccessEffectValue overrides apply) — never duplicated
    /// here as a second field that could disagree with it. Its meaning (magnitude vs. duration) and the
    /// resulting StatusLifetime's Kind are looked up from the combat's StatusContentCatalog by Key at
    /// apply time; the card never chooses a lifetime.</summary>
    public sealed record ApplyStatusPayload(
        StatusKey Key,
        StatusApplyTarget Target) : IEffectPayload;
}
