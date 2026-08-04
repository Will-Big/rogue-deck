using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Descriptions;
using NUnit.Framework;

namespace FateWeaver.Tests.EditMode
{
    public class DescriptionCatalogValidatorTests
    {
        private static IReadOnlyList<CardDefinition> DefaultCards()
            => TestContent.StarterDeckCards()
                .Concat(GoblinDeck.AllCards())
                .Concat(WardenDeck.Deck())
                .Concat(PartyPrototypeCards())
                .ToArray();

        private static IReadOnlyList<CardDefinition> PartyPrototypeCards()
        {
            var cards = TestContent.Cards();
            return new[]
            {
                cards.Get("fixture_attack"),
                cards.Get("fixture_selected_block"),
                cards.Get("fixture_all_block"),
                cards.Get("fixture_move_forward")
            };
        }

        [Test]
        public void Every_default_card_has_runtime_and_description_registrations()
        {
            Assert.DoesNotThrow(() =>
                DescriptionCatalogValidator.ValidateDefault(
                    DefaultCards(),
                    KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses())));
        }

        [Test]
        public void Unknown_effect_key_fails_preflight()
        {
            var card = new CardDefinition(
                "unknown",
                "unknown",
                Side.Player,
                5,
                new[] { new EffectData(new EffectKey("unknown_effect"), 1) })
                { Category = CardCategory.Execution };

            Assert.Throws<KeyNotFoundException>(() =>
                DescriptionCatalogValidator.ValidateDefault(
                    new[] { card },
                    KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses())));
        }

        [Test]
        public void Unknown_status_key_fails_preflight()
        {
            var card = new CardDefinition(
                "unknown_status",
                "unknown",
                Side.Player,
                5,
                new[]
                {
                    EffectData.ApplyStatus(
                        new StatusKey("unknown_status"),
                        StatusApplyTarget.Self,
                        1)
                })
                { Category = CardCategory.Execution };

            Assert.Throws<KeyNotFoundException>(() =>
                DescriptionCatalogValidator.ValidateDefault(
                    new[] { card },
                    KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses())));
        }

        [Test]
        public void Intervention_category_requires_an_action()
        {
            var card = new CardDefinition(
                "invalid_intervention",
                "invalid",
                Side.Player,
                0,
                new EffectData[0])
                { Category = CardCategory.Intervention };

            Assert.Throws<ArgumentException>(() =>
                DescriptionCatalogValidator.ValidateDefault(
                    new[] { card },
                    KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses())));
        }
    }
}
