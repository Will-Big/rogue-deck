using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class PartyDeckCombatSessionTests
    {
        private static CardDefinition Execution(
            string id,
            int order = 5,
            int cost = 0,
            IReadOnlyList<EffectData> effects = null)
            => new CardDefinition(
                id,
                id,
                Side.Player,
                CardType.Skill,
                order,
                effects ?? Array.Empty<EffectData>())
            {
                Category = CardCategory.Execution,
                EnergyCost = cost
            };

        private static CardDefinition DirectBlock(string id = "direct_block")
            => Execution(
                id,
                cost: 1,
                effects: new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block,
                        StatusLifetime.ThisTurn,
                        StatusApplyTarget.PartyMember,
                        magnitude: 3)
                });

        private static CardDefinition EnemyStrike(
            string id = "enemy_strike",
            int order = 1,
            int damage = 50,
            TargetSelector selector = TargetSelector.FrontMost)
            => new CardDefinition(
                id,
                id,
                Side.Enemy,
                CardType.Attack,
                order,
                new[] { new EffectData(EffectKeys.Damage, damage) { TargetSelector = selector } })
            {
                Category = CardCategory.Execution
            };

        private static PartyMemberLoadout Loadout(
            string id,
            IReadOnlyList<CardDefinition> cards = null,
            int maxHp = 25,
            string name = null)
            => new PartyMemberLoadout(id, name ?? id, maxHp, cards ?? Array.Empty<CardDefinition>());

        private static PartyTuning Tuning(int partySize)
        {
            var draw = new Dictionary<int, int>();
            for (int living = 1; living <= partySize; living++)
            {
                draw.Add(living, living + 2);
            }

            return new PartyTuning
            {
                DefaultMemberMaxHp = 25,
                SurviveChargesPerCombat = 1,
                DrawByLivingCount = draw
            };
        }

        private static DeckCombatSession Session(
            IReadOnlyList<PartyMemberLoadout> party,
            IReadOnlyList<CardDefinition> enemyCards = null,
            PartyTuning tuning = null,
            IReadOnlyList<CardDefinition> partyCards = null,
            int fateEnergyPerTurn = 3,
            int seed = 1)
            => new DeckCombatSession(
                party,
                new[] { new Enemy("goblin", 100) },
                new EnemyIntent(new IReadOnlyList<CardDefinition>[]
                {
                    enemyCards ?? Array.Empty<CardDefinition>()
                }),
                tuning ?? Tuning(party.Count),
                partyCards,
                fateEnergyPerTurn,
                seed);

        [Test]
        public void Constructor_rejects_empty_oversized_duplicate_or_invalid_party()
        {
            Assert.Throws<ArgumentException>(() => Session(Array.Empty<PartyMemberLoadout>(), tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => Session(new[]
            {
                Loadout("a"), Loadout("b"), Loadout("c"), Loadout("d")
            }, tuning: Tuning(4)));
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout("same"), Loadout("same") }));
            Assert.Throws<ArgumentException>(() => Session(new PartyMemberLoadout[] { null }, tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout(null) }, tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout(string.Empty) }, tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout("a", maxHp: 0) }, tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => Session(new[]
            {
                new PartyMemberLoadout("a", "A", 25, null)
            }, tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => Session(new[]
            {
                Loadout("a", new CardDefinition[] { null })
            }, tuning: Tuning(1)));
            Assert.Throws<ArgumentException>(() => new DeckCombatSession(
                new[] { Loadout("a") },
                new[] { new Enemy("goblin", 100) },
                new EnemyIntent(Array.Empty<IReadOnlyList<CardDefinition>>()),
                tuning: null));
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout("a") }, tuning: new PartyTuning
            {
                DefaultMemberMaxHp = 0,
                SurviveChargesPerCombat = 1,
                DrawByLivingCount = new Dictionary<int, int> { { 1, 3 } }
            }));
            Assert.Throws<ArgumentException>(() => Session(new[] { Loadout("a") }, tuning: new PartyTuning
            {
                DefaultMemberMaxHp = 25,
                SurviveChargesPerCombat = -1,
                DrawByLivingCount = new Dictionary<int, int> { { 1, 3 } }
            }));
        }

        [Test]
        public void Constructor_rejects_missing_or_non_positive_draw_tuning_entries()
        {
            var party = new[] { Loadout("a"), Loadout("b") };

            Assert.Throws<ArgumentException>(() => Session(party, tuning: new PartyTuning
            {
                DefaultMemberMaxHp = 25,
                SurviveChargesPerCombat = 1,
                DrawByLivingCount = null
            }));
            Assert.Throws<ArgumentException>(() => Session(party, tuning: new PartyTuning
            {
                DefaultMemberMaxHp = 25,
                SurviveChargesPerCombat = 1,
                DrawByLivingCount = new Dictionary<int, int> { { 1, 3 } }
            }));
            Assert.Throws<ArgumentException>(() => Session(party, tuning: new PartyTuning
            {
                DefaultMemberMaxHp = 25,
                SurviveChargesPerCombat = 1,
                DrawByLivingCount = new Dictionary<int, int> { { 1, 3 }, { 2, 0 } }
            }));
        }

        [Test]
        public void Prototype_tuning_is_hp_25_survive_1_and_draw_3_4_5()
        {
            var tuning = PartyTuning.Prototype;

            Assert.AreEqual(1, tuning.MinPartySize);
            Assert.AreEqual(3, tuning.MaxPartySize);
            Assert.AreEqual(25, tuning.DefaultMemberMaxHp);
            Assert.AreEqual(1, tuning.SurviveChargesPerCombat);
            Assert.AreEqual(3, tuning.DrawFor(1));
            Assert.AreEqual(4, tuning.DrawFor(2));
            Assert.AreEqual(5, tuning.DrawFor(3));
        }

        [Test]
        public void Dead_or_missing_direct_target_rejects_play_without_mutation()
        {
            var session = Session(new[]
            {
                Loadout("a", new[] { DirectBlock() }),
                Loadout("b")
            }, new[] { EnemyStrike(damage: 0) });
            session.State.Party.Single(member => member.Id == "b").Hp = 0;

            var energy = session.FateEnergy;
            var hand = session.Hand.ToArray();
            var zone = session.CurrentOrder.ToArray();
            var highestInstanceId = zone.Max(card => card.InstanceId);

            Assert.IsFalse(session.PlayExecutionCard(0, "missing"));
            Assert.IsFalse(session.PlayExecutionCard(0, "b"));
            Assert.AreEqual(energy, session.FateEnergy);
            CollectionAssert.AreEqual(hand, session.Hand);
            CollectionAssert.AreEqual(zone, session.CurrentOrder);

            session.State.Party.Single(member => member.Id == "b").Hp = 25;
            Assert.IsTrue(session.PlayExecutionCard(0, "b"));
            Assert.AreEqual(highestInstanceId + 1, session.CurrentOrder.Single(card => card.Def.Id == "direct_block").InstanceId);
        }

        [Test]
        public void Valid_target_can_be_placed_and_later_cancelled_if_target_dies()
        {
            var session = Session(
                new[]
                {
                    Loadout("a", new[] { DirectBlock() }),
                    Loadout("b")
                },
                new[] { EnemyStrike(selector: TargetSelector.BackMost) });
            session.State.Party.Single(member => member.Id == "b").SurviveCharges = 0;

            Assert.IsTrue(session.PlayExecutionCard(0, "b"));
            var placed = session.CurrentOrder.Single(card => card.Def.Id == "direct_block");
            Assert.AreEqual("b", placed.TargetId);

            var timeline = session.ResolveTurn();

            var cancelled = timeline.OfType<CardCancelled>().Single(e => e.CardId == "direct_block");
            Assert.AreEqual(CardCancellationReason.NoValidTarget, cancelled.Reason);
            Assert.IsFalse(session.State.Party.Single(member => member.Id == "b").Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Death_removes_owned_cards_but_keeps_party_owned_cards()
        {
            var ownedByA = Enumerable.Range(0, 5).Select(i => Execution("a_" + i)).ToArray();
            var session = Session(
                new[]
                {
                    Loadout("a", ownedByA),
                    Loadout("b")
                },
                new[] { EnemyStrike() },
                partyCards: new[] { Execution("party_card") });
            session.State.Party.Single(member => member.Id == "a").SurviveCharges = 0;

            session.ResolveTurn();

            var remaining = session.DrawPile.Concat(session.Hand).Concat(session.DiscardPile).ToArray();
            Assert.IsFalse(remaining.Any(card => card.OwnerId == "a"));
            Assert.IsTrue(remaining.Any(card => card.Def.Id == "party_card" && card.IsPartyOwned));
        }

        [Test]
        public void Kill_then_cancel_path_removes_dead_owner_from_every_pile_and_keeps_party_cards()
        {
            var ownedByA = Enumerable.Range(0, 8).Select(i => Execution("a_" + i, order: 2)).ToArray();
            var killThenCancel = new CardDefinition(
                "kill_then_cancel",
                "kill_then_cancel",
                Side.Enemy,
                CardType.Attack,
                1,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 25),
                    new EffectData(EffectKeys.Damage, 1)
                })
            {
                Category = CardCategory.Execution
            };
            var session = Session(
                new[] { Loadout("a", ownedByA) },
                new[] { killThenCancel },
                partyCards: new[] { Execution("party_0"), Execution("party_1") });
            session.State.Party.Single().SurviveCharges = 0;
            var ownedHandIndex = session.Hand
                .Select((card, index) => new { card, index })
                .First(entry => entry.card.OwnerId == "a")
                .index;
            Assert.IsTrue(session.PlayExecutionCard(ownedHandIndex));

            var timeline = session.ResolveTurn();

            var relevant = timeline.Where(e => e is CardCancelled || e is PartyMemberDied).ToArray();
            Assert.AreEqual(3, relevant.Length);
            Assert.AreEqual("kill_then_cancel", ((CardCancelled)relevant[0]).CardId);
            Assert.AreEqual("a", ((PartyMemberDied)relevant[1]).MemberId);
            Assert.AreEqual(CardCancellationReason.OwnerDied, ((CardCancelled)relevant[2]).Reason);
            Assert.IsFalse(session.DrawPile.Any(card => card.OwnerId == "a"));
            Assert.IsFalse(session.Hand.Any(card => card.OwnerId == "a"));
            Assert.IsFalse(session.DiscardPile.Any(card => card.OwnerId == "a"));
            Assert.IsTrue(
                session.DrawPile.Concat(session.Hand).Concat(session.DiscardPile)
                    .Any(card => card.IsPartyOwned));
        }

        [Test]
        public void Draw_count_uses_living_member_count()
        {
            var session = Session(
                new[]
                {
                    Loadout("a", new[] { Execution("a_card") }),
                    Loadout("b", Enumerable.Range(0, 8).Select(i => Execution("b_" + i)).ToArray())
                },
                new[] { EnemyStrike() });
            session.State.Party.Single(member => member.Id == "a").SurviveCharges = 0;
            Assert.AreEqual(4, session.Hand.Count);

            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());

            Assert.AreEqual(3, session.Hand.Count);
            Assert.IsTrue(session.Hand.All(card => card.OwnerId == "b"));
        }

        [Test]
        public void One_survivor_can_continue_the_next_turn()
        {
            var session = Session(
                new[] { Loadout("a"), Loadout("b", new[] { Execution("b_card") }) },
                new[] { EnemyStrike() });
            session.State.Party.Single(member => member.Id == "a").SurviveCharges = 0;

            var timeline = session.ResolveTurn();

            Assert.AreEqual(Outcome.Ongoing, timeline.OfType<TurnEnded>().Single().Outcome);
            Assert.IsTrue(session.BeginNextTurn());
            Assert.AreEqual(1, session.TurnIndex);
            Assert.AreEqual(1, session.State.Party.Count(member => member.IsAlive));
        }

        [Test]
        public void Haste_and_slow_on_a_do_not_change_b_card_execution_order()
        {
            var session = Session(new[]
            {
                Loadout("a"),
                Loadout("b", new[] { Execution("b_card", order: 5) })
            });
            var a = session.State.Party.Single(member => member.Id == "a");
            a.Statuses.Add(StatusKeys.Haste, StatusLifetime.Turns(2), magnitude: 4);
            a.Statuses.Add(StatusKeys.Slow, StatusLifetime.Turns(2), magnitude: 2);

            Assert.IsTrue(session.PlayExecutionCard(0));

            var bCard = session.CurrentOrder.Single(card => card.Def.Id == "b_card");
            Assert.AreEqual("b", bCard.OwnerId);
            Assert.AreEqual(5, bCard.ExecutionOrder);
        }
    }
}
