using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>The 10-card starter deck expressed as flat CardSpecs (mirrors the hand-coded StarterDeck,
    /// which stays as the equivalence oracle). The SO/codegen path produces specs of this shape.</summary>
    public static class StarterDeckSpecs
    {
        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            Slash(), Slash(), Guard(), Guard(), QuickCut(), HeavyStrike(), Cover(),
            PullForward(), PullForward(), SwapPositions()
        };

        public static CardSpec Slash() => new CardSpec
        {
            Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Action, Cost = 1, BaseInitiative = 5,
            Effects = new[] { new EffectSpec { Kind = EffectKind.Damage, Amount = 3 } }
        };

        public static CardSpec Guard() => new CardSpec
        {
            Id = "guard", Name = "막기", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Action, Cost = 1, BaseInitiative = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 4, Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self } }
        };

        public static CardSpec QuickCut() => new CardSpec
        {
            Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Action, Cost = 1, BaseInitiative = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.Damage, Amount = 2, Condition = ConditionKind.FirstToTrigger, SuccessAmount = 8 } }
        };

        public static CardSpec HeavyStrike() => new CardSpec
        {
            Id = "heavy_strike", Name = "강타", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Action, Cost = 2, BaseInitiative = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.Damage, Amount = 5, Condition = ConditionKind.PrevIsPlayerAttack, SuccessAmount = 10 } }
        };

        public static CardSpec Cover() => new CardSpec
        {
            Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Action, Cost = 1, BaseInitiative = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 2, Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                Condition = ConditionKind.NextIsEnemyAttack, SuccessAmount = 7 } }
        };

        public static CardSpec PullForward() => new CardSpec
        {
            Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Fate, Cost = 1, Fate = FateKind.ChangeInitiative, FateAmount = -2
        };

        public static CardSpec SwapPositions() => new CardSpec
        {
            Id = "swap_positions", Name = "자리 교환", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Fate, Cost = 1, Fate = FateKind.SwapInitiative, FateAmount = 0
        };
    }
}
