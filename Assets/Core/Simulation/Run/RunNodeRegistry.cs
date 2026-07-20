using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    public sealed class RunNodeRegistry
    {
        private readonly Dictionary<RunNodeKey, IRunNodeHandler> _handlers = new();

        public void Register(IRunNodeHandler handler) => _handlers[handler.Key] = handler;

        public bool Contains(RunNodeKey key) => _handlers.ContainsKey(key);

        public IRunNodeHandler Resolve(RunNodeKey key)
            => _handlers.TryGetValue(key, out var h)
                ? h
                : throw new KeyNotFoundException($"No run node handler registered for '{key}'");
    }
}
