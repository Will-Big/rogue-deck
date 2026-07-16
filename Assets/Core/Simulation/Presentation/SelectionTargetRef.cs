using System;

namespace FateWeaver.Simulation.Presentation
{
    public enum SelectionTargetKind { None, ExecutionCard, PartyMember, Enemy }

    public readonly struct SelectionTargetRef : IEquatable<SelectionTargetRef>
    {
        public SelectionTargetKind Kind { get; }
        public int Index { get; }
        public string EntityId { get; }

        private SelectionTargetRef(SelectionTargetKind kind, int index, string entityId)
        {
            Kind = kind;
            Index = index;
            EntityId = entityId;
        }

        public static SelectionTargetRef ExecutionCard(int index)
            => new SelectionTargetRef(SelectionTargetKind.ExecutionCard, index, null);

        public static SelectionTargetRef PartyMember(string id)
            => new SelectionTargetRef(SelectionTargetKind.PartyMember, -1, id);

        public static SelectionTargetRef Enemy(string id)
            => new SelectionTargetRef(SelectionTargetKind.Enemy, -1, id);

        public bool Equals(SelectionTargetRef other)
            => Kind == other.Kind && Index == other.Index
                && string.Equals(EntityId, other.EntityId, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is SelectionTargetRef other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((int)Kind * 397) ^ Index;
                return (hash * 397) ^ (EntityId == null ? 0 : EntityId.GetHashCode());
            }
        }
    }
}
