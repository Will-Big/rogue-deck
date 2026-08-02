using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Authoring
{
    /// <summary>The selected 10-card starter deck expressed as flat CardSpecs.
    /// The SO/codegen path produces specs of this shape.</summary>
    public static class StarterDeckSpecs
    {
        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            StarterPoolSpecs.ProbingStrike(),
            StarterPoolSpecs.DelayedStrike(),
            StarterPoolSpecs.QuickCover(),
            StarterPoolSpecs.EarlyGuard(),
            StarterPoolSpecs.Breather(),
            StarterPoolSpecs.Hasten(),
            StarterPoolSpecs.ToxicReclaim(),
            StarterPoolSpecs.EarlyOnset(),
            StarterPoolSpecs.SporeVeil(),
            StarterPoolSpecs.LastDrop()
        };
    }
}
