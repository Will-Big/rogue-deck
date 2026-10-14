using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    /// <summary>컴포저가 카드 데이터를 어떤 번역 요청으로 바꾸는지 잠근다. 최종 문장은 번역 템플릿
    /// (Localization/Csv) 몫이라 헤드리스에서 렌더할 수 없으므로, 요청 키·인자·줄 구조만 단언한다.
    /// 기대값은 <see cref="Render"/>가 편 "키(인자=값,…)" 꼴이다(인자는 이름순).</summary>
    public class DescriptionComposerTests
    {
        private static readonly DescriptionRequestCatalog Catalog =
            DescriptionRequestCatalog.CreateDefault(TestContent.Statuses());

        private const string EnemyFront = "Enemy/FrontOne";
        private const string AllySelf = "Ally/Self";
        private const string NoTarget = "-";

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

        private static string Damage(int amount) => "effect.damage(amount=" + amount + ",traits=text.empty)";

        private static string Block(int amount) => "effect.apply_status(amount=" + amount + ",status=status.block.name)";

        private static string Plain(string body) => "sentence(body=" + body + ")";

        private static string When(string condition, string body)
            => "sentence.conditional(body=" + body + ",condition=" + condition + ")";

        private static string Line(string target, params string[] sentences)
            => target + ": " + string.Join(" | ", sentences);

        /// <summary>카드 설명을 "대상: 문장 | 문장" 줄 목록으로 편다.</summary>
        private static string[] Render(CardDefinition card)
            => Render(DescriptionComposer.Compose(card, Catalog));

        private static string[] Render(LocalizedCardDescription doc)
            => doc.Lines.Select(line => Line(
                line.Target.HasValue ? line.Target.Value.Faction + "/" + line.Target.Value.Range : NoTarget,
                line.Sentences.Select(Render).ToArray())).ToArray();

        private static string Render(LocalizedTextRequest request)
            => request.Arguments.Count == 0
                ? request.Key
                : request.Key + "(" + string.Join(",", request.Arguments
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + (pair.Value is LocalizedTextRequest nested ? Render(nested) : pair.Value)))
                  + ")";

        [Test]
        public void Single_damage_effect_is_one_sentence()
        {
            var card = Execution("slash", Hit(4));
            CollectionAssert.AreEqual(new[] { Line(EnemyFront, Plain(Damage(4))) }, Render(card));
        }

        [Test]
        public void Target_range_becomes_the_line_target()
        {
            var card = Execution("aimed_slash", Hit(4)) with { EnemyTarget = CardTargetRange.BackOne };
            CollectionAssert.AreEqual(new[] { Line("Enemy/BackOne", Plain(Damage(4))) }, Render(card));
        }

        [Test]
        public void Conditional_effect_appends_condition_then_success_sentence()
        {
            var card = Execution("quick_cut",
                Hit(2) with { SuccessEffectValue = 8 })
                with { StartCondition = new FirstToTrigger() };
            CollectionAssert.AreEqual(
                new[] { Line(EnemyFront, Plain(Damage(2)), When("condition.first", Damage(8))) },
                Render(card));
        }

        [Test]
        public void Effects_with_different_targets_split_into_lines()
        {
            var card = Execution("wrist_cut",
                Hit(3),
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            CollectionAssert.AreEqual(
                new[] { Line(EnemyFront, Plain(Damage(3))), Line(NoTarget, Plain("effect.nullify_reward")) },
                Render(card));
        }

        [Test]
        public void Apply_status_uses_amount_as_magnitude()
        {
            var card = Execution("guard",
                EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4));
            CollectionAssert.AreEqual(new[] { Line(AllySelf, Plain(Block(4))) }, Render(card));
        }

        [Test]
        public void Conditional_status_reuses_success_amount_for_the_success_fragment()
        {
            var card = Execution("cover",
                EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 2)
                    with { SuccessEffectValue = 7 })
                with { StartCondition = new AdjacentCardHasEffect(AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage) };
            CollectionAssert.AreEqual(
                new[]
                {
                    Line(AllySelf, Plain(Block(2)),
                        When("condition.adjacent.next(kind=kind.damage_card,side=side.enemy)", Block(7)))
                },
                Render(card));
        }

        [Test]
        public void Grant_next_damage_card_bonus_carries_its_amount()
        {
            var card = Execution("mark", new EffectData(EffectKeys.GrantNextPlayerDamageCardBonus, 6));
            var doc = DescriptionComposer.Compose(card, Catalog);
            CollectionAssert.AreEqual(new[] { Line(NoTarget, Plain("effect.damage_bonus(amount=6)")) }, Render(doc));
            CollectionAssert.IsEmpty(doc.TargetEntries);
        }

        [Test]
        public void Intervention_card_renders_the_intervention_action_and_ignores_effects()
        {
            var card = new CardDefinition("pull_forward", "pull", Side.Player, 0,
                new[] { Hit(9) })
            {
                Category = CardCategory.Intervention,
                InterventionAction = new InterventionActionData(
                    InterventionActionKeys.ChangeExecutionOrder, 1,
                    new ChangeExecutionOrderPayload(Delta: -2, TargetSide: null))
            };
            var doc = DescriptionComposer.Compose(card, Catalog);
            CollectionAssert.AreEqual(
                new[] { Line(NoTarget, Plain("intervention.change_order(amount=-2)")) },
                Render(doc));
            CollectionAssert.IsEmpty(doc.TargetEntries);
        }

        [Test]
        public void Card_with_no_effects_or_intervention_has_no_lines()
        {
            var doc = DescriptionComposer.Compose(Execution("flavor_only"), Catalog);
            CollectionAssert.IsEmpty(doc.Lines);
            CollectionAssert.IsEmpty(doc.TargetEntries);
        }

        [Test]
        public void Null_card_or_catalog_fails_fast()
        {
            Assert.Throws<ArgumentNullException>(() => DescriptionComposer.Compose(null, Catalog));
            Assert.Throws<ArgumentNullException>(() => DescriptionComposer.Compose(Execution("x", Hit(1)), null));
        }

        [Test]
        public void Intervention_card_without_action_fails_fast()
        {
            var card = new CardDefinition("no_action", "no_action", Side.Player, 0, new EffectData[0])
            {
                Category = CardCategory.Intervention
            };

            Assert.Throws<ArgumentException>(() => DescriptionComposer.Compose(card, Catalog));
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
                DescriptionComposer.Compose(card, Catalog));
        }

        [Test]
        public void Execution_card_with_null_effect_entry_fails_fast()
        {
            var card = Execution("null_entry", Hit(1), null);

            Assert.Throws<ArgumentException>(() =>
                DescriptionComposer.Compose(card, Catalog));
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
                DescriptionComposer.Compose(card, Catalog));
        }

        [Test]
        public void Card_with_undefined_category_fails_fast()
        {
            var card = Execution("undefined_category") with
            {
                Category = (CardCategory)99
            };

            Assert.Throws<ArgumentException>(() =>
                DescriptionComposer.Compose(card, Catalog));
        }

        [TestCase(-2, "effect.move.forward(amount=2)")]
        [TestCase(2, "effect.move.backward(amount=2)")]
        [TestCase(0, "effect.move.stay")]
        public void Formation_movement_uses_signed_direction(
            int distance,
            string expected)
        {
            var card = Execution(
                "move",
                new EffectData(EffectKeys.MoveFormation, distance) { TargetFaction = CardTargetFaction.Ally });

            CollectionAssert.AreEqual(new[] { Line(AllySelf, Plain(expected)) }, Render(card));
        }

        [Test]
        public void Unknown_effect_fails_instead_of_rendering_an_empty_sentence()
        {
            var card = Execution(
                "unknown",
                new EffectData(new EffectKey("unknown_effect"), 1));

            Assert.Throws<KeyNotFoundException>(() =>
                DescriptionComposer.Compose(card, Catalog));
        }

        [Test]
        public void Unknown_intervention_action_fails_fast()
        {
            var card = new CardDefinition("unknown_action", "unknown_action", Side.Player, 0, new EffectData[0])
            {
                Category = CardCategory.Intervention,
                InterventionAction = new InterventionActionData(new InterventionActionKey("unknown_action"), 1, null)
            };

            Assert.Throws<KeyNotFoundException>(() =>
                DescriptionComposer.Compose(card, Catalog));
        }

        [Test]
        public void Empty_handler_request_fails_fast()
        {
            var catalog = new DescriptionRequestCatalog(TestContent.Statuses());
            catalog.Effects.Register(EffectKeys.Damage, (effect, value, context) => null);

            Assert.Throws<InvalidOperationException>(() =>
                DescriptionComposer.Compose(
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
                DescriptionComposer.Compose(card, Catalog));
        }

        [Test]
        public void Fixture_slash() =>
            CollectionAssert.AreEqual(new[] { Line(EnemyFront, Plain(Damage(4))) },
                Render(CardFixtures.Damage("slash_fx", damage: 4)));

        [Test]
        public void Fixture_guard() =>
            CollectionAssert.AreEqual(new[] { Line(AllySelf, Plain(Block(4))) },
                Render(CardFixtures.Block("guard_fx", magnitude: 4)));

        [Test]
        public void Fixture_quick_cut() =>
            CollectionAssert.AreEqual(
                new[] { Line(EnemyFront, Plain(Damage(2)), When("condition.first", Damage(8))) },
                Render(CardFixtures.DamageOnFirstTrigger("quick_fx", baseDamage: 2, whenFirst: 8)));

        [Test]
        public void Fixture_counter_stance() =>
            CollectionAssert.AreEqual(
                new[]
                {
                    Line(EnemyFront, Plain(Damage(4)),
                        When("condition.previous(kind=kind.damage_card,side=side.enemy)", Damage(9)))
                },
                Render(CardFixtures.DamageAfterEnemyDamage("counter_fx", baseDamage: 4, whenAfter: 9)));

        [Test]
        public void Fixture_cover() =>
            CollectionAssert.AreEqual(
                new[]
                {
                    Line(AllySelf, Plain(Block(2)),
                        When("condition.adjacent.next(kind=kind.damage_card,side=side.enemy)", Block(7)))
                },
                Render(CardFixtures.BlockBeforeEnemyDamage("cover_fx", baseMagnitude: 2, whenBefore: 7)));

        [TestCase(-1)]
        [TestCase(1)]
        public void Fixture_change_execution_order_keeps_signed_delta(int delta) =>
            CollectionAssert.AreEqual(
                new[] { Line(NoTarget, Plain("intervention.change_order(amount=" + delta + ")")) },
                Render(CardFixtures.ChangeExecutionOrder("order_fx", delta: delta)));

        [Test]
        public void Fixture_swap_positions() =>
            CollectionAssert.AreEqual(
                new[] { Line(NoTarget, Plain("intervention.swap_order")) },
                Render(CardFixtures.SwapExecutionOrder("swap_fx")));

        [Test]
        public void Content_goblin_jab() =>
            CollectionAssert.AreEqual(new[] { Line("Ally/FrontOne", Plain(Damage(5))) },
                Render(TestContent.Cards().Get("goblin_jab")));

        [Test]
        public void Content_crude_guard() =>
            CollectionAssert.AreEqual(
                new[] { Line("Enemy/Self", Plain(Block(3))) },
                Render(TestContent.Cards().Get("crude_guard")));

        [Test]
        public void Content_sly_jab() =>
            CollectionAssert.AreEqual(
                new[] { Line("Ally/FrontOne", Plain(Damage(3)), When("condition.no_preceding(side=side.player)", Damage(6))) },
                Render(TestContent.Cards().Get("sly_jab")));

        [Test]
        public void No_following_enemy_card_condition() =>
            CollectionAssert.AreEqual(
                new[] { Line(EnemyFront, Plain(Damage(2)), When("condition.no_following(side=side.enemy)", Damage(7))) },
                Render(
                    Execution("warden_smash",
                        Hit(2) with { SuccessEffectValue = 7 })
                        with { StartCondition = new NoFollowingCardOfSide(Side.Enemy) }));

        [Test]
        public void Number_argument_follows_data()
        {
            var tuned = new CardDefinition("slash", "베기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 99) { TargetFaction = CardTargetFaction.Enemy } }) { EnemyTarget = CardTargetRange.FrontOne, Category = CardCategory.Execution };
            CollectionAssert.AreEqual(new[] { Line(EnemyFront, Plain(Damage(99))) }, Render(tuned));
        }

        [Test]
        public void Duration_status_selects_the_lifetime_template()
        {
            // slow is Turns-kind in the catalog, so the card gives only a duration (2 turns) —
            // its executionOrder strength is the status's own, not a card-authored number, so card text
            // does not restate it (규칙 10; 취약의 배율이 카드 텍스트에 없는 것과 같다).
            var card = new CardDefinition("slow_hex", "둔화 저주", Side.Player, 5,
                new[]
                {
                    EffectData.ApplyStatus(StatusKeys.Slow, CardTargetFaction.Enemy, count: 2)
                }) { EnemyTarget = CardTargetRange.FrontOne, Category = CardCategory.Execution };
            CollectionAssert.AreEqual(
                new[] { Line(EnemyFront, Plain("effect.apply_status.Turns(amount=2,status=status.slow.name)")) },
                Render(card));
        }

        [Test]
        public void All_of_condition_nests_its_children()
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
            CollectionAssert.AreEqual(
                new[]
                {
                    Line(EnemyFront, Plain(Damage(1)), When(
                        "condition.all(first=condition.previous(kind=kind.card,side=side.player),next=condition.within(n=3))",
                        Damage(6)))
                },
                Render(card));
        }
    }
}
