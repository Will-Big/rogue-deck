using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    public class StructuredCardDescriptionTests
    {
        private static readonly KoreanDescriptionCatalog Korean =
            KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());

        [Test]
        public void Toxic_reclaim_separates_enemy_and_ally_self_lines()
        {
            var definition = CardSpecMapper.ToDefinition(StarterPoolSpecs.ToxicReclaim());

            var layout = DescriptionComposer.Compose(definition, Korean);

            CollectionAssert.AreEqual(
                new[]
                {
                    new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                    new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)
                },
                layout.TargetEntries);
            Assert.AreEqual("독 최대 1 소비. 독 1.", layout.Lines[0].Text);
            Assert.AreEqual("소비했다면 방어 4.", layout.Lines[1].Text);
            Assert.AreEqual(
                "[◆] 독 최대 1 소비. 독 1.\n[◆] 소비했다면 방어 4.",
                layout.PlainText);
        }

        [Test]
        public void Repeated_nonconsecutive_target_joins_the_first_matching_line()
        {
            var layout = DescriptionComposer.Compose(
                Execution("repeat", DamageEnemy(3), BlockSelf(2), DamageEnemy(3)), Korean);

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual(2, layout.TargetEntries.Count);
            Assert.AreEqual(
                new CardTargetKey(
                    CardTargetFaction.Enemy,
                    CardTargetRange.FrontOne),
                layout.Lines[0].Target.Value);
            Assert.AreEqual("피해 3. 피해 3.", layout.Lines[0].Text);
            Assert.AreEqual(
                new CardTargetKey(
                    CardTargetFaction.Ally,
                    CardTargetRange.Self),
                layout.Lines[1].Target.Value);
            Assert.AreEqual("방어 2.", layout.Lines[1].Text);
            Assert.AreEqual(
                "[◆] 피해 3. 피해 3.\n[◆] 방어 2.",
                layout.PlainText);
        }

        [Test]
        public void Nonconsecutive_null_targets_share_one_line_without_deduplication()
        {
            var layout = DescriptionComposer.Compose(
                Execution(
                    "repeat_null",
                    new EffectData(EffectKeys.GrantNextTurnFate, 1),
                    DamageEnemy(3),
                    new EffectData(EffectKeys.GrantNextTurnFate, 2)),
                Korean);

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.IsNull(layout.Lines[0].Target);
            Assert.AreEqual(
                "다음 사용 턴에 운명력 1 획득. "
                + "다음 사용 턴에 운명력 2 획득.",
                layout.Lines[0].Text);
            Assert.AreEqual(
                new CardTargetKey(
                    CardTargetFaction.Enemy,
                    CardTargetRange.FrontOne),
                layout.Lines[1].Target.Value);
            Assert.AreEqual("피해 3.", layout.Lines[1].Text);
        }

        [Test]
        public void Conflicting_ranges_include_card_id_and_both_ranges()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                DescriptionComposer.Compose(
                    Execution("conflict",
                        DamageEnemy(3, TargetSelector.FrontOne),
                        PoisonEnemy(1, TargetSelector.BackOne)),
                    Korean));

            StringAssert.Contains("conflict", ex.Message);
            StringAssert.Contains("FrontOne", ex.Message);
            StringAssert.Contains("BackOne", ex.Message);
        }

        [Test]
        public void Every_default_and_generated_card_composes_deterministically()
        {
            var cards = StarterDeckSpecs.Build()
                .Concat(StarterPoolSpecs.Build())
                .Concat(PartyPrototypeDeckSpecs.Build())
                .Select(CardSpecMapper.ToDefinition)
                .Concat(TestContent.StarterDeckCards())
                .Concat(GoblinDeck.AllCards())
                .Concat(WardenDeck.Deck())
                .Concat(new[]
                {
                    TestContent.Cards().Get("fixture_attack"),
                    TestContent.Cards().Get("fixture_selected_block"),
                    TestContent.Cards().Get("fixture_all_block"),
                    TestContent.Cards().Get("fixture_move_forward")
                });
            foreach (var card in cards)
            {
                var first = DescriptionComposer.Compose(card, Korean);
                var second = DescriptionComposer.Compose(card, Korean);

                Assert.AreEqual(first.PlainText, second.PlainText, card.Id);
                CollectionAssert.AreEqual(first.TargetEntries, second.TargetEntries, card.Id);
                CollectionAssert.AreEqual(
                    first.Lines.Select(line => line.Text).ToArray(),
                    second.Lines.Select(line => line.Text).ToArray(),
                    card.Id);
            }
        }

        private static CardDefinition Execution(string id, params EffectData[] effects)
            => new CardDefinition(id, id, Side.Player, 0, effects)
            {
                Category = CardCategory.Execution
            };

        private static EffectData DamageEnemy(int amount, TargetSelector selector = TargetSelector.FrontOne)
            => new EffectData(EffectKeys.Damage, amount) { TargetSelector = selector };

        private static EffectData PoisonEnemy(int amount, TargetSelector selector)
            => EffectData.ApplyStatus(
                StatusKeys.Poison,
                StatusApplyTarget.TargetEnemy,
                amount) with { TargetSelector = selector };

        private static EffectData BlockSelf(int amount)
            => EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusApplyTarget.Self,
                amount);
    }
}
