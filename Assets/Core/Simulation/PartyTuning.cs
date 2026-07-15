using System;
using System.Collections.Generic;

namespace FateWeaver.Simulation
{
    /// <summary>Party-size limits and per-living-member combat economy values.</summary>
    public sealed class PartyTuning
    {
        public int MinPartySize { get; init; } = 1;
        public int MaxPartySize { get; init; } = 3;
        public int DefaultMemberMaxHp { get; init; }
        public int SurviveChargesPerCombat { get; init; }
        public IReadOnlyDictionary<int, int> DrawByLivingCount { get; init; }

        public int DrawFor(int livingCount)
        {
            if (DrawByLivingCount == null
                || !DrawByLivingCount.TryGetValue(livingCount, out var drawCount)
                || drawCount <= 0)
            {
                throw new ArgumentException("Draw tuning must contain a positive value for the living party count.");
            }

            return drawCount;
        }

        public static PartyTuning Prototype => new PartyTuning
        {
            DefaultMemberMaxHp = 25,
            SurviveChargesPerCombat = 1,
            DrawByLivingCount = new Dictionary<int, int>
            {
                { 1, 3 },
                { 2, 4 },
                { 3, 5 }
            }
        };
    }
}
