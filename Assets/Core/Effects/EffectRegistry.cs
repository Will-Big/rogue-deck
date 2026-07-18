using System.Collections.Generic;

namespace FateWeaver.Core.Effects
{
    public sealed class EffectRegistry
    {
        private readonly Dictionary<EffectKey, IEffectHandler> _handlers = new();

        public void Register(IEffectHandler handler) => _handlers[handler.Key] = handler;

        public bool Contains(EffectKey key) => _handlers.ContainsKey(key);

        public IEffectHandler Resolve(EffectKey key)
            => _handlers.TryGetValue(key, out var h)
                ? h
                : throw new KeyNotFoundException($"No effect handler registered for '{key}'");
    }
}
