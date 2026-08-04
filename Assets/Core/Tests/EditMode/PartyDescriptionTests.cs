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
            KoreanDescriptionCatalog.CreateDefault();

        private static CardDefinition Execution(params EffectData[] effects) =>
            new CardDefinition("party_test", "파티 테스트", Side.Player, 0, effects)
            {
                Category = CardCategory.Execution
            };

        [TestCase(TargetSelector.FrontOne, "[◆] 피해 4.")]
        [TestCase(TargetSelector.FrontTwo, "[◆] 피해 4.")]
        [TestCase(TargetSelector.BackOne, "[◆] 피해 4.")]
        [TestCase(TargetSelector.BackTwo, "[◆] 피해 4.")]
        public void Position_selector_uses_target_symbol(TargetSelector selector, string expected)
        {
            var card = Execution(new EffectData(EffectKeys.Damage, 4) { TargetSelector = selector });

            Assert.AreEqual(expected, DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Party_member_status_fails_because_direct_selection_has_no_frame_schema()
        {
            var card = Execution(EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusApplyTarget.PartyMember,
                4));

            var ex = Assert.Throws<System.InvalidOperationException>(() =>
                DescriptionComposer.Describe(card, Korean));
            StringAssert.Contains("party_test", ex.Message);
        }

        [Test]
        public void All_party_status_uses_ally_symbol()
        {
            var card = Execution(EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusApplyTarget.AllPartyMembers,
                4));

            Assert.AreEqual("[◆] 방어 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Previous_executed_condition_names_execution_history()
        {
            var card = Execution(EffectData.Conditional(
                EffectKeys.Damage,
                1,
                new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage),
                2));

            Assert.AreEqual(
                "[◆] 피해 1. 직전에 실행한 카드가 적 피해 카드이면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Previous_adjacent_condition_names_frozen_placement_order()
        {
            var card = Execution(EffectData.Conditional(
                EffectKeys.Damage,
                1,
                new AdjacentCardHasEffect(AdjacentDirection.Previous, Side.Player, EffectKeys.Damage),
                2));

            Assert.AreEqual(
                "[◆] 피해 1. 앞에 배치된 카드가 플레이어 피해 카드이면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void No_preceding_condition_names_execution_history()
        {
            var card = Execution(EffectData.Conditional(
                EffectKeys.Damage,
                1,
                new NoPrecedingCardOfSide(Side.Player),
                2));

            Assert.AreEqual(
                "[◆] 피해 1. 이전에 실행한 플레이어 카드가 없으면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void No_following_condition_names_frozen_placement_order()
        {
            var card = Execution(EffectData.Conditional(
                EffectKeys.Damage,
                1,
                new NoFollowingCardOfSide(Side.Enemy),
                2));

            Assert.AreEqual(
                "[◆] 피해 1. 뒤에 배치된 적 카드가 없으면 피해 2.",
                DescriptionComposer.Describe(card, Korean));
        }
    }
}
