using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class PartyPrototypeDataTests
    {
        // GameContent를 클래스 상태로 캐시하지 않는다 — Statuses의 Rules가 가변이라 인스턴스를
        // 공유하면 다른 테스트의 변경이 샐 수 있다(TestContent.cs:35-46). 호출마다 새로 읽는다.
        private static IReadOnlyList<CardDefinition> PrototypeDeckCards()
        {
            var content = TestContent.Content();
            var cards = new List<CardDefinition>();
            foreach (var id in content.Decks.Get("party_prototype"))
            {
                cards.Add(content.Cards.Get(id));
            }

            return cards;
        }

        [Test]
        public void Prototype_deck_contains_only_validation_prefixed_cards()
        {
            Assert.That(
                PrototypeDeckCards(),
                Is.All.Matches<CardDefinition>(card => card.Name.StartsWith("[검증]")));
        }

        [Test]
        public void Prototype_deck_has_six_cards_and_expected_duplicates()
        {
            var cards = PrototypeDeckCards();
            var attacks = cards.Where(card => card.Id == "fixture_attack").ToList();
            var move = cards.Single(card => card.Id == "fixture_move_forward");

            Assert.AreEqual(6, cards.Count);
            Assert.AreEqual(2, attacks.Count);
            Assert.AreEqual(2, cards.Count(card => card.Id == "fixture_selected_block"));
            Assert.AreEqual(1, cards.Count(card => card.Id == "fixture_all_block"));
            Assert.AreEqual(1, cards.Count(card => card.Id == "fixture_move_forward"));
            Assert.IsTrue(attacks.All(card => card.Effects.Count == 1));
            Assert.IsTrue(attacks.All(card => card.Effects.Single().Key == EffectKeys.Damage));
            Assert.AreEqual(EffectKeys.MoveFormation, move.Effects.Single().Key);
            Assert.AreEqual(-1, move.Effects.Single().EffectValue);
        }

        [Test]
        public void Owner_block_uses_self_without_direct_target_selection()
        {
            var ownerBlock = PrototypeDeckCards()
                .First(card => card.Id == "fixture_selected_block");

            Assert.IsTrue(PartyTargetRules.IsValidBaseExecutionDefinition(ownerBlock));
            Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(ownerBlock));
            Assert.AreEqual(
                StatusApplyTarget.Self,
                ((ApplyStatusPayload)ownerBlock.Effects.Single().Payload).Target);
        }

        [Test]
        public void All_block_does_not_open_direct_target_selection()
        {
            var allBlock = PrototypeDeckCards()
                .Single(card => card.Id == "fixture_all_block");

            Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(allBlock));
            Assert.AreEqual(
                StatusApplyTarget.AllPartyMembers,
                ((ApplyStatusPayload)allBlock.Effects.Single().Payload).Target);
        }
    }
}
