using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

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
                    && (!adjacent.Type.HasValue || neighbor.Def.Type == adjacent.Type.Value)
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

            if (condition is NoPrecedingCardOfSide noPreceding)
            {
                // Only counts cards that actually finished resolution: a placed-but-cancelled card
                // (OwnerDied / NoValidTarget / StatusIntercepted) never "preceded" anything. By the
                // time this card resolves, every earlier-index card has already concluded, so its
                // final CancellationReason is settled.
                for (int i = 0; i < index; i++)
                {
                    var earlier = ctx.Order[i];
                    if (earlier.Def.Side == noPreceding.Side && earlier.CancellationReason == null)
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
                    && (!previousExecuted.Type.HasValue || last.Def.Type == previousExecuted.Type.Value)
                        ? ConditionTier.Success
                        : ConditionTier.Basic;
            }

            if (condition is SameTarget)
            {
                // Skips cancelled player cards: only the last player card that actually resolved
                // (and therefore has a meaningful TargetId) counts.
                var previous = ctx.LastExecutedPlayerCard;
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
    }
}
