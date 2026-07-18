using System.Collections.Generic;

namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionActionRegistry
    {
        private readonly Dictionary<InterventionActionKey, IInterventionActionHandler> _handlers = new();

        public void Register(IInterventionActionHandler handler) => _handlers[handler.Key] = handler;

        public bool Contains(InterventionActionKey key) => _handlers.ContainsKey(key);

        public IInterventionActionHandler Resolve(InterventionActionKey key)
            => _handlers.TryGetValue(key, out var h)
                ? h
                : throw new KeyNotFoundException($"No intervention action handler registered for '{key}'");
    }
}
