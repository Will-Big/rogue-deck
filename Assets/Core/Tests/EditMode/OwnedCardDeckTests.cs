using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class OwnedCardDeckTests
    {
        private static CardDefinition Card(string id, Side side = Side.Player) => new CardDefinition(
            id, id, side, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) })
        {
            Category = CardCategory.Execution,
            EnergyCost = 1
        };

        private static OwnedCard Owned(string id, string ownerId)
            => new OwnedCard(Card(id), ownerId);

        [Test]
        public void Remove_owned_by_removes_matching_cards_from_all_three_piles()
        {
            var deck = new Deck(new[]
            {
                Owned("draw", "warrior"),
                Owned("hand", "warrior"),
                Owned("discard", "warrior")
            }, seed: 1);
            deck.Draw(2);
            deck.DiscardFromHand(0);

            deck.RemoveOwnedBy("warrior");

            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(0, deck.HandCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void Remove_owned_by_keeps_other_character_cards()
        {
            var deck = new Deck(new[]
            {
                Owned("warrior_card", "warrior"),
                Owned("mage_card", "mage")
            }, seed: 1);
            deck.Draw(1);

            deck.RemoveOwnedBy("warrior");

            var remaining = deck.DrawPile.Concat(deck.Hand).Concat(deck.DiscardPile).ToArray();
            Assert.AreEqual(1, remaining.Length);
            Assert.AreEqual("mage", remaining[0].OwnerId);
        }

        [Test]
        public void Remove_owned_by_keeps_party_owned_cards()
        {
            var deck = new Deck(new[]
            {
                Owned("warrior_card", "warrior"),
                Owned("party_card", null)
            }, seed: 1);
            deck.Draw(1);

            deck.RemoveOwnedBy("warrior");

            var remaining = deck.DrawPile.Concat(deck.Hand).Concat(deck.DiscardPile).ToArray();
            Assert.AreEqual(1, remaining.Length);
            Assert.IsTrue(remaining[0].IsPartyOwned);
            Assert.AreEqual("party_card", remaining[0].Def.Id);
        }

        [Test]
        public void Placement_copies_owner_and_assigns_unique_instance_id()
        {
            var session = new DeckCombatSession(
                new[] { Owned("warrior_card", "warrior") },
                playerHp: 30,
                enemies: new[] { new Enemy("goblin", 30) },
                enemyPolicy: EnemyPolicy(Card("enemy_card", Side.Enemy)),
                fateEnergyPerTurn: 3,
                handSize: 1,
                seed: 1);

            Assert.IsTrue(session.PlayExecutionCard(0));

            var enemy = session.CurrentOrder.Single(c => c.Def.Id == "enemy_card");
            var player = session.CurrentOrder.Single(c => c.Def.Id == "warrior_card");
            Assert.AreEqual("warrior", player.OwnerId);
            Assert.AreNotEqual(enemy.InstanceId, player.InstanceId);
            Assert.GreaterOrEqual(enemy.InstanceId, 0);
            Assert.GreaterOrEqual(player.InstanceId, 0);

            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());
            var nextEnemy = session.CurrentOrder.Single(c => c.Def.Id == "enemy_card");
            Assert.Greater(nextEnemy.InstanceId, player.InstanceId);

            var newSession = new DeckCombatSession(
                new[] { Owned("warrior_card", "warrior") },
                playerHp: 30,
                enemies: new[] { new Enemy("goblin", 30) },
                enemyPolicy: EnemyPolicy(Card("enemy_card", Side.Enemy)),
                fateEnergyPerTurn: 3,
                handSize: 1,
                seed: 1);
            Assert.AreEqual(enemy.InstanceId, newSession.CurrentOrder.Single().InstanceId);
        }

        [Test]
        public void Legacy_definition_deck_assigns_legacy_player_owner()
        {
            var deck = new Deck(new[] { Card("legacy") }, seed: 1);

            Assert.AreEqual(CombatState.LegacyPlayerId, deck.DrawPile.Single().OwnerId);
        }

        private static IEnemyTurnPolicy EnemyPolicy(CardDefinition enemyCard)
            => new EnemyIntent(new IReadOnlyList<CardDefinition>[] { new[] { enemyCard } });
    }
}
