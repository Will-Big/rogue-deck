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
            "warden_swing", "휘두르기", Side.Enemy, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition Smash() => new CardDefinition(
            "warden_smash", "내려치기", Side.Enemy, 5,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new NoFollowingCardOfSide(Side.Enemy), 7) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition Uppercut() => new CardDefinition(
            "warden_uppercut", "올려치기", Side.Enemy, 4,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new NoPrecedingCardOfSide(Side.Enemy), 7) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition Block() => new CardDefinition(
            "warden_block", "막기", Side.Enemy, 4,
            new[] { EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3) })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static CardDefinition Brace() => new CardDefinition(
            "warden_brace", "버티기", Side.Enemy, 4,
            new[]
            {
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3)
                    with
                    {
                        Condition = new NoPrecedingCardOfSide(Side.Enemy),
                        SuccessEffectValue = 6
                    }
            })
            { EnergyCost = 0, Category = CardCategory.Execution };

        public static IReadOnlyList<CardDefinition> Deck() => new[]
        {
            Swing(),
            Swing(),
            Smash(),
            Uppercut(),
            Block(),
            Brace()
        };

        public static IEnemyTurnPolicy Policy()
            => new SelfLockPolicy(new ShuffleBagPolicy(Deck(), CardsPerTurn));
    }
}
