using System;
using System.Collections.Generic;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class EffectDescriptionRegistry
    {
        private readonly Dictionary<EffectKey, IEffectDescriptionHandler> _handlers =
            new Dictionary<EffectKey, IEffectDescriptionHandler>();

        public void Register(IEffectDescriptionHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (string.IsNullOrWhiteSpace(handler.Key.Id))
                throw new ArgumentException("Effect description key is required.", nameof(handler));
            if (_handlers.ContainsKey(handler.Key))
                throw new ArgumentException(
                    "Duplicate effect description key '" + handler.Key + "'.", nameof(handler));
            _handlers.Add(handler.Key, handler);
        }

        public bool Contains(EffectKey key) => _handlers.ContainsKey(key);

        public IEffectDescriptionHandler Resolve(EffectKey key)
            => _handlers.TryGetValue(key, out var handler)
                ? handler
                : throw new KeyNotFoundException(
                    "No effect description handler registered for '" + key + "'.");
    }
}
