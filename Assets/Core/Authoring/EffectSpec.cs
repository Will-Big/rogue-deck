using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    public enum TargetSelectorRef
    {
        None = 0,
        FrontOne = 1,
        BackOne = 3,
        All = 5,
        FrontTwo = 6,
        BackTwo = 7
    }

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

        [JsonIgnore]
        public abstract EffectKey Key { get; }
        public abstract EffectData ToEffectData();

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

        protected static TargetSelector? ToSelector(TargetSelectorRef selector)
        {
            switch (selector)
            {
                case TargetSelectorRef.None: return null;
                case TargetSelectorRef.FrontOne: return TargetSelector.FrontOne;
                case TargetSelectorRef.FrontTwo: return TargetSelector.FrontTwo;
                case TargetSelectorRef.BackOne: return TargetSelector.BackOne;
                case TargetSelectorRef.BackTwo: return TargetSelector.BackTwo;
                case TargetSelectorRef.All: return TargetSelector.All;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(selector), selector, "Unsupported target selector value.");
            }
        }

        protected IEnumerable<string> ValidateSelector(TargetSelectorRef selector)
        {
            if (selector != TargetSelectorRef.None
                && !Enum.IsDefined(typeof(TargetSelectorRef), selector))
            {
                yield return "unsupported target selector value " + (int)selector + ".";
            }
        }

        protected static string Quote(string value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
