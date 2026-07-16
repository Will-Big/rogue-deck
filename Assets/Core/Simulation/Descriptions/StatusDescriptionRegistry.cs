using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class StatusDescriptionRegistry
    {
        private readonly Dictionary<StatusKey, string> _names =
            new Dictionary<StatusKey, string>();

        public void Register(StatusKey key, string displayName)
        {
            if (string.IsNullOrWhiteSpace(key.Id))
                throw new ArgumentException("Status key is required.", nameof(key));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Status display name is required.", nameof(displayName));
            if (_names.ContainsKey(key))
                throw new ArgumentException(
                    "Duplicate status description key '" + key + "'.", nameof(key));
            _names.Add(key, displayName);
        }

        public bool Contains(StatusKey key) => _names.ContainsKey(key);

        public string Resolve(StatusKey key)
            => _names.TryGetValue(key, out var displayName)
                ? displayName
                : throw new KeyNotFoundException(
                    "No status description registered for '" + key + "'.");
    }
}
