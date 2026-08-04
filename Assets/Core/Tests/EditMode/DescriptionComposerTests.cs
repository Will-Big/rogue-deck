using System;
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    public class DescriptionComposerTests
    {
        private sealed class EmptyEffectDescriptionHandler : IEffectDescriptionHandler
        {
            public EffectKey Key => EffectKeys.Damage;

            public EffectDescriptionFragment Describe(EffectData effect, int value, DescriptionContext context)
                => new EffectDescriptionFragment(null, string.Empty);
        }

        private static readonly KoreanDescriptionCatalog Korean =
            KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());

        private static CardDefinition Execution(string id, params EffectData[] effects)
            => new CardDefinition(id, id, Side.Player, 5, effects)
               { Category = CardCategory.Execution };

        [Test]
        public void Single_damage_effect_is_one_sentence()
        {
            var card = Execution("slash", new EffectData(EffectKeys.Damage, 4));
            Assert.AreEqual("[◆] 피해 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Target_selector_prefixes_the_effect_fragment_through_the_vocabulary()
        {
            var card = Execution("aimed_slash",
                new EffectData(EffectKeys.Damage, 4) { TargetSelector = TargetSelector.BackOne });
            Assert.AreEqual("[◆] 피해 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Conditional_effect_appends_condition_then_success_sentence()
        {
            var card = Execution("quick_cut",
                EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8));
            Assert.AreEqual("[◆] 피해 2. 첫 발동이면 피해 8.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Multiple_effects_join_with_a_space()
        {
            var card = Execution("wrist_cut",
                new EffectData(EffectKeys.Damage, 3),
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            Assert.AreEqual("[◆] 피해 3.\n다음 플레이어 조건 보상을 무효화.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Apply_status_uses_amount_as_magnitude()
        {
            var card = Execution("guard",
                EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 4));
            Assert.AreEqual("[◆] 방어 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Conditional_status_reuses_success_amount_for_the_success_fragment()
        {
            var card = Execution("cover",
                EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 2)
                    with
                    {
                        Condition = new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
                        SuccessEffectValue = 7
                    });
            Assert.AreEqual(
                "[◆] 방어 2. 바로 뒤가 적 피해 카드이면 방어 7.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Grant_next_damage_card_bonus_renders_its_amount()
        {
            var card = Execution("mark", new EffectData(EffectKeys.GrantNextPlayerDamageCardBonus, 6));
            Assert.AreEqual("다음 플레이어 피해 카드가 주는 피해 +6.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Intervention_card_renders_the_intervention_action_and_ignores_effects()
        {
            var card = new CardDefinition("pull_forward", "pull", Side.Player, 0,
                new EffectData[0])
            {
                Category = CardCategory.Intervention,
                InterventionAction = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, 1, -2)
            };
            Assert.AreEqual("한 카드의 실행 순서 -2.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Card_with_no_effects_or_intervention_renders_empty()
        {
            var card = Execution("flavor_only");
            Assert.AreEqual(string.Empty, DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Execution_card_with_null_effects_fails_fast()
        {
            var card = new CardDefinition(
                "null_effects",
                "null_effects",
                Side.Player,
                5,
                null)
            {
                Category = CardCategory.Execution
            };

            Assert.Throws<ArgumentException>(() =>
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Execution_card_with_intervention_action_fails_fast()
        {
            var card = Execution("execution_with_intervention") with
            {
                InterventionAction = new InterventionActionData(
                    InterventionActionKeys.ChangeExecutionOrder,
                    1,
                    -2)
            };

            Assert.Throws<ArgumentException>(() =>
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Card_with_undefined_category_fails_fast()
        {
            var card = Execution("undefined_category") with
            {
                Category = (CardCategory)99
            };

            Assert.Throws<ArgumentException>(() =>
                DescriptionComposer.Describe(card, Korean));
        }

        [TestCase(-2, "[◆] 대형 전방으로 2칸 이동.")]
        [TestCase(2, "[◆] 대형 후방으로 2칸 이동.")]
        [TestCase(0, "[◆] 대형 위치 유지.")]
        public void Korean_formation_movement_uses_signed_direction(
            int distance,
            string expected)
        {
            var card = Execution(
                "move",
                new EffectData(EffectKeys.MoveFormation, distance));

            Assert.AreEqual(expected, DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Unknown_effect_fails_instead_of_rendering_an_empty_sentence()
        {
            var card = Execution(
                "unknown",
                new EffectData(new EffectKey("unknown_effect"), 1));

            Assert.Throws<KeyNotFoundException>(() =>
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Empty_handler_fragment_fails_fast()
        {
            var effects = new EffectDescriptionRegistry();
            effects.Register(new EmptyEffectDescriptionHandler());
            var catalog = new KoreanDescriptionCatalog(
                effects,
                new InterventionDescriptionRegistry(),
                new StatusDescriptionRegistry(),
                new KoreanDescriptionGrammar(),
                TestContent.Statuses());

            Assert.Throws<InvalidOperationException>(() =>
                DescriptionComposer.Describe(
                    Execution("empty", new EffectData(EffectKeys.Damage, 1)),
                    catalog));
        }

        [Test]
        public void Apply_status_requires_its_status_key_and_lifetime()
        {
            var card = Execution(
                "invalid_status",
                new EffectData(EffectKeys.ApplyStatus, 1));

            Assert.Throws<ArgumentException>(() =>
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Korean_slash() =>
            Assert.AreEqual("[◆] 피해 4.",
                DescriptionComposer.Describe(StarterDeck.Slash(), Korean));

        [Test]
        public void Korean_guard() =>
            Assert.AreEqual("[◆] 방어 4.",
                DescriptionComposer.Describe(StarterDeck.Guard(), Korean));

        [Test]
        public void Korean_quick_cut() =>
            Assert.AreEqual("[◆] 피해 2. 첫 발동이면 피해 8.",
                DescriptionComposer.Describe(StarterDeck.QuickCut(), Korean));

        [Test]
        public void Korean_counter_stance() =>
            Assert.AreEqual("[◆] 피해 4. 직전에 실행한 카드가 적 피해 카드이면 피해 9.",
                DescriptionComposer.Describe(StarterDeck.Counter(), Korean));

        [Test]
        public void Korean_cover() =>
            Assert.AreEqual("[◆] 방어 2. 바로 뒤가 적 피해 카드이면 방어 7.",
                DescriptionComposer.Describe(StarterDeck.Cover(), Korean));

        [Test]
        public void Korean_pull_forward() =>
            Assert.AreEqual("한 카드의 실행 순서 -1.",
                DescriptionComposer.Describe(StarterDeck.PullForward(), Korean));

        [Test]
        public void Korean_push_back() =>
            Assert.AreEqual("한 카드의 실행 순서 +1.",
                DescriptionComposer.Describe(StarterDeck.PushBack(), Korean));

        [Test]
        public void Korean_swap_positions() =>
            Assert.AreEqual("두 카드의 실행 순서를 교환.",
                DescriptionComposer.Describe(StarterDeck.SwapPositions(), Korean));

        [Test]
        public void Korean_goblin_jab() =>
            Assert.AreEqual("[◆] 피해 4.",
                DescriptionComposer.Describe(GoblinDeck.Thrust(), Korean));

        [Test]
        public void Korean_crude_guard() =>
            Assert.AreEqual("[◆] 방어 3.",
                DescriptionComposer.Describe(GoblinDeck.CrudeGuard(), Korean));

        [Test]
        public void Korean_sly_jab() =>
            Assert.AreEqual("[◆] 피해 3. 이전에 실행한 플레이어 카드가 없으면 피해 6.",
                DescriptionComposer.Describe(GoblinDeck.SlyJab(), Korean));

        [Test]
        public void Korean_no_following_enemy_card_condition() =>
            Assert.AreEqual("[◆] 피해 2. 뒤에 배치된 적 카드가 없으면 피해 7.",
                DescriptionComposer.Describe(
                    Execution("warden_smash",
                        EffectData.Conditional(
                            EffectKeys.Damage,
                            2,
                            new NoFollowingCardOfSide(Side.Enemy),
                            7)),
                    Korean));

        [Test]
        public void Korean_number_token_follows_data()
        {
            var tuned = new CardDefinition("slash", "베기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 99) }) { Category = CardCategory.Execution };
            Assert.AreEqual("[◆] 피해 99.", DescriptionComposer.Describe(tuned, Korean));
        }

        [Test]
        public void Korean_slow_status_shows_turn_suffix()
        {
            // Task 4: slow is Turns-kind in the catalog, so the card gives only a duration (2 turns) —
            // its executionOrder strength is the status's own, not a card-authored number, so card text
            // no longer restates it (규칙 10; 취약의 배율이 카드 텍스트에 없는 것과 같다).
            var card = new CardDefinition("slow_hex", "둔화 저주", Side.Player, 5,
                new[]
                {
                    EffectData.ApplyStatus(StatusKeys.Slow, StatusApplyTarget.TargetEnemy, count: 2)
                }) { Category = CardCategory.Execution };
            Assert.AreEqual("[◆] 둔화 (2턴).", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Korean_allof_condition_joins_naturally()
        {
            // A single conditional effect (base 1, 6 on success when prev is a player card AND within the 3rd slot).
            var card = new CardDefinition("chain", "연쇄 베기", Side.Player, 5,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage, 1,
                        new AllOf(new Condition[]
                        {
                            new PreviousExecutedCardIs(Side.Player),
                            new WithinNth(3)
                        }),
                        6)
                }) { Category = CardCategory.Execution };
            Assert.AreEqual("[◆] 피해 1. 직전에 실행한 카드가 플레이어 카드이고 3번째 안이면 피해 6.",
                DescriptionComposer.Describe(card, Korean));
        }
    }
}
