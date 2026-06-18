using System;

namespace FateWeaver.Core.Effects
{
    /// <summary>Typed wrapper over a string id (open set, type-safe).
    /// Plain readonly struct (NOT record struct) to stay within Unity 6's C# 9.</summary>
    public readonly struct EffectKey : IEquatable<EffectKey>
    {
        public string Id { get; }

        public EffectKey(string id) => Id = id;

        public bool Equals(EffectKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is EffectKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(EffectKey a, EffectKey b) => a.Equals(b);
        public static bool operator !=(EffectKey a, EffectKey b) => !a.Equals(b);
    }

    public static class EffectKeys
    {
        public static readonly EffectKey Damage = new EffectKey("damage");
        public static readonly EffectKey NullifyNextPlayerConditionReward =
            new EffectKey("nullify_next_player_condition_reward");
    }
}
