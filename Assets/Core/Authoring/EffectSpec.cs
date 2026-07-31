using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Authoring
{
    public enum TargetSelectorRef { None, FrontMost, SecondFromFront, BackMost, Random, All }

    public enum ConditionKind { None, FirstToTrigger, WithinNth, BeforeNextEnemyDamageCard, PrevExecutedIsPlayerDamageCard, NextIsEnemyDamageCard, PrevExecutedIsEnemyDamageCard, NoPrecedingPlayerCard, NoFollowingEnemyCard, NoFollowingPlayerCard, ConsumedStatusAtLeast }

    /// <summary>Closed condition combinator (백로그 §10): the kind enum + central switch stay by design.</summary>
    [Serializable]
    public struct ConditionSpec
    {
        public ConditionKind Kind;
        public int N;
        public int SuccessEffectValue;
        public bool SkipOnBasic;

        public Condition ToCondition()
        {
            switch (Kind)
            {
                case ConditionKind.FirstToTrigger: return new FirstToTrigger();
                case ConditionKind.WithinNth: return new WithinNth(N);
                case ConditionKind.BeforeNextEnemyDamageCard: return new BeforeNextEnemyDamageCard();
                case ConditionKind.PrevExecutedIsPlayerDamageCard:
                    return new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.Damage);
                case ConditionKind.PrevExecutedIsEnemyDamageCard:
                    return new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage);
                case ConditionKind.NextIsEnemyDamageCard:
                    return new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage);
                case ConditionKind.NoPrecedingPlayerCard:
                    return new NoPrecedingCardOfSide(Side.Player);
                case ConditionKind.NoFollowingEnemyCard:
                    return new NoFollowingCardOfSide(Side.Enemy);
                case ConditionKind.NoFollowingPlayerCard:
                    return new NoFollowingCardOfSide(Side.Player);
                case ConditionKind.ConsumedStatusAtLeast:
                    return new ConsumedStatusAtLeast(N);
                default: return null;
            }
        }
    }

    /// <summary>One authored effect. Each concrete spec owns its parameters (real types), its mapping
    /// to core EffectData, its validation, and its codegen literal — adding a new effect touches no
    /// central enum/switch (AGENTS.md rule 9). Registered explicitly in EffectSpecCatalog.</summary>
    [Serializable]
    public abstract class EffectSpec
    {
        public ConditionSpec Condition;

        public abstract EffectKey Key { get; }
        public abstract EffectData ToEffectData();

        /// <summary>C# literal for codegen (SO → GeneratedCards.cs). Lives here so a new effect's
        /// authoring+export stay in one class.</summary>
        public abstract string ToLiteral();

        public virtual IEnumerable<string> Validate(AuthoringContext context)
        {
            yield break;
        }

        protected EffectData ApplyCondition(EffectData effect)
            => Condition.Kind == ConditionKind.None
                ? effect
                : effect with
                {
                    Condition = Condition.ToCondition(),
                    SuccessEffectValue = Condition.SuccessEffectValue,
                    SkipOnBasic = Condition.SkipOnBasic
                };

        protected string ConditionLiteral()
            => "Condition = new ConditionSpec { Kind = ConditionKind." + Condition.Kind
                + ", N = " + Condition.N
                + ", SuccessEffectValue = " + Condition.SuccessEffectValue
                + ", SkipOnBasic = " + (Condition.SkipOnBasic ? "true" : "false") + " }";

        protected static TargetSelector? ToSelector(TargetSelectorRef selector)
        {
            switch (selector)
            {
                case TargetSelectorRef.FrontMost: return TargetSelector.FrontMost;
                case TargetSelectorRef.SecondFromFront: return TargetSelector.SecondFromFront;
                case TargetSelectorRef.BackMost: return TargetSelector.BackMost;
                case TargetSelectorRef.Random: return TargetSelector.Random;
                case TargetSelectorRef.All: return TargetSelector.All;
                default: return null;
            }
        }

        protected static string Quote(string value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
