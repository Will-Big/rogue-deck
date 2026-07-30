using System;

namespace FateWeaver.Core.Status
{
    /// <summary>Typed wrapper over a string id (open set, type-safe). Mirrors EffectKey/InterventionActionKey.
    /// Plain readonly struct (NOT record struct) to stay within Unity 6's C# 9.</summary>
    public readonly struct StatusKey : IEquatable<StatusKey>
    {
        public string Id { get; }

        public StatusKey(string id) => Id = id;

        public bool Equals(StatusKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is StatusKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(StatusKey a, StatusKey b) => a.Equals(b);
        public static bool operator !=(StatusKey a, StatusKey b) => !a.Equals(b);
    }

    public static class StatusKeys
    {
        public static readonly StatusKey Stun = new StatusKey("stun");
        public static readonly StatusKey Vulnerable = new StatusKey("vulnerable");
        public static readonly StatusKey RewardNullified = new StatusKey("reward_nullified");
        public static readonly StatusKey Block = new StatusKey("block");
        public static readonly StatusKey Slow = new StatusKey("slow");
        public static readonly StatusKey Haste = new StatusKey("haste");
        public static readonly StatusKey Poison = new StatusKey("poison");
        public static readonly StatusKey PoisonDormant = new StatusKey("poison_dormant");
        public static readonly StatusKey PoisonStasis = new StatusKey("poison_stasis");
        public static readonly StatusKey Contagion = new StatusKey("contagion");
        public static readonly StatusKey Weak = new StatusKey("weak");
    }
}
