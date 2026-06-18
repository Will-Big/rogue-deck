using System;

namespace FateWeaver.Core.Fate
{
    public readonly struct FateActionKey : IEquatable<FateActionKey>
    {
        public string Id { get; }

        public FateActionKey(string id) => Id = id;

        public bool Equals(FateActionKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is FateActionKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(FateActionKey a, FateActionKey b) => a.Equals(b);
        public static bool operator !=(FateActionKey a, FateActionKey b) => !a.Equals(b);
    }

    public static class FateActionKeys
    {
        public static readonly FateActionKey ChangeInitiative = new FateActionKey("change_initiative");
        public static readonly FateActionKey SwapInitiative = new FateActionKey("swap_initiative");
        public static readonly FateActionKey Lock = new FateActionKey("lock");
    }
}
