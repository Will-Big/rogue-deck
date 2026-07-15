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
        private static readonly IDescriptionVocabulary Kr = KoreanDescriptionVocabulary.Instance;

        private static CardDefinition Execution(params EffectData[] effects) =>
            new CardDefinition("party_test", "파티 테스트", Side.Player, CardType.Skill, 0, effects)
            {
                Category = CardCategory.Execution
            };

        [TestCase(TargetSelector.FrontMost, "가장 앞의 대상에게 피해 4.")]
        [TestCase(TargetSelector.SecondFromFront, "전열에서 두 번째 대상에게 피해 4.")]
        [TestCase(TargetSelector.BackMost, "가장 뒤의 대상에게 피해 4.")]
        [TestCase(TargetSelector.Random, "무작위 대상에게 피해 4.")]
        public void Position_selector_uses_exact_target_phrase(TargetSelector selector, string expected)
        {
            var card = Execution(new EffectData(EffectKeys.Damage, 4) { TargetSelector = selector });

            Assert.AreEqual(expected, DescriptionComposer.Describe(card, Kr));
        }

        [TestCase(StatusApplyTarget.PartyMember, "선택한 아군에게 방어 4.")]
        [TestCase(StatusApplyTarget.AllPartyMembers, "모든 아군에게 방어 4.")]
        public void Ally_status_target_distinguishes_direct_and_all_targets(
            StatusApplyTarget target,
            string expected)
        {
            var card = Execution(EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusLifetime.ThisTurn,
                target,
                4));

            Assert.AreEqual(expected, DescriptionComposer.Describe(card, Kr));
        }

        [Test]
        public void Previous_executed_condition_names_execution_history()
        {
            var card = Execution(EffectData.Conditional(
                EffectKeys.Damage,
                1,
                new PreviousExecutedCardIs(Side.Enemy, CardType.Attack),
                2));

            Assert.AreEqual(
                "피해 1. 직전에 실행한 카드가 적 공격이면 피해 2.",
                DescriptionComposer.Describe(card, Kr));
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
                "피해 1. 이전에 실행한 플레이어 카드가 없으면 피해 2.",
                DescriptionComposer.Describe(card, Kr));
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
                "피해 1. 뒤에 배치된 적 카드가 없으면 피해 2.",
                DescriptionComposer.Describe(card, Kr));
        }
    }
}
