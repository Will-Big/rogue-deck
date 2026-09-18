using System;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Conditions
{
    public static class ConditionEvaluator
    {
        public static ConditionTier Evaluate(
            Condition condition,
            ExecutionCardInstance card,
            ResolutionContext ctx)
        {
            var index = ctx.IndexOf(card);
            if (index < 0)
            {
                throw new InvalidOperationException("Card is not present in the resolution context.");
            }

            if (condition is FirstToTrigger)
            {
                return index == 0 ? ConditionTier.Success : ConditionTier.Basic;
            }

            if (condition is WithinNth withinNth)
            {
                return index < withinNth.N ? ConditionTier.Success : ConditionTier.Basic;
            }

            if (condition is AdjacentCardIs adjacent)
            {
                var offset = adjacent.Direction == AdjacentDirection.Previous ? -1 : 1;
                var neighbor = ctx.CardAt(index + offset);
                return neighbor != null
                    && neighbor.Def.Side == adjacent.Side
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is AdjacentCardHasEffect adjacentEffect)
            {
                var offset = adjacentEffect.Direction == AdjacentDirection.Previous ? -1 : 1;
                var neighbor = ctx.CardAt(index + offset);
                return neighbor != null
                    && neighbor.Def.Side == adjacentEffect.Side
                    && neighbor.Def.HasEffect(adjacentEffect.EffectKey)
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is BeforeNextEnemyDamageCard)
            {
                for (int i = 0; i < index; i++)
                {
                    var earlier = ctx.Order[i];
                    if (earlier.Def.Side == Side.Enemy
                        && earlier.Def.HasEffect(EffectKeys.Damage))
                    {
                        return ConditionTier.Basic;
                    }
                }

                return ConditionTier.Success;
            }

            if (condition is NoPrecedingCardOfSide noPreceding)
            {
                // 배치 질의는 현재 실행선을 본다. 주인이 죽어 빠진 카드는 이미 실행선에 없고,
                // 차례가 왔지만 효과가 없었던 카드는 앞 카드로 센다(스펙 §6).
                for (int i = 0; i < index; i++)
                {
                    var earlier = ctx.Order[i];
                    if (earlier.Def.Side == noPreceding.Side)
                    {
                        return ConditionTier.Basic;
                    }
                }

                return ConditionTier.Success;
            }

            if (condition is NoFollowingCardOfSide noFollowing)
            {
                for (int i = index + 1; i < ctx.Order.Count; i++)
                {
                    if (ctx.Order[i].Def.Side == noFollowing.Side)
                    {
                        return ConditionTier.Basic;
                    }
                }

                return ConditionTier.Success;
            }

            if (condition is PreviousExecutedCardIs previousExecuted)
            {
                var last = ctx.LastExecutedCard;
                return last != null
                    && last.Def.Side == previousExecuted.Side
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is PreviousExecutedCardHasEffect previousEffect)
            {
                var last = ctx.LastExecutedCard;
                return last != null
                    && last.Def.Side == previousEffect.Side
                    && last.Def.HasEffect(previousEffect.EffectKey)
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is SameTarget)
            {
                // 실행 이력의 마지막 플레이어 카드와 비교한다. 차례가 왔지만 대상을 못 찾은 카드도 이력에 있다.
                var previous = ctx.LastExecutedPlayerCard;
                return previous != null
                    && !string.IsNullOrEmpty(card.TargetId)
                    && card.TargetId == previous.TargetId
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is ConsumedStatusAtLeast consumedAtLeast)
            {
                return card.ConsumedStatusAmount >= consumedAtLeast.N
                    ? ConditionTier.Success
                    : ConditionTier.Basic;
            }

            if (condition is AllOf allOf)
            {
                var tier = ConditionTier.Success;
                foreach (var child in allOf.Conditions)
                {
                    var childTier = Evaluate(child, card, ctx);
                    if (childTier < tier)
                    {
                        tier = childTier;
                    }
                }

                return tier;
            }

            throw new NotSupportedException($"Unsupported condition type '{condition.GetType().Name}'.");
        }
    }
}
