using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>Holds the statuses attached to one holder (entity or card instance).</summary>
    public sealed class StatusBag
    {
        private readonly List<StatusInstance> _statuses = new();

        public IReadOnlyList<StatusInstance> All => _statuses;

        /// <summary>Adds stacks, merging into an existing instance of the same key.</summary>
        public void Add(StatusKey key, int stacks = 1)
        {
            var existing = Get(key);
            if (existing != null)
            {
                existing.Stacks += stacks;
            }
            else
            {
                _statuses.Add(new StatusInstance(key, stacks));
            }
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
    }

    /// <summary>Anything that can carry statuses: an entity (player/enemy) or a card instance.</summary>
    public interface IStatusHolder
    {
        StatusBag Statuses { get; }
    }
}
