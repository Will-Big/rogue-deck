using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Effects
{
    /// <summary>Optional handler capability: validates its own EffectData (payload type, required
    /// values) during content validation. Content walks resolve the handler and delegate, so a new
    /// effect's validation lives in its handler class — no central switch.</summary>
    public interface IEffectDataValidator
    {
        IEnumerable<string> ValidateData(EffectData effect);
    }
}
