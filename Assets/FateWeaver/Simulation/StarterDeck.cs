using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>The 10-card starter deck (7 action : 3 fate). Card-specific initiative values plus
    /// intervention actions and enemy initiative create the ordering puzzle.</summary>
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
            { Cost = 1, Category = CardCategory.Execution };

        public static CardDefinition Guard() => new CardDefinition(
            "guard", "막기", Side.Player, CardType.Defense, DefaultInitiative,
            new[]
            {
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 4)
            })
            { Cost = 1, Category = CardCategory.Execution };

        public static CardDefinition QuickCut() => new CardDefinition(
            "quick_cut", "찰나의 베기", Side.Player, CardType.Attack, DefaultInitiative,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8) })
            { Cost = 1, Category = CardCategory.Execution };

        public static CardDefinition Counter() => new CardDefinition(
            "counter_stance", "반격", Side.Player, CardType.Attack, 7,
            new[]
            {
                EffectData.Conditional(
                    EffectKeys.Damage, 4,
                    new AdjacentCardIs(AdjacentDirection.Previous, Side.Enemy, CardType.Attack), 9)
            })
            { Cost = 2, Category = CardCategory.Execution };

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
            { Cost = 1, Category = CardCategory.Execution };

        // --- intervention cards -----------------------------------------------------

        public static CardDefinition PullForward() => InterventionCard(
            "pull_forward", "앞당김", cost: 1,
            new InterventionActionData(InterventionActionKeys.ChangeInitiative, cost: 1, amount: -2));

        public static CardDefinition SwapPositions() => InterventionCard(
            "swap_positions", "자리 교환", cost: 1,
            new InterventionActionData(InterventionActionKeys.SwapInitiative, cost: 1, amount: 0));

        private static CardDefinition InterventionCard(string id, string name, int cost, InterventionActionData action) =>
            new CardDefinition(id, name, Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
            { Cost = cost, Category = CardCategory.Intervention, InterventionAction = action };

        // --- helper for enemy intent ---------------------------------------

        public static CardDefinition EnemyAttack(string id, string name, int initiative, int damage) =>
            new CardDefinition(id, name, Side.Enemy, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) })
                { Cost = 0, Category = CardCategory.Execution };
    }
}
