using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
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
            Effects = new[] { new EffectSpec { Kind = EffectKind.Damage, EffectValue = 4 } }
        };

        public static CardSpec Guard() => new CardSpec
        {
            Id = "guard", Name = "막기", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 4, Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self } }
        };

        public static CardSpec QuickCut() => new CardSpec
        {
            Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.Damage, EffectValue = 2, Condition = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } }
        };

        public static CardSpec Counter() => new CardSpec
        {
            Id = "counter_stance", Name = "반격", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 7,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.Damage, EffectValue = 4, Condition = ConditionKind.PrevExecutedIsEnemyAttack, SuccessEffectValue = 9 } }
        };

        public static CardSpec Cover() => new CardSpec
        {
            Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 2, Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                Condition = ConditionKind.NextIsEnemyAttack, SuccessEffectValue = 7 } }
        };

        public static CardSpec PullForward() => new CardSpec
        {
            Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.ChangeExecutionOrder, InterventionEffectValue = -1
        };

        public static CardSpec PushBack() => new CardSpec
        {
            Id = "push_back", Name = "밀어내기", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.ChangeExecutionOrder, InterventionEffectValue = 1
        };

        public static CardSpec SwapPositions() => new CardSpec
        {
            Id = "swap_positions", Name = "자리 교환", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.SwapExecutionOrder, InterventionEffectValue = 0
        };

        public static CardSpec SlowHex() => new CardSpec
        {
            Id = "slow_hex", Name = "둔화", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 3, Status = StatusKindRef.Slow,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.TargetEnemy } }
        };

        public static CardSpec QuickenSelf() => new CardSpec
        {
            Id = "quicken_self", Name = "가속", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 3, Status = StatusKindRef.Haste,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.Self } }
        };
    }
}
