namespace FateWeaver.Simulation.Run
{
    /// <summary>One authored node on the linear run map: a node type key plus that type's payload.</summary>
    public sealed class RunNodeData
    {
        public RunNodeKey Key { get; }
        public IRunNodePayload Payload { get; }

        public RunNodeData(RunNodeKey key, IRunNodePayload payload)
        {
            Key = key;
            Payload = payload;
        }
    }
}
