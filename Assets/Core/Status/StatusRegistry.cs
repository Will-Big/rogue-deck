using System;
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Core.Status
{
    /// <summary>StatusKey -> IStatusBehavior. New statuses register here without touching the resolver.</summary>
    public sealed class StatusRegistry
    {
        private readonly Dictionary<StatusKey, IStatusBehavior> _behaviors = new();

        public IReadOnlyList<StatusKey> RegisteredKeys
            => _behaviors.Keys.OrderBy(key => key.Id, StringComparer.Ordinal).ToArray();

        public void Register(IStatusBehavior behavior) => _behaviors[behavior.Key] = behavior;

        public IStatusBehavior Resolve(StatusKey key)
            => _behaviors.TryGetValue(key, out var behavior)
                ? behavior
                : throw new KeyNotFoundException($"No status behavior registered for '{key}'");

        public bool TryResolve(StatusKey key, out IStatusBehavior behavior)
            => _behaviors.TryGetValue(key, out behavior);
    }
}
