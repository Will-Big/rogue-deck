using System;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    public enum TargetSelectorRef { None, FrontMost, SecondFromFront, BackMost, Random }

    public enum EffectKind { Damage, ApplyStatus, GrantNextAttackBonus, NullifyNextReward, MoveFormation }

    public enum ConditionKind { None, FirstToTrigger, WithinNth, BeforeNextEnemyAttack, PrevExecutedIsPlayerAttack, NextIsEnemyAttack, PrevExecutedIsEnemyAttack, NoPrecedingPlayerCard, NoFollowingEnemyCard }

    public enum StatusKindRef { None, Stun, Vulnerable, Block, RewardNullified, Slow, Haste }

    public enum InterventionKind { None, ChangeExecutionOrder, SwapExecutionOrder, Lock }

    /// <summary>Flat, Inspector- and codegen-friendly description of one effect. Mapped to core EffectData.</summary>
    [Serializable]
    public struct EffectSpec
    {
        public EffectKind Kind;
        public int EffectValue;
        public ConditionKind Condition;
        public int ConditionN;
        public int SuccessEffectValue;
        public StatusKindRef Status;
        public StatusLifetimeKind Lifetime;
        public int LifetimeCount;
        public StatusApplyTarget Target;
        public TargetSelectorRef Selector;
    }
}
