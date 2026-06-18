using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>Holds the statuses attached to one holder (entity or card instance).
    /// One instance per key; re-applying refreshes its lifetime.</summary>
    public sealed class StatusBag
    {
        private readonly List<StatusInstance> _statuses = new();

        public IReadOnlyList<StatusInstance> All => _statuses;

        /// <summary>Applies a status with the given lifetime (replaces any existing instance of the key).</summary>
        public void Add(StatusKey key, StatusLifetime lifetime)
        {
            var existing = Get(key);
            if (existing != null)
            {
                _statuses.Remove(existing);
            }

            _statuses.Add(new StatusInstance(key, lifetime));
        }

        public StatusInstance Get(StatusKey key)
        {
            foreach (var status in _statuses)
            {
                if (status.Key == key)
                {
                    return status;
                }
            }

            return null;
        }

        public bool Has(StatusKey key) => Get(key) != null;

        public bool Remove(StatusKey key)
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].Key == key)
                {
                    _statuses.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Spends one charge of an UntilConsumed status (call after its hook actually fired);
        /// removes it at zero. No-op for other lifetime kinds.</summary>
        public void Consume(StatusInstance status)
        {
            if (status == null || status.Kind != StatusLifetimeKind.UntilConsumed)
            {
                return;
            }

            status.Count--;
            if (status.Count <= 0)
            {
                _statuses.Remove(status);
            }
        }

        /// <summary>End-of-turn maintenance: drop ThisTurn statuses; tick down Turns statuses (remove at 0).</summary>
        public void EndOfTurn()
        {
            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                var status = _statuses[i];
                if (status.Kind == StatusLifetimeKind.ThisTurn)
                {
                    _statuses.RemoveAt(i);
                }
                else if (status.Kind == StatusLifetimeKind.Turns)
                {
                    status.Count--;
                    if (status.Count <= 0)
                    {
                        _statuses.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>Anything that can carry statuses: an entity (player/enemy) or a card instance.</summary>
    public interface IStatusHolder
    {
        StatusBag Statuses { get; }
    }
}
