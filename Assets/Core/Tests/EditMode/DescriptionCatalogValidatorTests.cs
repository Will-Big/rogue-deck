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

        /// <summary>party_prototype 덱의 모든 카드를 콘텐츠에서 읽는다 — id를 여기 박아두면
        /// 다섯 번째 프로토타입 카드가 이 "모든 기본 카드에 등록이 있다" 검사를 조용히 피해 갈 수
        /// 있다. 덱은 같은 id를 중복해서 담으므로(예: fixture_attack 2장) Distinct로 접는다.</summary>
        private static IReadOnlyList<CardDefinition> PartyPrototypeCards()
        {
            var content = TestContent.Content();
            return content.Decks.Get("party_prototype")
                .Distinct()
                .Select(id => content.Cards.Get(id))
                .ToArray();
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
