using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Fixed linear node sequence for one run. Authored in the Unity layer (SO) and
    /// converted to this pure data on load, like the card SO pipeline.</summary>
    public sealed class RunDefinition
    {
        public IReadOnlyList<RunNodeData> Nodes { get; }

        public RunDefinition(IReadOnlyList<RunNodeData> nodes) => Nodes = nodes;
    }
}
