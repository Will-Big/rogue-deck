using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>Neutral validation-only cards used by the two-member party prototype.</summary>
    public static class PartyPrototypeDeck
    {
        private const int AttackExecutionOrder = 4;
        private const int DefaultEnergyCost = 1;
        private const int AttackDamage = 4;
        private const int BlockMagnitude = 4;
        private const int MoveForwardDistance = -1;

        public static IReadOnlyList<CardDefinition> Build() => new List<CardDefinition>
        {
            Attack(), Attack(),
            SelectedBlock(), SelectedBlock(),
            AllBlock(),
            MoveForward()
        };

        public static CardDefinition Attack() => ExecutionCard(
            "fixture_attack",
            "[검증] 공격",
            AttackExecutionOrder,
            new EffectData(EffectKeys.Damage, AttackDamage));

        public static CardDefinition SelectedBlock() => ExecutionCard(
            "fixture_selected_block",
            "[검증] 선택 방어",
            StarterDeck.DefaultExecutionOrder,
            EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusLifetime.ThisTurn,
                StatusApplyTarget.Self,
                BlockMagnitude));

        public static CardDefinition AllBlock() => ExecutionCard(
            "fixture_all_block",
            "[검증] 전체 방어",
            StarterDeck.DefaultExecutionOrder,
            EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusLifetime.ThisTurn,
                StatusApplyTarget.AllPartyMembers,
                BlockMagnitude));

        public static CardDefinition MoveForward() => ExecutionCard(
            "fixture_move_forward",
            "[검증] 대형 이동",
            StarterDeck.DefaultExecutionOrder,
            new EffectData(EffectKeys.MoveFormation, MoveForwardDistance));

        private static CardDefinition ExecutionCard(
            string id,
            string name,
            int executionOrder,
            EffectData effect)
            => new CardDefinition(
                id,
                name,
                Side.Player,
                executionOrder,
                new[] { effect })
            {
                EnergyCost = DefaultEnergyCost,
                Category = CardCategory.Execution
            };
    }
}
