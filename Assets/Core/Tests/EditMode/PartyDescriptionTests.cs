using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests
{
    public class PartyDescriptionTests
    {
        private static readonly KoreanDescriptionCatalog Korean =
            KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());

        private static CardDefinition Execution(params EffectData[] effects) =>
            new CardDefinition("party_test", "파티 테스트", Side.Player, 0, effects)
            {
                EnemyTarget = CardTargetRange.FrontOne,
                Category = CardCategory.Execution
            };

        private static EffectData Hit(int value)
            => new EffectData(EffectKeys.Damage, value) { TargetFaction = CardTargetFaction.Enemy };

        [TestCase(CardTargetRange.FrontOne, "[◆] 피해 4.")]
        [TestCase(CardTargetRange.FrontTwo, "[◆] 피해 4.")]
        [TestCase(CardTargetRange.BackOne, "[◆] 피해 4.")]
        [TestCase(CardTargetRange.BackTwo, "[◆] 피해 4.")]
        public void Position_range_uses_target_symbol(CardTargetRange range, string expected)
        {
            var card = Execution(Hit(4)) with { EnemyTarget = range };

            Assert.AreEqual(expected, DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void All_party_status_uses_ally_symbol()
        {
            var card = Execution(EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4))
                with { AllyTarget = CardTargetRange.All };

            Assert.AreEqual("[◆] 방어 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Previous_executed_condition_names_execution_history()
        {
            var card = Execution(Hit(1) with { SuccessEffectValue = 2 })
                with { StartCondition = new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage) };

            Assert.AreEqual(
                "[◆] 피해 1. 직전에 실행된 카드가 적 피해 카드이면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Previous_adjacent_condition_names_frozen_placement_order()
        {
            var card = Execution(Hit(1) with { SuccessEffectValue = 2 })
                with { StartCondition = new AdjacentCardHasEffect(AdjacentDirection.Previous, Side.Player, EffectKeys.Damage) };

            Assert.AreEqual(
                "[◆] 피해 1. 앞에 배치된 카드가 플레이어 피해 카드이면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void No_preceding_condition_names_placement_on_the_line()
        {
            var card = Execution(Hit(1) with { SuccessEffectValue = 2 })
                with { StartCondition = new NoPrecedingCardOfSide(Side.Player) };

            Assert.AreEqual(
                "[◆] 피해 1. 앞에 배치된 플레이어 카드가 없으면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void No_following_condition_names_frozen_placement_order()
        {
            var card = Execution(Hit(1) with { SuccessEffectValue = 2 })
                with { StartCondition = new NoFollowingCardOfSide(Side.Enemy) };

            Assert.AreEqual(
                "[◆] 피해 1. 뒤에 배치된 적 카드가 없으면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }
    }
}
