using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Conditions
{
    public static class ConditionEvaluator
    {
        public static ConditionTier Evaluate(
            Condition condition,
            ActionCardInstance card,
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
                    && neighbor.Def.Type == adjacent.Type
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is BeforeNextEnemyAttack)
            {
                for (int i = 0; i < index; i++)
                {
                    var earlier = ctx.Order[i];
                    if (earlier.Def.Side == Side.Enemy && earlier.Def.Type == CardType.Attack)
                    {
                        return ConditionTier.Basic;
                    }
                }

                return ConditionTier.Success;
            }

            if (condition is SameTarget)
            {
                var previous = PreviousPlayerCard(ctx, index);
                return previous != null
                    && !string.IsNullOrEmpty(card.TargetId)
                    && card.TargetId == previous.TargetId
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

        private static ActionCardInstance PreviousPlayerCard(ResolutionContext ctx, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                if (ctx.Order[i].Def.Side == Side.Player)
                {
                    return ctx.Order[i];
                }
            }

            return null;
        }
    }
}
