using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    public class DescriptionComposerTests
    {
        // Fake vocab: marker strings so we assert STRUCTURE, not Korean wording.
        private sealed class FakeVocabulary : IDescriptionVocabulary
        {
            public string Damage(int amount) => "DMG" + amount;
            public string Status(StatusKey key, StatusApplyTarget target, int magnitude, StatusLifetime lifetime)
                => "STATUS:" + key.Id + ":" + target + ":" + magnitude + ":" + lifetime.Kind;
            public string NullifyNextReward() => "NULLIFY";
            public string GrantNextAttackBonus(int amount) => "GRANT" + amount;
            public string Condition(Condition condition) => "COND[" + condition.GetType().Name + "]";
            public string Fate(FateActionData fate) => "FATE:" + fate.Key.Id + ":" + fate.Amount;
        }

        private static readonly IDescriptionVocabulary Vocab = new FakeVocabulary();

        private static CardDefinition Action(string id, params EffectData[] effects)
            => new CardDefinition(id, id, Side.Player, CardType.Attack, 5, effects)
               { Category = CardCategory.Action };

        [Test]
        public void Single_damage_effect_is_one_sentence()
        {
            var card = Action("slash", new EffectData(EffectKeys.Damage, 4));
            Assert.AreEqual("DMG4.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Conditional_effect_appends_condition_then_success_sentence()
        {
            var card = Action("quick_cut",
                EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8));
            Assert.AreEqual("DMG2. COND[FirstToTrigger] DMG8.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Multiple_effects_join_with_a_space()
        {
            var card = Action("wrist_cut",
                new EffectData(EffectKeys.Damage, 3),
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            Assert.AreEqual("DMG3. NULLIFY.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Apply_status_uses_amount_as_magnitude()
        {
            var card = Action("guard",
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 4));
            Assert.AreEqual("STATUS:block:Self:4:ThisTurn.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Conditional_status_reuses_success_amount_for_the_success_fragment()
        {
            var card = Action("cover",
                new EffectData(EffectKeys.ApplyStatus, 2)
                {
                    StatusKey = StatusKeys.Block,
                    StatusLifetime = StatusLifetime.ThisTurn,
                    StatusTarget = StatusApplyTarget.Self,
                    Condition = new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack),
                    SuccessAmount = 7
                });
            Assert.AreEqual(
                "STATUS:block:Self:2:ThisTurn. COND[AdjacentCardIs] STATUS:block:Self:7:ThisTurn.",
                DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Grant_next_attack_bonus_renders_its_amount()
        {
            var card = Action("mark", new EffectData(EffectKeys.GrantNextPlayerAttackDamageBonus, 6));
            Assert.AreEqual("GRANT6.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Fate_card_renders_the_fate_action_and_ignores_effects()
        {
            var card = new CardDefinition("pull_forward", "pull", Side.Player, CardType.Skill, 0,
                new EffectData[0])
            {
                Category = CardCategory.Fate,
                FateAction = new FateActionData(FateActionKeys.ChangeInitiative, 1, -2)
            };
            Assert.AreEqual("FATE:change_initiative:-2.", DescriptionComposer.Describe(card, Vocab));
        }

        [Test]
        public void Card_with_no_effects_or_fate_renders_empty()
        {
            var card = Action("flavor_only");
            Assert.AreEqual(string.Empty, DescriptionComposer.Describe(card, Vocab));
        }

        // --- Korean vocabulary integration (real output) ---------------------

        private static readonly IDescriptionVocabulary Kr = KoreanDescriptionVocabulary.Instance;

        [Test]
        public void Korean_slash() =>
            Assert.AreEqual("피해 4.",
                DescriptionComposer.Describe(StarterDeck.Slash(), Kr));

        [Test]
        public void Korean_guard() =>
            Assert.AreEqual("방어 4.",
                DescriptionComposer.Describe(StarterDeck.Guard(), Kr));

        [Test]
        public void Korean_quick_cut() =>
            Assert.AreEqual("피해 2. 첫 발동이면 피해 8.",
                DescriptionComposer.Describe(StarterDeck.QuickCut(), Kr));

        [Test]
        public void Korean_counter_stance() =>
            Assert.AreEqual("피해 4. 바로 앞이 적 공격이면 피해 9.",
                DescriptionComposer.Describe(StarterDeck.Counter(), Kr));

        [Test]
        public void Korean_cover() =>
            Assert.AreEqual("방어 2. 바로 뒤가 적 공격이면 방어 7.",
                DescriptionComposer.Describe(StarterDeck.Cover(), Kr));

        [Test]
        public void Korean_pull_forward() =>
            Assert.AreEqual("한 카드의 주도력 -2.",
                DescriptionComposer.Describe(StarterDeck.PullForward(), Kr));

        [Test]
        public void Korean_swap_positions() =>
            Assert.AreEqual("두 카드의 주도력을 교환.",
                DescriptionComposer.Describe(StarterDeck.SwapPositions(), Kr));

        [Test]
        public void Korean_goblin_jab() =>
            Assert.AreEqual("피해 4.",
                DescriptionComposer.Describe(GoblinDeck.Thrust(), Kr));

        [Test]
        public void Korean_crude_guard() =>
            Assert.AreEqual("방어 3.",
                DescriptionComposer.Describe(GoblinDeck.CrudeGuard(), Kr));

        [Test]
        public void Korean_sly_jab() =>
            Assert.AreEqual("피해 3. 앞에 플레이어 카드가 없으면 피해 6.",
                DescriptionComposer.Describe(GoblinDeck.SlyJab(), Kr));

        [Test]
        public void Korean_number_token_follows_data()
        {
            var tuned = new CardDefinition("slash", "베기", Side.Player, CardType.Attack, 4,
                new[] { new EffectData(EffectKeys.Damage, 99) }) { Category = CardCategory.Action };
            Assert.AreEqual("피해 99.", DescriptionComposer.Describe(tuned, Kr));
        }

        [Test]
        public void Korean_slow_status_shows_turn_suffix()
        {
            var card = new CardDefinition("slow_hex", "둔화 저주", Side.Player, CardType.Skill, 5,
                new[]
                {
                    EffectData.ApplyStatus(StatusKeys.Slow, StatusLifetime.Turns(2),
                        StatusApplyTarget.TargetEnemy, 3)
                }) { Category = CardCategory.Action };
            Assert.AreEqual("적 둔화 3 (2턴).", DescriptionComposer.Describe(card, Kr));
        }

        [Test]
        public void Korean_allof_condition_joins_naturally()
        {
            // A single conditional effect (base 1, 6 on success when prev is a player card AND within the 3rd slot).
            var card = new CardDefinition("chain", "연쇄 베기", Side.Player, CardType.Attack, 5,
                new[]
                {
                    EffectData.Conditional(
                        EffectKeys.Damage, 1,
                        new AllOf(new Condition[]
                        {
                            new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, null),
                            new WithinNth(3)
                        }),
                        6)
                }) { Category = CardCategory.Action };
            Assert.AreEqual("피해 1. 바로 앞이 플레이어 카드이고 3번째 안이면 피해 6.",
                DescriptionComposer.Describe(card, Kr));
        }
    }
}
