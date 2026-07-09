using System.Collections.Generic;

namespace FateWeaver.Simulation
{
    /// <summary>One turn of a multi-turn scenario: the zone drawn that turn, the fate energy
    /// available, and the scripted fate plays for that turn.</summary>
    public sealed class TurnScript
    {
        public int FateEnergy { get; }
        public IReadOnlyList<ZoneCardSpec> ZoneCards { get; }
        public IReadOnlyList<InterventionPlaySpec> InterventionPlays { get; }

        public TurnScript(
            int fateEnergy,
            IReadOnlyList<ZoneCardSpec> zoneCards,
            IReadOnlyList<InterventionPlaySpec> interventionPlays)
        {
            FateEnergy = fateEnergy;
            ZoneCards = zoneCards;
            InterventionPlays = interventionPlays;
        }
    }
}
