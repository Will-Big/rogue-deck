using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Warden enemy cards. The Warden teaches locked telegraphs by drawing two cards from a
    /// deterministic shuffle bag and locking exactly one of them each turn.</summary>
    public static class WardenDeck
    {
        public const string EnemyId = "warden";
        public const int StartingHp = 20;
        public const int CardsPerTurn = 2;

        public static CardDefinition Swing() => new CardDefinition(
            "warden_swing", "휘두르기", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) })
            { Cost = 0, Category = CardCategory.Action };

        public static CardDefinition Smash() => new CardDefinition(
            "warden_smash", "내려치기", Side.Enemy, CardType.Attack, 5,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new NoFollowingCardOfSide(Side.Enemy), 7) })
            { Cost = 0, Category = CardCategory.Action };

        public static CardDefinition Uppercut() => new CardDefinition(
            "warden_uppercut", "올려치기", Side.Enemy, CardType.Attack, 4,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new NoPrecedingCardOfSide(Side.Enemy), 7) })
            { Cost = 0, Category = CardCategory.Action };

        public static CardDefinition Block() => new CardDefinition(
            "warden_block", "막기", Side.Enemy, CardType.Defense, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3) })
            { Cost = 0, Category = CardCategory.Action };

        public static CardDefinition Brace() => new CardDefinition(
            "warden_brace", "버티기", Side.Enemy, CardType.Defense, 4,
            new[]
            {
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3)
                    with
                    {
                        Condition = new NoPrecedingCardOfSide(Side.Enemy),
                        SuccessAmount = 6
                    }
            })
            { Cost = 0, Category = CardCategory.Action };

        public static IReadOnlyList<CardDefinition> Deck() => new[]
        {
            Swing(),
            Swing(),
            Smash(),
            Uppercut(),
            Block(),
            Brace()
        };

        public static IEnemyTurnPolicy Policy(int seed)
            => new SelfLockPolicy(new ShuffleBagPolicy(Deck(), CardsPerTurn, seed), seed);
    }
}
