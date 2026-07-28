using System;

namespace FateWeaver.Simulation.Presentation
{
    // Unit kinds (party member, enemy...) return here when the intervention card design
    // adds unit targets — see the 2026-07-28 P0-C targeting spec, §2 policy 2.
    public enum SelectionTargetKind { None, ExecutionCard }

    public readonly struct SelectionTargetRef : IEquatable<SelectionTargetRef>
    {
        public SelectionTargetKind Kind { get; }
        public int Index { get; }

        private SelectionTargetRef(SelectionTargetKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static SelectionTargetRef ExecutionCard(int index)
            => new SelectionTargetRef(SelectionTargetKind.ExecutionCard, index);

        public bool Equals(SelectionTargetRef other)
            => Kind == other.Kind && Index == other.Index;

        public override bool Equals(object obj)
            => obj is SelectionTargetRef other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ Index;
            }
        }
    }
}
