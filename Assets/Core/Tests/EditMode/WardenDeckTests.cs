using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class WardenDeckTests
    {
        private static CardResolved Resolved(IReadOnlyList<ResolutionEvent> timeline, string cardId)
            => timeline.OfType<CardResolved>().First(e => e.CardId == cardId);

        [Test]
        public void Warden_deck_contract_matches_design()
        {
            Assert.AreEqual("warden", WardenDeck.EnemyId);
            Assert.AreEqual(20, WardenDeck.StartingHp);

            var cards = WardenDeck.Deck();
            Assert.AreEqual(6, cards.Count);
            CollectionAssert.AreEquivalent(
                new[] { "warden_swing", "warden_swing", "warden_smash", "warden_uppercut", "warden_block", "warden_brace" },
                cards.Select(c => c.Id).ToArray());
            Assert.IsTrue(cards.All(c => c.Side == Side.Enemy));
            Assert.IsTrue(cards.All(c => c.EnergyCost == 0));
            Assert.IsTrue(cards.All(c => c.Category == CardCategory.Execution));
        }

        [Test]
        public void Warden_special_cards_have_expected_conditions()
        {
            var smash = WardenDeck.Deck().Single(c => c.Id == "warden_smash");
            var smashEffect = smash.Effects.Single();
            Assert.AreEqual(EffectKeys.Damage, smashEffect.Key);
            Assert.AreEqual(2, smashEffect.EffectValue);
            Assert.AreEqual(7, smashEffect.SuccessEffectValue);
            Assert.AreEqual(Side.Enemy, ((NoFollowingCardOfSide)smashEffect.Condition).Side);

            var uppercut = WardenDeck.Deck().Single(c => c.Id == "warden_uppercut");
            var uppercutEffect = uppercut.Effects.Single();
            Assert.AreEqual(2, uppercutEffect.EffectValue);
            Assert.AreEqual(7, uppercutEffect.SuccessEffectValue);
            Assert.AreEqual(Side.Enemy, ((NoPrecedingCardOfSide)uppercutEffect.Condition).Side);

            var brace = WardenDeck.Deck().Single(c => c.Id == "warden_brace");
            var braceEffect = brace.Effects.Single();
            Assert.AreEqual(EffectKeys.ApplyStatus, braceEffect.Key);
            Assert.AreEqual(3, braceEffect.EffectValue);
            Assert.AreEqual(6, braceEffect.SuccessEffectValue);
            Assert.AreEqual(StatusKeys.Block, ((ApplyStatusPayload)braceEffect.Payload).Key);
            Assert.AreEqual(Side.Enemy, ((NoPrecedingCardOfSide)braceEffect.Condition).Side);
        }

        [Test]
        public void Warden_policy_draws_two_cards_and_locks_one()
        {
            var policy = WardenDeck.Policy();
            var rng = new System.Random(17);
            for (int turn = 0; turn < 6; turn++)
            {
                var cards = policy.CardsForTurn(turn, rng);
                Assert.AreEqual(2, cards.Count);
                Assert.AreEqual(1, cards.Count(c => c.StartsLocked));
            }
        }

        [Test]
        public void Smash_deals_success_damage_when_no_enemy_card_follows()
        {
            var smash = WardenDeck.Smash();
            var session = new DeckCombatSession(
                new[] { WardenDeck.Swing() },
                100,
                new[] { new Enemy(WardenDeck.EnemyId, WardenDeck.StartingHp) },
                new EnemyIntent(new IReadOnlyList<CardDefinition>[] { new[] { smash } }),
                3,
                5,
                1);

            var resolved = Resolved(session.ResolveTurn(), "warden_smash");

            Assert.AreEqual(ConditionTier.Success, resolved.ConditionTier);
            Assert.AreEqual(7, resolved.DamageDealt);
        }

        [Test]
        public void Smash_deals_basic_damage_when_enemy_card_follows()
        {
            var session = new DeckCombatSession(
                new[] { WardenDeck.Swing() },
                100,
                new[] { new Enemy(WardenDeck.EnemyId, WardenDeck.StartingHp) },
                new EnemyIntent(new IReadOnlyList<CardDefinition>[]
                {
                    new[] { WardenDeck.Smash(), WardenDeck.Swing() }
                }),
                3,
                5,
                1);

            var resolved = Resolved(session.ResolveTurn(), "warden_smash");

            Assert.AreEqual(ConditionTier.Basic, resolved.ConditionTier);
            Assert.AreEqual(2, resolved.DamageDealt);
        }
    }
}
