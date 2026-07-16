using System;
using System.Collections.Generic;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class InterventionDescriptionRegistry
    {
        private readonly Dictionary<InterventionActionKey, IInterventionDescriptionHandler> _handlers =
            new Dictionary<InterventionActionKey, IInterventionDescriptionHandler>();

        public void Register(IInterventionDescriptionHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (string.IsNullOrWhiteSpace(handler.Key.Id))
                throw new ArgumentException("Intervention description key is required.", nameof(handler));
            if (_handlers.ContainsKey(handler.Key))
                throw new ArgumentException(
                    "Duplicate intervention description key '" + handler.Key + "'.", nameof(handler));
            _handlers.Add(handler.Key, handler);
        }

        public bool Contains(InterventionActionKey key) => _handlers.ContainsKey(key);

        public IInterventionDescriptionHandler Resolve(InterventionActionKey key)
            => _handlers.TryGetValue(key, out var handler)
                ? handler
                : throw new KeyNotFoundException(
                    "No intervention description handler registered for '" + key + "'.");
    }
}
