using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    /// <summary>조건부 효과 셋이 세션 전체(배치 → 개입 → 해결)를 통과해 옳게 동작하는지 잠근다.
    /// 입력은 합성 픽스처다 — 특정 카드의 밸런스가 아니라 조건 판정 규칙이 검증 대상이다.</summary>
    public class ConditionalCardRuleTests
    {
        private static EnemyIntent Goblin(int executionOrder, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { CardFixtures.EnemyAttack("goblin_jab", executionOrder, damage) }
            });

        private static int HandIndex(DeckCombatSession s, string id)
        {
            for (int i = 0; i < s.Hand.Count; i++) if (s.Hand[i].Def.Id == id) return i;
            return -1;
        }

        private static int ZoneIndex(DeckCombatSession s, string id)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++) if (order[i].Def.Id == id) return i;
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> t, string id)
            => t.OfType<CardResolved>().First(e => e.CardId == id).DamageDealt;

        [Test]
        public void Conditional_damage_on_first_trigger_uses_the_boosted_value()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[]
                {
                    CardFixtures.DamageOnFirstTrigger("quick_cut", baseDamage: 2, whenFirst: 8),
                    CardFixtures.ChangeExecutionOrder("pull_forward", delta: -1)
                },
                30, new[] { new Enemy("goblin", 100) }, Goblin(5, 3), 3, 5, 1);

            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            session.PlayInterventionCard(
                HandIndex(session, "pull_forward"), ZoneIndex(session, "quick_cut"));

            Assert.AreEqual(8, DamageOf(session.ResolveTurn(), "quick_cut"));
        }

        [Test]
        public void Conditional_damage_after_an_enemy_damage_card_uses_the_boosted_value()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[]
                {
                    CardFixtures.DamageAfterEnemyDamage(
                        "counter_stance", baseDamage: 4, whenAfter: 9, executionOrder: 7, cost: 2)
                },
                30, new[] { new Enemy("goblin", 100) }, Goblin(6, 4), 3, 5, 1);

            session.PlayExecutionCard(HandIndex(session, "counter_stance"));

            Assert.AreEqual(9, DamageOf(session.ResolveTurn(), "counter_stance"));
        }

        [Test]
        public void Conditional_block_before_an_enemy_damage_card_absorbs_the_hit()
        {
            var session = new DeckCombatSession(TestContent.Statuses(),
                new[]
                {
                    CardFixtures.BlockBeforeEnemyDamage("cover", baseMagnitude: 2, whenBefore: 7)
                },
                30, new[] { new Enemy("goblin", 100) }, Goblin(6, 3), 3, 5, 1);

            session.PlayExecutionCard(HandIndex(session, "cover"));
            int hp = session.State.Party[0].Hp;
            session.ResolveTurn();

            Assert.AreEqual(hp, session.State.Party[0].Hp);
        }
    }
}
