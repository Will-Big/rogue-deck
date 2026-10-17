using System;

namespace FateWeaver.Core.Effects
{
    /// <summary>문자열 id를 감싼 타입(열린 집합이면서 타입 안전).
    /// Unity 6의 C# 9 안에 머물기 위해 record struct가 아닌 일반 readonly struct로 둔다.</summary>
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
        public static readonly EffectKey GrantNextPlayerDamageCardBonus =
            new EffectKey("grant_next_player_damage_card_bonus");
        public static readonly EffectKey ApplyStatus = new EffectKey("apply_status");
        public static readonly EffectKey MoveFormation = new EffectKey("move_formation");
        public static readonly EffectKey ConsumeStatus = new EffectKey("consume_status");
        public static readonly EffectKey TriggerStatus = new EffectKey("trigger_status");       // 작업 8
        public static readonly EffectKey GrantNextTurnFate = new EffectKey("grant_next_turn_fate"); // 작업 9

        /// <summary>반응 전용: 행위자(반응 보유자)의 상태를 대상에게 옮긴다(전염). 카드로 저작하지 않는다.</summary>
        public static readonly EffectKey TransferStatus = new EffectKey("transfer_status");
    }
}
