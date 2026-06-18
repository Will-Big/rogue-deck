using System.Collections.Generic;

namespace FateWeaver.Core.Fate
{
    public sealed class FateActionRegistry
    {
        private readonly Dictionary<FateActionKey, IFateActionHandler> _handlers = new();

        public void Register(IFateActionHandler handler) => _handlers[handler.Key] = handler;

        public IFateActionHandler Resolve(FateActionKey key)
            => _handlers.TryGetValue(key, out var h)
                ? h
                : throw new KeyNotFoundException($"No fate action handler registered for '{key}'");
    }
}
