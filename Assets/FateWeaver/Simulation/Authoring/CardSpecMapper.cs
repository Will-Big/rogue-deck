using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Pure mapping from authored CardSpec to the core CardDefinition. Single place that knows
    /// how the flat authoring enums correspond to core keys / condition records / status / fate actions.</summary>
    public static class CardSpecMapper
    {
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec.Category == CardCategory.Fate)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.Type, 0, Array.Empty<EffectData>())
                {
                    Cost = spec.Cost,
                    Category = CardCategory.Fate,
                    FateAction = new FateActionData(ToFateKey(spec.Fate), spec.Cost, spec.FateAmount)
                };
            }

            var effects = (spec.Effects ?? Array.Empty<EffectSpec>()).Select(ToEffectData).ToArray();
            return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.Type, spec.BaseInitiative, effects)
            {
                Cost = spec.Cost,
                Category = CardCategory.Action
            };
        }

        public static EffectData ToEffectData(EffectSpec e)
        {
            var key = ToEffectKey(e.Kind);
            var hasCondition = e.Condition != ConditionKind.None;

            if (e.Kind == EffectKind.ApplyStatus)
            {
                return new EffectData(key, e.Amount)
                {
                    StatusKey = ToStatusKey(e.Status),
                    StatusLifetime = ToLifetime(e.Lifetime, e.LifetimeCount),
                    StatusTarget = e.Target,
                    Condition = hasCondition ? ToCondition(e) : null,
                    SuccessAmount = hasCondition ? e.SuccessAmount : (int?)null
                };
            }

            return hasCondition
                ? EffectData.Conditional(key, e.Amount, ToCondition(e), e.SuccessAmount)
                : new EffectData(key, e.Amount);
        }

        private static EffectKey ToEffectKey(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.ApplyStatus: return EffectKeys.ApplyStatus;
                case EffectKind.GrantNextAttackBonus: return EffectKeys.GrantNextPlayerAttackDamageBonus;
                case EffectKind.NullifyNextReward: return EffectKeys.NullifyNextPlayerConditionReward;
                default: return EffectKeys.Damage;
            }
        }

        private static Condition ToCondition(EffectSpec e)
        {
            switch (e.Condition)
            {
                case ConditionKind.FirstToTrigger: return new FirstToTrigger();
                case ConditionKind.WithinNth: return new WithinNth(e.ConditionN);
                case ConditionKind.BeforeNextEnemyAttack: return new BeforeNextEnemyAttack();
                case ConditionKind.PrevIsPlayerAttack:
                    return new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, CardType.Attack);
                case ConditionKind.PrevIsEnemyAttack:
                    return new AdjacentCardIs(AdjacentDirection.Previous, Side.Enemy, CardType.Attack);
                case ConditionKind.NextIsEnemyAttack:
                    return new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack);
                case ConditionKind.NoPrecedingPlayerCard:
                    return new NoPrecedingCardOfSide(Side.Player);
                default: return null;
            }
        }

        private static StatusKey ToStatusKey(StatusKindRef s)
        {
            switch (s)
            {
                case StatusKindRef.Stun: return StatusKeys.Stun;
                case StatusKindRef.Vulnerable: return StatusKeys.Vulnerable;
                case StatusKindRef.RewardNullified: return StatusKeys.RewardNullified;
                default: return StatusKeys.Block;
            }
        }

        private static StatusLifetime ToLifetime(StatusLifetimeKind kind, int count)
        {
            switch (kind)
            {
                case StatusLifetimeKind.Permanent: return StatusLifetime.Permanent;
                case StatusLifetimeKind.Turns: return StatusLifetime.Turns(count);
                case StatusLifetimeKind.UntilConsumed: return StatusLifetime.UntilConsumed(count);
                default: return StatusLifetime.ThisTurn;
            }
        }

        private static FateActionKey ToFateKey(FateKind f)
        {
            switch (f)
            {
                case FateKind.SwapInitiative: return FateActionKeys.SwapInitiative;
                case FateKind.Lock: return FateActionKeys.Lock;
                default: return FateActionKeys.ChangeInitiative;
            }
        }
    }
}
