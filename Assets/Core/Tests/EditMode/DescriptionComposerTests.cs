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

        /// <summary>플레이어 실행 카드. 아군 효과는 자신, 적 효과는 적 전열 하나를 고른다.</summary>
        private static CardDefinition Execution(string id, params EffectData[] effects)
            => new CardDefinition(id, id, Side.Player, 5, effects)
               {
                   AllyTarget = CardTargetRange.Self,
                   EnemyTarget = CardTargetRange.FrontOne,
                   Category = CardCategory.Execution
               };

        private static EffectData Hit(int value)
            => new EffectData(EffectKeys.Damage, value) { TargetFaction = CardTargetFaction.Enemy };

        [Test]
        public void Single_damage_effect_is_one_sentence()
        {
            var card = Execution("slash", Hit(4));
            Assert.AreEqual("[◆] 피해 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Target_range_prefixes_the_effect_fragment_through_the_vocabulary()
        {
            var card = Execution("aimed_slash", Hit(4)) with { EnemyTarget = CardTargetRange.BackOne };
            Assert.AreEqual("[◆] 피해 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Conditional_effect_appends_condition_then_success_sentence()
        {
            var card = Execution("quick_cut",
                Hit(2) with { SuccessEffectValue = 8 })
                with { StartCondition = new FirstToTrigger() };
            Assert.AreEqual("[◆] 피해 2. 첫 발동이면 피해 8.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Multiple_effects_join_with_a_space()
        {
            var card = Execution("wrist_cut",
                Hit(3),
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            Assert.AreEqual("[◆] 피해 3.\n다음 플레이어 조건 보상을 무효화.",
                DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Apply_status_uses_amount_as_magnitude()
        {
            var card = Execution("guard",
                EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4));
            Assert.AreEqual("[◆] 방어 4.", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Conditional_status_reuses_success_amount_for_the_success_fragment()
        {
            var card = Execution("cover",
                EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 2)
                    with { SuccessEffectValue = 7 })
                with { StartCondition = new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage) };
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
                InterventionAction = new InterventionActionData(
                    InterventionActionKeys.ChangeExecutionOrder, 1,
                    new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null))
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
                    new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null))
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
                new EffectData(EffectKeys.MoveFormation, distance) { TargetFaction = CardTargetFaction.Ally });

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
                    Execution("empty", Hit(1)),
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
                DescriptionComposer.Describe(CardFixtures.Damage("slash_fx", damage: 4), Korean));

        [Test]
        public void Korean_guard() =>
            Assert.AreEqual("[◆] 방어 4.",
                DescriptionComposer.Describe(CardFixtures.Block("guard_fx", magnitude: 4), Korean));

        [Test]
        public void Korean_quick_cut() =>
            Assert.AreEqual("[◆] 피해 2. 첫 발동이면 피해 8.",
                DescriptionComposer.Describe(
                    CardFixtures.DamageOnFirstTrigger("quick_fx", baseDamage: 2, whenFirst: 8), Korean));

        [Test]
        public void Korean_counter_stance() =>
            Assert.AreEqual("[◆] 피해 4. 직전에 실행된 카드가 적 피해 카드이면 피해 9.",
                DescriptionComposer.Describe(
                    CardFixtures.DamageAfterEnemyDamage("counter_fx", baseDamage: 4, whenAfter: 9), Korean));

        [Test]
        public void Korean_cover() =>
            Assert.AreEqual("[◆] 방어 2. 바로 뒤가 적 피해 카드이면 방어 7.",
                DescriptionComposer.Describe(
                    CardFixtures.BlockBeforeEnemyDamage("cover_fx", baseMagnitude: 2, whenBefore: 7), Korean));

        [Test]
        public void Korean_pull_forward() =>
            Assert.AreEqual("한 카드의 실행 순서 -1.",
                DescriptionComposer.Describe(
                    CardFixtures.ChangeExecutionOrder("pull_fx", delta: -1), Korean));

        [Test]
        public void Korean_push_back() =>
            Assert.AreEqual("한 카드의 실행 순서 +1.",
                DescriptionComposer.Describe(
                    CardFixtures.ChangeExecutionOrder("push_fx", delta: 1), Korean));

        [Test]
        public void Korean_swap_positions() =>
            Assert.AreEqual("두 카드의 실행 순서를 교환.",
                DescriptionComposer.Describe(CardFixtures.SwapExecutionOrder("swap_fx"), Korean));

        [Test]
        public void Korean_goblin_jab() =>
            Assert.AreEqual("[◆] 피해 5.",
                DescriptionComposer.Describe(TestContent.Cards().Get("goblin_jab"), Korean));

        [Test]
        public void Korean_crude_guard() =>
            Assert.AreEqual("[◆] 방어 3.",
                DescriptionComposer.Describe(TestContent.Cards().Get("crude_guard"), Korean));

        [Test]
        public void Korean_sly_jab() =>
            Assert.AreEqual("[◆] 피해 3. 앞에 배치된 플레이어 카드가 없으면 피해 6.",
                DescriptionComposer.Describe(TestContent.Cards().Get("sly_jab"), Korean));

        [Test]
        public void Korean_no_following_enemy_card_condition() =>
            Assert.AreEqual("[◆] 피해 2. 뒤에 배치된 적 카드가 없으면 피해 7.",
                DescriptionComposer.Describe(
                    Execution("warden_smash",
                        Hit(2) with { SuccessEffectValue = 7 })
                        with { StartCondition = new NoFollowingCardOfSide(Side.Enemy) },
                    Korean));

        [Test]
        public void Korean_number_token_follows_data()
        {
            var tuned = new CardDefinition("slash", "베기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 99) { TargetFaction = CardTargetFaction.Enemy } }) { EnemyTarget = CardTargetRange.FrontOne, Category = CardCategory.Execution };
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
                    EffectData.ApplyStatus(StatusKeys.Slow, CardTargetFaction.Enemy, count: 2)
                }) { EnemyTarget = CardTargetRange.FrontOne, Category = CardCategory.Execution };
            Assert.AreEqual("[◆] 둔화 (2턴).", DescriptionComposer.Describe(card, Korean));
        }

        [Test]
        public void Korean_allof_condition_joins_naturally()
        {
            // A single conditional effect (base 1, 6 on success when prev is a player card AND within the 3rd slot).
            var card = new CardDefinition("chain", "연쇄 베기", Side.Player, 5,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 1) { TargetFaction = CardTargetFaction.Enemy, SuccessEffectValue = 6 }
                })
            {
                EnemyTarget = CardTargetRange.FrontOne,
                Category = CardCategory.Execution,
                StartCondition = new AllOf(new Condition[]
                {
                    new PreviousExecutedCardIs(Side.Player),
                    new WithinNth(3)
                })
            };
            Assert.AreEqual("[◆] 피해 1. 직전에 실행된 카드가 플레이어 카드이고 3번째 안이면 피해 6.",
                DescriptionComposer.Describe(card, Korean));
        }
    }
}
