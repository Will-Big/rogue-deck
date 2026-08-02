using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Goblin enemy cards + the turn policy that telegraphs them (a variable 1..MaxCardsPerTurn
    /// distinct cards each turn). Rules live here (pure); the policy is the swappable seam.</summary>
    public static class GoblinDeck
    {
        /// <summary>Combat id this enemy is created with (single source so UI/localization can resolve it).</summary>
        public const string EnemyId = "goblin";

        public const int StartingHp = 28;

        /// <summary>Most distinct cards the goblin can telegraph in one turn (auto-capped by the catalog size).</summary>
        public const int MaxCardsPerTurn = 2;

        public static CardDefinition Thrust() => new CardDefinition(
            "goblin_jab", "찌르기", Side.Enemy, 6,
            new[] { new EffectData(EffectKeys.Damage, 4) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition CrudeGuard() => new CardDefinition(
            "crude_guard", "조잡한 방어", Side.Enemy, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 3) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition SlyJab() => new CardDefinition(
            "sly_jab", "약삭빠른 찌르기", Side.Enemy, 3,
            new[] { EffectData.Conditional(EffectKeys.Damage, 3, new NoPrecedingCardOfSide(Side.Player), 6) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        private static readonly Func<CardDefinition>[] Catalog = { Thrust, CrudeGuard, SlyJab };

        /// <summary>Every distinct goblin card the intent can draw — the single place editors/art-seeding
        /// enumerate enemy cards from. Tracks <see cref="Catalog"/>, so new cards appear automatically.</summary>
        public static IReadOnlyList<CardDefinition> AllCards()
        {
            var cards = new CardDefinition[Catalog.Length];
            for (int i = 0; i < Catalog.Length; i++)
            {
                cards[i] = Catalog[i]();
            }

            return cards;
        }

        /// <summary>The goblin's default turn policy: each turn telegraphs a variable 1..MaxCardsPerTurn
        /// distinct cards from the catalog (randomness comes from the combat RNG). Swap for another
        /// IEnemyTurnPolicy to change behavior without touching the combat loop.</summary>
        public static IEnemyTurnPolicy Policy()
            => new RandomMovesetPolicy(AllCards(), minCards: 1, maxCards: MaxCardsPerTurn);
    }
}
