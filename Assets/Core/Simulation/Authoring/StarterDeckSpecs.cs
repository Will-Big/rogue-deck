using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>The 10-card starter deck expressed as flat CardSpecs (mirrors the hand-coded StarterDeck,
    /// which stays as the equivalence oracle). The SO/codegen path produces specs of this shape.</summary>
    public static class StarterDeckSpecs
    {
        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            Slash(), Slash(), Guard(), Guard(), QuickCut(), Counter(), Cover(),
            PullForward(), PushBack(), SwapPositions()
        };

        public static CardSpec Slash() => new CardSpec
        {
            Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[] { new DamageSpec { Value = 4 } }
        };

        public static CardSpec Guard() => new CardSpec
        {
            Id = "guard", Name = "막기", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new ApplyStatusSpec {
                Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self } }
        };

        public static CardSpec QuickCut() => new CardSpec
        {
            Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new DamageSpec { Value = 2,
                Condition = new ConditionSpec { Kind = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } } }
        };

        public static CardSpec Counter() => new CardSpec
        {
            Id = "counter_stance", Name = "반격", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 7,
            Effects = new EffectSpec[] { new DamageSpec { Value = 4,
                Condition = new ConditionSpec { Kind = ConditionKind.PrevExecutedIsEnemyDamageCard, SuccessEffectValue = 9 } } }
        };

        public static CardSpec Cover() => new CardSpec
        {
            Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new ApplyStatusSpec {
                Status = StatusKeyRef.Of(StatusKeys.Block), Value = 2,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                Condition = new ConditionSpec { Kind = ConditionKind.NextIsEnemyDamageCard, SuccessEffectValue = 7 } } }
        };

        public static CardSpec PullForward() => new CardSpec
        {
            Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = -1
        };

        public static CardSpec PushBack() => new CardSpec
        {
            Id = "push_back", Name = "밀어내기", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = 1
        };

        public static CardSpec SwapPositions() => new CardSpec
        {
            Id = "swap_positions", Name = "자리 교환", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.SwapExecutionOrder),
            InterventionEffectValue = 0
        };

        public static CardSpec SlowHex() => new CardSpec
        {
            Id = "slow_hex", Name = "둔화", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new EffectSpec[] { new ApplyStatusSpec {
                Status = StatusKeyRef.Of(StatusKeys.Slow), Value = 3,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.TargetEnemy } }
        };

        public static CardSpec QuickenSelf() => new CardSpec
        {
            Id = "quicken_self", Name = "가속", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new EffectSpec[] { new ApplyStatusSpec {
                Status = StatusKeyRef.Of(StatusKeys.Haste), Value = 3,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.Self } }
        };
    }
}
