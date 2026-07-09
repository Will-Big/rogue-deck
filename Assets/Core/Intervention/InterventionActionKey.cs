using System;

namespace FateWeaver.Core.Intervention
{
    public readonly struct InterventionActionKey : IEquatable<InterventionActionKey>
    {
        public string Id { get; }

        public InterventionActionKey(string id) => Id = id;

        public bool Equals(InterventionActionKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is InterventionActionKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(InterventionActionKey a, InterventionActionKey b) => a.Equals(b);
        public static bool operator !=(InterventionActionKey a, InterventionActionKey b) => !a.Equals(b);
    }

    public static class InterventionActionKeys
    {
        public static readonly InterventionActionKey ChangeExecutionOrder = new InterventionActionKey("change_execution_order");
        public static readonly InterventionActionKey SwapExecutionOrder = new InterventionActionKey("swap_execution_order");
        public static readonly InterventionActionKey Lock = new InterventionActionKey("lock");
    }
}
