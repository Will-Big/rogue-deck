using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>시작 카드 풀 22장 (Tools/card-idea-notebook/시작 카드 풀.md, 2026-07-29).
    /// StarterDeckSpecs와 같은 순수 CardSpec 저작 — SO 미러링은 병합 후 메인 체크아웃에서 진행.</summary>
    public static class StarterPoolSpecs
    {
        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            VanguardSlash(), ParryStrike(), Hasten(), ProbingStrike(), QuickCover(), Delay(),
            DelayedStrike(), EarlyGuard(), Crossover(), Riposte(), Foresight(), Breather()
            // Task 13: 독 카드 10장이 여기 추가된다.
        };

        public static CardSpec VanguardSlash() => new CardSpec
        {
            Id = "vanguard_slash", Name = "선봉 베기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new EffectSpec[] { new DamageSpec { Value = 5 } }
        };

        public static CardSpec ParryStrike() => new CardSpec
        {
            Id = "parry_strike", Name = "쳐내기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new DamageSpec { Value = 1 },
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 3,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec Hasten() => new CardSpec
        {
            Id = "hasten", Name = "재촉", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = -1,
            InterventionTargetSide = InterventionTargetSideRef.Player
        };

        public static CardSpec ProbingStrike() => new CardSpec
        {
            Id = "probing_strike", Name = "견제타", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
            {
                new DamageSpec { Value = 4 },
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 1,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec QuickCover() => new CardSpec
        {
            Id = "quick_cover", Name = "빠른 엄호", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
            {
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                    Lifetime = StatusLifetimeKind.ThisTurn,
                    Target = StatusApplyTarget.PartyBySelector, Selector = TargetSelectorRef.FrontMost
                }
            }
        };

        public static CardSpec Delay() => new CardSpec
        {
            Id = "delay", Name = "유예", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = 1,
            InterventionTargetSide = InterventionTargetSideRef.Enemy
        };

        public static CardSpec DelayedStrike() => new CardSpec
        {
            Id = "delayed_strike", Name = "늦춘 일격", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new DamageSpec { Value = 5 } }
        };

        public static CardSpec EarlyGuard() => new CardSpec
        {
            Id = "early_guard", Name = "앞선 대비", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
            {
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec Crossover() => new CardSpec
        {
            Id = "crossover", Name = "엇갈림", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.SwapExecutionOrder),
            InterventionRequireAdjacent = true
        };

        public static CardSpec Riposte() => new CardSpec
        {
            Id = "riposte", Name = "응수", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new DamageSpec
                {
                    Value = 3,
                    Condition = new ConditionSpec
                    {
                        Kind = ConditionKind.PrevExecutedIsEnemyDamageCard, SuccessEffectValue = 7
                    }
                }
            }
        };

        public static CardSpec Foresight() => new CardSpec
        {
            Id = "foresight", Name = "예견", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 2,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                    Condition = new ConditionSpec
                    {
                        Kind = ConditionKind.NextIsEnemyDamageCard, SuccessEffectValue = 6
                    }
                }
            }
        };

        public static CardSpec Breather() => new CardSpec
        {
            Id = "breather", Name = "숨 고르기", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = 1,
            InterventionTargetSide = InterventionTargetSideRef.Player
        };
    }
}
