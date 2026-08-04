using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class PartyPrototypeDataTests
    {
        private sealed class NoEnemyTurns : IEnemyTurnPolicy
        {
            public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
                => Array.Empty<CardDefinition>();
        }

        [Test]
        public void Prototype_deck_contains_only_validation_prefixed_cards()
        {
            Assert.That(
                PartyPrototypeDeck.Build(),
                Is.All.Matches<CardDefinition>(card => card.Name.StartsWith("[검증]")));
        }

        [Test]
        public void Prototype_deck_has_six_cards_and_expected_duplicates()
        {
            var cards = PartyPrototypeDeck.Build();
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
        public void Hand_coded_and_authored_specs_map_to_equal_definitions()
        {
            var handCoded = PartyPrototypeDeck.Build();
            var authored = PartyPrototypeDeckSpecs.Build()
                .Select(CardSpecMapper.ToDefinition)
                .ToList();

            Assert.AreEqual(handCoded.Count, authored.Count);
            for (var i = 0; i < handCoded.Count; i++)
            {
                AssertDefinitionsEqual(handCoded[i], authored[i]);
            }
        }

        [Test]
        public void Owner_block_uses_self_without_direct_target_selection()
        {
            var ownerBlock = PartyPrototypeDeck.Build()
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
            var allBlock = PartyPrototypeDeck.Build()
                .Single(card => card.Id == "fixture_all_block");

            Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(allBlock));
            Assert.AreEqual(
                StatusApplyTarget.AllPartyMembers,
                ((ApplyStatusPayload)allBlock.Effects.Single().Payload).Target);
        }

        [Test]
        public void Roster_assigns_distinct_character_owners()
        {
            var roster = PartyPrototypeRoster.Build();
            var session = new DeckCombatSession(TestContent.Statuses(),
                roster,
                Array.Empty<Enemy>(),
                new NoEnemyTurns(),
                PartyPrototypeRoster.Tuning);

            Assert.AreEqual(2, roster.Count);
            Assert.AreEqual("member_a", roster[0].Id);
            Assert.AreEqual("파티원 A", roster[0].Name);
            Assert.AreEqual("member_b", roster[1].Id);
            Assert.AreEqual("파티원 B", roster[1].Name);
            CollectionAssert.AreEqual(
                StarterDeck.Build().Select(card => card.Id),
                roster[0].Cards.Select(card => card.Id));
            CollectionAssert.AreEqual(
                new[] { "member_a", "member_b" },
                session.AllDeckCards.Select(card => card.OwnerId).Distinct());
            Assert.AreEqual(StarterDeck.Build().Count, session.AllDeckCards.Count(card => card.OwnerId == "member_a"));
            Assert.AreEqual(6, session.AllDeckCards.Count(card => card.OwnerId == "member_b"));
            Assert.IsTrue(roster.All(member => member.MaxHp == PartyPrototypeRoster.Tuning.DefaultMemberMaxHp));
        }

        private static void AssertDefinitionsEqual(CardDefinition expected, CardDefinition actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.Side, actual.Side);
            Assert.AreEqual(expected.Category, actual.Category);
            Assert.AreEqual(expected.EnergyCost, actual.EnergyCost);
            Assert.AreEqual(expected.BaseExecutionOrder, actual.BaseExecutionOrder);
            CollectionAssert.AreEqual(expected.Effects, actual.Effects);
            Assert.AreEqual(expected.InterventionAction, actual.InterventionAction);
            Assert.AreEqual(expected.StartsLocked, actual.StartsLocked);
        }
    }
}
