using System;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Serializable reference to an open-set status key. Validated (registry membership)
    /// at editor/boot time instead of being a closed enum.</summary>
    [Serializable]
    public struct StatusKeyRef
    {
        public string Id;

        public bool IsEmpty => string.IsNullOrEmpty(Id);
        public StatusKey ToKey() => new StatusKey(Id);
        public static StatusKeyRef Of(StatusKey key) => new StatusKeyRef { Id = key.Id };
    }
}
