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
            DelayedStrike(), EarlyGuard(), Crossover(), Riposte(), Foresight(), Breather(),
            VenomThrust(), LastDrop(), SporeVeil(), SpreadCulture(), ToxicReclaim(),
            CondensedBurst(), Distill(), EarlyOnset(), StableCulture(), PosthumousSpread()
        };

        private static ApplyStatusSpec PoisonApply(int value) => new ApplyStatusSpec
        {
            Status = StatusKeyRef.Of(StatusKeys.Poison), Value = value,
            Lifetime = StatusLifetimeKind.Permanent, Target = StatusApplyTarget.TargetEnemy,
            Selector = TargetSelectorRef.FrontMost
        };

        public static CardSpec VanguardSlash() => new CardSpec
        {
            Id = "vanguard_slash", Name = "선봉 베기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new EffectSpec[]
                { new DamageSpec { Value = 5, Selector = TargetSelectorRef.FrontMost } }
        };

        public static CardSpec ParryStrike() => new CardSpec
        {
            Id = "parry_strike", Name = "쳐내기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new DamageSpec { Value = 1, Selector = TargetSelectorRef.FrontMost },
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
                new DamageSpec { Value = 4, Selector = TargetSelectorRef.FrontMost },
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
            Effects = new EffectSpec[]
                { new DamageSpec { Value = 5, Selector = TargetSelectorRef.FrontMost } }
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
                    Value = 3, Selector = TargetSelectorRef.FrontMost,
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

        public static CardSpec VenomThrust() => new CardSpec
        {
            Id = "venom_thrust", Name = "맹독 찌르기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
                { new DamageSpec { Value = 2, Selector = TargetSelectorRef.FrontMost }, PoisonApply(1) }
        };

        public static CardSpec LastDrop()
        {
            var poison = PoisonApply(1);
            poison.Condition = new ConditionSpec
            {
                Kind = ConditionKind.NoFollowingPlayerCard, SuccessEffectValue = 2
            };
            return new CardSpec
            {
                Id = "last_drop", Name = "마지막 한 방울", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 7,
                Effects = new EffectSpec[] { poison }
            };
        }

        public static CardSpec SporeVeil() => new CardSpec
        {
            Id = "spore_veil", Name = "포자막", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                PoisonApply(1),
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 2,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec SpreadCulture()
        {
            var poison = PoisonApply(1);
            poison.Selector = TargetSelectorRef.All;
            return new CardSpec
            {
                Id = "spread_culture", Name = "확산 배양", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 6,
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Value = 2, Selector = TargetSelectorRef.All },
                    poison
                }
            };
        }

        public static CardSpec ToxicReclaim()
        {
            var block = new ApplyStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                Condition = new ConditionSpec
                {
                    Kind = ConditionKind.ConsumedStatusAtLeast, N = 1,
                    SuccessEffectValue = 4, SkipOnBasic = true
                }
            };
            return new CardSpec
            {
                Id = "toxic_reclaim", Name = "독성 환원", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new EffectSpec[]
                {
                    new ConsumeStatusSpec
                    {
                        Status = StatusKeyRef.Of(StatusKeys.Poison), MaxAmount = 1,
                        Selector = TargetSelectorRef.FrontMost
                    },
                    PoisonApply(1),
                    block
                }
            };
        }

        public static CardSpec CondensedBurst() => new CardSpec
        {
            Id = "condensed_burst", Name = "응축 파열", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 6,
            Effects = new EffectSpec[]
            {
                new ConsumeStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Poison), MaxAmount = 3, DamageBonusPerConsumed = 2,
                    Selector = TargetSelectorRef.FrontMost
                },
                new DamageSpec { Value = 2, Selector = TargetSelectorRef.FrontMost },
                PoisonApply(1)
            }
        };

        public static CardSpec Distill()
        {
            var fate = new GrantNextTurnFateSpec
            {
                Value = 1,
                Condition = new ConditionSpec
                {
                    Kind = ConditionKind.ConsumedStatusAtLeast, N = 1,
                    SuccessEffectValue = 1, SkipOnBasic = true
                }
            };
            return new CardSpec
            {
                Id = "distill", Name = "증류", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new EffectSpec[]
                {
                    new ConsumeStatusSpec
                    {
                        Status = StatusKeyRef.Of(StatusKeys.Poison), MaxAmount = 1,
                        Selector = TargetSelectorRef.FrontMost
                    },
                    PoisonApply(1),
                    fate
                }
            };
        }

        public static CardSpec EarlyOnset() => new CardSpec
        {
            Id = "early_onset", Name = "조기 발병", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 3,
            Effects = new EffectSpec[]
            {
                PoisonApply(1),
                new TriggerStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Poison),
                    SuppressMarker = StatusKeyRef.Of(StatusKeys.PoisonDormant),
                    Selector = TargetSelectorRef.FrontMost
                }
            }
        };

        public static CardSpec StableCulture()
        {
            var poison = PoisonApply(2);
            poison.Selector = TargetSelectorRef.BackMost;
            var stasis = new ApplyStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.PoisonStasis), Value = 0,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.TargetEnemy,
                Selector = TargetSelectorRef.BackMost
            };
            return new CardSpec
            {
                Id = "stable_culture", Name = "안정 배양", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new EffectSpec[] { poison, stasis }
            };
        }

        public static CardSpec PosthumousSpread()
        {
            var contagion = new ApplyStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Contagion), Value = 0,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2,
                Target = StatusApplyTarget.TargetEnemy, Selector = TargetSelectorRef.FrontMost
            };
            return new CardSpec
            {
                Id = "posthumous_spread", Name = "사후 전염", Side = Side.Player,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
                Effects = new EffectSpec[]
                {
                    new DamageSpec { Value = 1, Selector = TargetSelectorRef.FrontMost },
                    PoisonApply(1),
                    contagion
                }
            };
        }
    }
}
