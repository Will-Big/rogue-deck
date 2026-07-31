using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Generated;

namespace FateWeaver.Tests.EditMode
{
    public class StructuredCardDescriptionTests
    {
        private static readonly KoreanDescriptionCatalog Korean =
            KoreanDescriptionCatalog.CreateDefault();

        [Test]
        public void Toxic_reclaim_separates_enemy_and_ally_self_lines()
        {
            var definition = GeneratedCards.StarterPool()
                .Select(CardSpecMapper.ToDefinition)
                .Single(card => card.Id == "toxic_reclaim");

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
                "[◆] 독 최대 1 소비. 독 1.\n[◇◎] 소비했다면 방어 4.",
                layout.PlainText);
        }

        [Test]
        public void Repeated_nonconsecutive_target_keeps_three_lines()
        {
            var layout = DescriptionComposer.Compose(
                Execution("repeat", DamageEnemy(3), BlockSelf(2), DamageEnemy(3)), Korean);

            Assert.AreEqual(3, layout.Lines.Count);
            Assert.AreEqual(2, layout.TargetEntries.Count);
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
            var cards = GeneratedCards.StarterDeck()
                .Concat(GeneratedCards.StarterPool())
                .Select(CardSpecMapper.ToDefinition)
                .Concat(StarterDeck.Build())
                .Concat(GoblinDeck.AllCards())
                .Concat(WardenDeck.Deck())
                .Concat(PartyPrototypeDeck.Build());
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
                StatusLifetime.Permanent,
                StatusApplyTarget.TargetEnemy,
                amount) with { TargetSelector = selector };

        private static EffectData BlockSelf(int amount)
            => EffectData.ApplyStatus(
                StatusKeys.Block,
                StatusLifetime.ThisTurn,
                StatusApplyTarget.Self,
                amount);
    }
}
