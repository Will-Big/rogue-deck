using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Enemies;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>적이 둘 이상인 전투: 카드 주인, 죽은 적의 정책·카드, 적별 실행 순서 보정.
    /// 편성은 전투 안 id(goblin#0, goblin#1)로 적을 가른다.</summary>
    public class MultiEnemyCombatTests
    {
        private static CardDefinition HeroStrike(int damage = 5) => new CardDefinition(
            "hero_strike", "찌르기", Side.Player, 5,
            new[] { new EffectData(EffectKeys.Damage, damage) { TargetFaction = CardTargetFaction.Enemy } })
            { EnemyTarget = CardTargetRange.FrontOne, EnergyCost = 0, Category = CardCategory.Execution };

        private static CardDefinition EnemyJab(string id, int order = 5, int damage = 1) => new CardDefinition(
            id, id, Side.Enemy, order,
            new[] { new EffectData(EffectKeys.Damage, damage) { TargetFaction = CardTargetFaction.Ally } })
            { AllyTarget = CardTargetRange.FrontOne, EnergyCost = 0, Category = CardCategory.Execution };

        /// <summary>정책이 실제로 몇 번 불렸는지 센다 — 죽은 적을 건너뛰는지는 존에 카드가 없는
        /// 것만으로는 구분되지 않는다(RNG 소비도 없어야 한다).</summary>
        private sealed class CountingPolicy : IEnemyTurnPolicy
        {
            private readonly CardDefinition _card;

            public CountingPolicy(CardDefinition card) => _card = card;

            public int Calls { get; private set; }

            public IReadOnlyList<CardDefinition> CardsForTurn(int turnIndex, Random rng)
            {
                Calls++;
                return new[] { _card };
            }
        }

        private static PartyTuning Tuning() => new PartyTuning
        {
            MinPartySize = 1,
            MaxPartySize = 3,
            DrawByLivingCount = new Dictionary<int, int> { { 1, 3 } }
        };

        private static DeckCombatSession Session(params EncounterEnemy[] enemies)
            => new DeckCombatSession(
                TestContent.Statuses(),
                new[]
                {
                    new PartyMemberLoadout(
                        "hero", "용사", 40, Enumerable.Range(0, 6).Select(_ => HeroStrike()).ToList())
                },
                enemies,
                Tuning(),
                partyCards: null,
                fateEnergyPerTurn: 3,
                seed: 1);

        private static EncounterEnemy Pair(string id, int hp, IEnemyTurnPolicy policy)
            => new EncounterEnemy(new Enemy(id, hp), policy);

        private static IEnemyTurnPolicy Every(CardDefinition card)
            => new CountingPolicy(card);

        [Test]
        public void Each_enemy_places_its_own_cards_and_owns_them()
        {
            var session = Session(
                Pair("goblin#0", 50, Every(EnemyJab("jab_a"))),
                Pair("goblin#1", 50, Every(EnemyJab("jab_b"))));

            var placed = session.CurrentOrder
                .Where(card => card.Def.Side == Side.Enemy)
                .ToDictionary(card => card.Def.Id, card => card.OwnerId);

            Assert.AreEqual("goblin#0", placed["jab_a"]);
            Assert.AreEqual("goblin#1", placed["jab_b"]);
        }

        [Test]
        public void A_dead_enemy_places_no_cards_and_its_policy_is_not_called_again()
        {
            var doomed = new CountingPolicy(EnemyJab("jab_a"));
            var survivor = new CountingPolicy(EnemyJab("jab_b"));
            var session = Session(Pair("goblin#0", 5, doomed), Pair("goblin#1", 50, survivor));

            Assert.IsTrue(session.PlayExecutionCard(0), "전제: 손패 첫 장이 hero_strike여야 한다.");
            session.ResolveTurn();
            Assert.IsFalse(session.State.Enemies[0].Hp > 0, "전제: 앞줄 적이 죽어야 한다.");
            Assert.IsTrue(session.BeginNextTurn());

            Assert.AreEqual(1, doomed.Calls, "죽은 적의 정책은 다시 불리지 않는다.");
            Assert.AreEqual(2, survivor.Calls);
            CollectionAssert.AreEqual(
                new[] { "jab_b" },
                session.CurrentOrder.Where(c => c.Def.Side == Side.Enemy).Select(c => c.Def.Id).ToArray());
        }

        [Test]
        public void A_dead_enemys_pending_card_disappears_from_the_line()
        {
            // 적 카드는 실행 순서 9라 플레이어(5) 뒤다. 그 사이에 주인이 죽으면 실행되지 않는다.
            var session = Session(
                Pair("goblin#0", 5, Every(EnemyJab("jab_a", order: 9, damage: 7))),
                Pair("goblin#1", 50, Every(EnemyJab("jab_b", order: 9, damage: 7))));
            var heroHpBefore = session.State.Party.Single().Hp;

            Assert.IsTrue(session.PlayExecutionCard(0), "전제: 손패 첫 장이 hero_strike여야 한다.");
            var timeline = session.ResolveTurn();

            Assert.IsTrue(
                timeline.OfType<CardRemoved>().Any(e => e.CardId == "jab_a"),
                "죽은 적의 대기 카드는 실행선에서 사라진다.");
            Assert.AreEqual(
                heroHpBefore - 7, session.State.Party.Single().Hp,
                "살아남은 적의 카드 한 장만 맞아야 한다.");
        }

        [Test]
        public void Execution_order_is_corrected_by_the_placing_enemys_own_statuses()
        {
            var session = Session(
                Pair("goblin#0", 50, Every(EnemyJab("jab_a"))),
                Pair("goblin#1", 50, Every(EnemyJab("jab_b"))));
            var baseOrder = session.CurrentOrder.First(c => c.Def.Id == "jab_a").ExecutionOrder;

            // 둘째 적만 느리게 한다. 첫째 적의 카드 순서는 그대로여야 한다.
            session.State.Enemies[1].Statuses.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());

            Assert.AreEqual(
                baseOrder, session.CurrentOrder.First(c => c.Def.Id == "jab_a").ExecutionOrder);
            Assert.Greater(
                session.CurrentOrder.First(c => c.Def.Id == "jab_b").ExecutionOrder, baseOrder);
        }

        [Test]
        public void Cards_enter_the_line_in_formation_order()
        {
            // 실행 순서가 같은 적 카드끼리는 대형 앞줄 것이 앞선다. 진형이 바뀌면 다음 턴부터 따라간다.
            var session = Session(
                Pair("goblin#0", 50, Every(EnemyJab("jab_a"))),
                Pair("goblin#1", 50, Every(EnemyJab("jab_b"))));
            CollectionAssert.AreEqual(
                new[] { "jab_a", "jab_b" }, EnemyCardIds(session));

            var front = session.State.Enemies[0];
            session.State.Enemies.RemoveAt(0);
            session.State.Enemies.Add(front);
            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());

            CollectionAssert.AreEqual(new[] { "jab_b", "jab_a" }, EnemyCardIds(session));
        }

        private static string[] EnemyCardIds(DeckCombatSession session)
            => session.CurrentOrder.Where(c => c.Def.Side == Side.Enemy).Select(c => c.Def.Id).ToArray();

        [Test]
        public void The_same_enemy_twice_gets_its_own_hp_and_cards()
        {
            var session = Session(
                Pair("goblin#0", 5, Every(EnemyJab("jab_a"))),
                Pair("goblin#1", 50, Every(EnemyJab("jab_b"))));

            Assert.IsTrue(session.PlayExecutionCard(0));
            session.ResolveTurn();

            Assert.AreEqual(0, Math.Max(0, session.State.Enemies[0].Hp));
            Assert.AreEqual(50, session.State.Enemies[1].Hp, "한쪽을 죽여도 다른 쪽 HP는 그대로다.");
            Assert.AreEqual(Outcome.Ongoing, session.Outcome, "적이 하나 남았으면 전투는 계속된다.");
        }

        [Test]
        public void Duplicate_encounter_ids_are_rejected()
        {
            Assert.Throws<ArgumentException>(() => Session(
                Pair("goblin#0", 5, Every(EnemyJab("jab_a"))),
                Pair("goblin#0", 5, Every(EnemyJab("jab_b")))));
        }
    }
}
