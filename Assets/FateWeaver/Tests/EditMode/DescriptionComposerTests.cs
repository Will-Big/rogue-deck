using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;
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
    }
}
