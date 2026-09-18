using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>상태 키 → 반응 능력. 한 상태는 반응 능력을 하나만 가진다(중복 키 거부).</summary>
    public sealed class ReactionRegistry
    {
        private readonly Dictionary<StatusKey, IReactionHandler> _handlers = new();

        public void Register(IReactionHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(handler.Key))
            {
                throw new ArgumentException("A reaction is already registered for status '" + handler.Key.Id + "'.");
            }

            _handlers.Add(handler.Key, handler);
        }

        public bool TryResolve(StatusKey key, out IReactionHandler handler) => _handlers.TryGetValue(key, out handler);
    }
}
