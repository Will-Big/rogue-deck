using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>The 10-card starter deck (7 action : 3 fate). Card-specific executionOrder values plus
    /// intervention actions and enemy executionOrder create the ordering puzzle.</summary>
    public static class StarterDeck
    {
        public const int DefaultExecutionOrder = 5;

        public static IReadOnlyList<CardDefinition> Build()
        {
            var cards = new List<CardDefinition>();
            cards.Add(Slash());
            cards.Add(Slash());
            cards.Add(Guard());
            cards.Add(Guard());
            cards.Add(QuickCut());
            cards.Add(Counter());
            cards.Add(Cover());
            cards.Add(PullForward());
            cards.Add(PullForward());
            cards.Add(SwapPositions());
            return cards;
        }

        // --- execution cards ---------------------------------------------------

        public static CardDefinition Slash() => new CardDefinition(
            "slash", "베기", Side.Player, CardType.Attack, 4,
            new[] { new EffectData(EffectKeys.Damage, 4) })
            { EnergyCost = 1, Category = CardCategory.Execution };

        public static CardDefinition Guard() => new CardDefinition(
            "guard", "막기", Side.Player, CardType.Defense, DefaultExecutionOrder,
            new[]
            {
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 4)
            })
            { EnergyCost = 1, Category = CardCategory.Execution };

        public static CardDefinition QuickCut() => new CardDefinition(
            "quick_cut", "찰나의 베기", Side.Player, CardType.Attack, DefaultExecutionOrder,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8) })
            { EnergyCost = 1, Category = CardCategory.Execution };

        public static CardDefinition Counter() => new CardDefinition(
            "counter_stance", "반격", Side.Player, CardType.Attack, 7,
            new[]
            {
                EffectData.Conditional(
                    EffectKeys.Damage, 4,
                    new PreviousExecutedCardIs(Side.Enemy, CardType.Attack), 9)
            })
            { EnergyCost = 2, Category = CardCategory.Execution };

        public static CardDefinition Cover() => new CardDefinition(
            "cover", "엄호", Side.Player, CardType.Defense, DefaultExecutionOrder,
            new[]
            {
                new EffectData(EffectKeys.ApplyStatus, 2)
                {
                    StatusKey = StatusKeys.Block,
                    StatusLifetime = StatusLifetime.ThisTurn,
                    StatusTarget = StatusApplyTarget.Self,
                    Condition = new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack),
                    SuccessEffectValue = 7
                }
            })
            { EnergyCost = 1, Category = CardCategory.Execution };

        // --- intervention cards -----------------------------------------------------

        public static CardDefinition PullForward() => InterventionCard(
            "pull_forward", "앞당김", interventionCost: 1,
            new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2));

        public static CardDefinition SwapPositions() => InterventionCard(
            "swap_positions", "자리 교환", interventionCost: 1,
            new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, effectValue: 0));

        private static CardDefinition InterventionCard(string id, string name, int interventionCost, InterventionActionData action) =>
            new CardDefinition(id, name, Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
            { EnergyCost = interventionCost, Category = CardCategory.Intervention, InterventionAction = action };

        // --- helper for enemy intent ---------------------------------------

        public static CardDefinition EnemyAttack(string id, string name, int executionOrder, int damage) =>
            new CardDefinition(id, name, Side.Enemy, CardType.Attack, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) })
                { EnergyCost = 0, Category = CardCategory.Execution };
    }
}
