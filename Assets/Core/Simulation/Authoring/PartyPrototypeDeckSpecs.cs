using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Authoring-shaped mirror of the neutral party validation deck.</summary>
    public static class PartyPrototypeDeckSpecs
    {
        private const int AttackExecutionOrder = 4;
        private const int DefaultExecutionOrder = 5;
        private const int DefaultEnergyCost = 1;
        private const int AttackDamage = 4;
        private const int BlockMagnitude = 4;
        private const int MoveForwardDistance = -1;

        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            Attack(), Attack(),
            SelectedBlock(), SelectedBlock(),
            AllBlock(),
            MoveForward()
        };

        public static CardSpec Attack() => ExecutionSpec(
            "fixture_attack",
            "[검증] 공격",
            CardType.Attack,
            AttackExecutionOrder,
            new EffectSpec
            {
                Kind = EffectKind.Damage,
                EffectValue = AttackDamage
            });

        public static CardSpec SelectedBlock() => ExecutionSpec(
            "fixture_selected_block",
            "[검증] 선택 방어",
            CardType.Defense,
            DefaultExecutionOrder,
            new EffectSpec
            {
                Kind = EffectKind.ApplyStatus,
                EffectValue = BlockMagnitude,
                Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn,
                Target = StatusApplyTarget.Self
            });

        public static CardSpec AllBlock() => ExecutionSpec(
            "fixture_all_block",
            "[검증] 전체 방어",
            CardType.Defense,
            DefaultExecutionOrder,
            new EffectSpec
            {
                Kind = EffectKind.ApplyStatus,
                EffectValue = BlockMagnitude,
                Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn,
                Target = StatusApplyTarget.AllPartyMembers
            });

        public static CardSpec MoveForward() => ExecutionSpec(
            "fixture_move_forward",
            "[검증] 대형 이동",
            CardType.Skill,
            DefaultExecutionOrder,
            new EffectSpec
            {
                Kind = EffectKind.MoveFormation,
                EffectValue = MoveForwardDistance
            });

        private static CardSpec ExecutionSpec(
            string id,
            string name,
            CardType type,
            int executionOrder,
            EffectSpec effect)
            => new CardSpec
            {
                Id = id,
                Name = name,
                Side = Side.Player,
                Type = type,
                Category = CardCategory.Execution,
                EnergyCost = DefaultEnergyCost,
                BaseExecutionOrder = executionOrder,
                Effects = new[] { effect }
            };
    }
}
