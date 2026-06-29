using System;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    public enum EffectKind { Damage, ApplyStatus, GrantNextAttackBonus, NullifyNextReward }

    public enum ConditionKind { None, FirstToTrigger, WithinNth, BeforeNextEnemyAttack, PrevIsPlayerAttack, NextIsEnemyAttack, PrevIsEnemyAttack, NoPrecedingPlayerCard, NoFollowingEnemyCard }

    public enum StatusKindRef { None, Stun, Vulnerable, Block, RewardNullified, Slow, Haste }

    public enum FateKind { None, ChangeInitiative, SwapInitiative, Lock }

    /// <summary>Flat, Inspector- and codegen-friendly description of one effect. Mapped to core EffectData.</summary>
    [Serializable]
    public struct EffectSpec
    {
        public EffectKind Kind;
        public int Amount;
        public ConditionKind Condition;
        public int ConditionN;
        public int SuccessAmount;
        public StatusKindRef Status;
        public StatusLifetimeKind Lifetime;
        public int LifetimeCount;
        public StatusApplyTarget Target;
    }
}
