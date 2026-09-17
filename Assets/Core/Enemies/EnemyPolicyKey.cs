using System;

namespace FateWeaver.Core.Enemies
{
    /// <summary>적 정책 키. 적 JSON의 "policy" 값이다. EffectKey와 같은 이유로 record struct가 아닌
    /// 일반 readonly struct다(Unity 6의 C# 9).</summary>
    public readonly struct EnemyPolicyKey : IEquatable<EnemyPolicyKey>
    {
        public string Id { get; }

        public EnemyPolicyKey(string id) => Id = id;

        public bool Equals(EnemyPolicyKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is EnemyPolicyKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(EnemyPolicyKey a, EnemyPolicyKey b) => a.Equals(b);
        public static bool operator !=(EnemyPolicyKey a, EnemyPolicyKey b) => !a.Equals(b);
    }

    public static class EnemyPolicyKeys
    {
        public static readonly EnemyPolicyKey RandomPick = new EnemyPolicyKey("random_pick");
        public static readonly EnemyPolicyKey ShuffleBag = new EnemyPolicyKey("shuffle_bag");
        public static readonly EnemyPolicyKey Sequence = new EnemyPolicyKey("sequence");
    }
}
