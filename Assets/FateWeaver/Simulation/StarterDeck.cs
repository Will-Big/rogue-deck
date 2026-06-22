using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>The 10-card starter deck (7 action : 3 fate). Player action cards share a base initiative
    /// so order among them is placement order; fate cards + enemy initiative create the puzzle.</summary>
    public static class StarterDeck
    {
        public const int DefaultInitiative = 5;

        public static IReadOnlyList<CardDefinition> Build()
        {
            var cards = new List<CardDefinition>();
            cards.Add(Slash());
            cards.Add(Slash());
            cards.Add(Guard());
            cards.Add(Guard());
            cards.Add(QuickCut());
            cards.Add(HeavyStrike());
            cards.Add(Cover());
            cards.Add(PullForward());
            cards.Add(PullForward());
            cards.Add(SwapPositions());
            return cards;
        }

        // --- action cards ---------------------------------------------------

        public static CardDefinition Slash() => new CardDefinition(
            "slash", "베기", Side.Player, CardType.Attack, DefaultInitiative,
            new[] { new EffectData(EffectKeys.Damage, 3) })
            { Cost = 1, Category = CardCategory.Action };

        public static CardDefinition Guard() => new CardDefinition(
            "guard", "막기", Side.Player, CardType.Defense, DefaultInitiative,
            new[]
            {
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 4)
            })
            { Cost = 1, Category = CardCategory.Action };

        public static CardDefinition QuickCut() => new CardDefinition(
            "quick_cut", "찰나의 베기", Side.Player, CardType.Attack, DefaultInitiative,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8) })
            { Cost = 1, Category = CardCategory.Action };

        public static CardDefinition HeavyStrike() => new CardDefinition(
            "heavy_strike", "강타", Side.Player, CardType.Attack, DefaultInitiative,
            new[]
            {
                EffectData.Conditional(
                    EffectKeys.Damage, 5,
                    new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, CardType.Attack), 10)
            })
            { Cost = 2, Category = CardCategory.Action };

        public static CardDefinition Cover() => new CardDefinition(
            "cover", "엄호", Side.Player, CardType.Defense, DefaultInitiative,
            new[]
            {
                new EffectData(EffectKeys.ApplyStatus, 2)
                {
                    StatusKey = StatusKeys.Block,
                    StatusLifetime = StatusLifetime.ThisTurn,
                    StatusTarget = StatusApplyTarget.Self,
                    Condition = new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack),
                    SuccessAmount = 7
                }
            })
            { Cost = 1, Category = CardCategory.Action };

        // --- fate cards -----------------------------------------------------

        public static CardDefinition PullForward() => FateCard(
            "pull_forward", "앞당김", cost: 1,
            new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2));

        public static CardDefinition SwapPositions() => FateCard(
            "swap_positions", "자리 교환", cost: 1,
            new FateActionData(FateActionKeys.SwapInitiative, cost: 1, amount: 0));

        private static CardDefinition FateCard(string id, string name, int cost, FateActionData action) =>
            new CardDefinition(id, name, Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
            { Cost = cost, Category = CardCategory.Fate, FateAction = action };

        // --- helper for enemy intent ---------------------------------------

        public static CardDefinition EnemyAttack(string id, string name, int initiative, int damage) =>
            new CardDefinition(id, name, Side.Enemy, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) })
                { Cost = 0, Category = CardCategory.Action };
    }
}
