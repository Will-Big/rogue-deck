using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class PartyTargetingTests
    {
        [Test]
        public void Front_two_returns_up_to_two_distinct_living_members_in_order()
        {
            var state = new CombatState(TestContent.Statuses());
            var deadFront = new PartyMember("a", "A", maxHp: 10) { Hp = 0 };
            state.Party.Add(deadFront);
            state.Party.Add(new PartyMember("b", "B", maxHp: 10));
            state.Party.Add(new PartyMember("c", "C", maxHp: 10));
            state.Party.Add(new PartyMember("d", "D", maxHp: 10));

            CollectionAssert.AreEqual(
                new[] { "b", "c" },
                PartyTargeting.SelectRange(state, CardTargetRange.FrontTwo)
                    .Select(member => member.Id));
        }

        [TestCase(CardTargetRange.FrontTwo)]
        [TestCase(CardTargetRange.BackTwo)]
        [TestCase(CardTargetRange.All)]
        public void One_living_member_range_returns_that_member_once(CardTargetRange selector)
        {
            var state = new CombatState(TestContent.Statuses());
            var only = new PartyMember("only", "Only", maxHp: 10);
            state.Party.Add(only);

            var targets = PartyTargeting.SelectRange(state, selector);

            Assert.AreEqual(1, targets.Count);
            Assert.AreSame(only, targets[0]);
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new BlockBehavior());
            r.Register(new VulnerableBehavior());
            return r;
        }

        /// <summary>효과 하나짜리 카드. 두 진영 축을 같은 범위로 둔다 — 효과 진영이 어느 축을 쓸지 고른다.</summary>
        private static ExecutionCardInstance Card(string id, Side side, EffectData effect, CardTargetRange range)
            => new ExecutionCardInstance(new CardDefinition(id, id, side, 1, new[] { effect })
            {
                AllyTarget = range,
                EnemyTarget = range
            });

        // --- Strict self resolution ------------------------------------------------------------------

        [Test]
        public void Missing_owner_in_multi_party_self_effect_does_not_fall_back_to_front()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            state.Party.Add(a);
            state.Party.Add(b);

            var effect = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4);
            var card = Card("guard", Side.Player, effect, CardTargetRange.Self);
            // OwnerId intentionally left null.
            var result = EffectHarness.Apply(new ApplyStatusHandler(), state, card, effect);

            Assert.IsFalse(result.Applied);
            Assert.IsNull(card.CancellationReason, "대상 없음은 카드 취소가 아니다");
            Assert.IsFalse(a.Statuses.Has(StatusKeys.Block), "must not fall back to the front member");
            Assert.IsFalse(b.Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Enemy_self_without_owner_uses_the_only_enemy_for_legacy_runners()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Enemies.Add(new Enemy("goblin", 20));

            var effect = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Enemy, 3);
            var card = Card("crude_guard", Side.Enemy, effect, CardTargetRange.Self);
            var result = EffectHarness.Apply(new ApplyStatusHandler(), state, card, effect);

            Assert.IsTrue(result.Applied);
            Assert.IsTrue(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Enemy_self_without_owner_is_not_applied_when_multiple_enemies_exist()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Enemies.Add(new Enemy("a", 20));
            state.Enemies.Add(new Enemy("b", 20));

            var effect = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Enemy, 3);
            var card = Card("crude_guard", Side.Enemy, effect, CardTargetRange.Self);
            var result = EffectHarness.Apply(new ApplyStatusHandler(), state, card, effect);

            Assert.IsFalse(result.Applied);
            Assert.IsNull(card.CancellationReason, "대상 없음은 카드 취소가 아니다");
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            Assert.IsFalse(state.Enemies[1].Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Single_party_member_self_effect_resolves_without_an_owner_id()
        {
            var state = new CombatState(TestContent.Statuses());
            var hero = new PartyMember("hero", "Hero", maxHp: 10); // not the solo "player" id
            state.Party.Add(hero);
            state.Enemies.Add(new Enemy("goblin", 20));

            var effect = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 4);
            var card = Card("guard", Side.Player, effect, CardTargetRange.Self);
            // OwnerId left null: with only one party member, Self resolves unambiguously — symmetric
            // with Enemy_self_without_owner_uses_the_only_enemy_for_legacy_runners above.
            var result = EffectHarness.Apply(new ApplyStatusHandler(), state, card, effect);

            Assert.IsTrue(result.Applied);
            Assert.IsTrue(hero.Statuses.Has(StatusKeys.Block));
        }

        // --- AllPartyMembers independence + per-member damage folding ----------------------------

        [Test]
        public void All_party_status_creates_independent_instances()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 20);
            var b = new PartyMember("b", "B", maxHp: 20);
            state.Party.Add(a);
            state.Party.Add(b);
            state.Enemies.Add(new Enemy("goblin", 20));

            var applyEffect = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, 5);
            var applyCard = Card("guard_all", Side.Player, applyEffect, CardTargetRange.All);
            var applyResult = EffectHarness.Apply(new ApplyStatusHandler(), state, applyCard, applyEffect);

            Assert.IsTrue(a.Statuses.Has(StatusKeys.Block));
            Assert.IsTrue(b.Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(5, a.Statuses.Get(StatusKeys.Block).Magnitude);
            Assert.AreEqual(5, b.Statuses.Get(StatusKeys.Block).Magnitude);

            // Consume A's block via an enemy attack that targets the front (A); B's must remain untouched.
            var damageEffect = new EffectData(EffectKeys.Damage, 5) { TargetFaction = CardTargetFaction.Ally };
            var damageCard = Card("smash", Side.Enemy, damageEffect, CardTargetRange.FrontOne);
            var damageResult = EffectHarness.Apply(new DamageHandler(), state, damageCard, damageEffect, Statuses());

            Assert.AreEqual(20, a.Hp); // fully absorbed
            Assert.AreEqual(0, a.Statuses.Get(StatusKeys.Block).Magnitude); // A's charge spent
            Assert.AreEqual(5, b.Statuses.Get(StatusKeys.Block).Magnitude); // B's instance is independent
        }

        [Test]
        public void Block_and_vulnerable_on_a_do_not_modify_damage_to_b()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 20);
            var b = new PartyMember("b", "B", maxHp: 20);
            state.Party.Add(a);
            state.Party.Add(b);

            a.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            a.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, magnitude: 10);

            var damageEffect = new EffectData(EffectKeys.Damage, 4) { TargetFaction = CardTargetFaction.Ally };
            var card = Card("smash", Side.Enemy, damageEffect, CardTargetRange.BackOne);
            var result = EffectHarness.Apply(new DamageHandler(), state, card, damageEffect, Statuses());

            CollectionAssert.AreEqual(new[] { "b" }, result.TargetIds);
            Assert.AreEqual(4, result.DamageDealt); // unmodified by A's vulnerable/block
            Assert.AreEqual(16, b.Hp);
            Assert.AreEqual(20, a.Hp); // A untouched
            Assert.AreEqual(10, a.Statuses.Get(StatusKeys.Block).Magnitude); // A's block untouched
        }

        // --- Independent formations -----------------------------------------------------------

        [Test]
        public void Position_range_ignores_dead_members_without_reindexing_the_other_side()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var a = new PartyMember("a", "A", maxHp: 10);
            var b = new PartyMember("b", "B", maxHp: 10);
            var c = new PartyMember("c", "C", maxHp: 10);
            a.Hp = 0; // front member dead
            state.Party.Add(a);
            state.Party.Add(b);
            state.Party.Add(c);
            state.Enemies.Add(new Enemy("e1", 10));
            state.Enemies.Add(new Enemy("e2", 10));

            var frontOne = PartyTargeting.Select(state, CardTargetRange.FrontOne);
            Assert.AreEqual("b", frontOne.Id, "position selection skips the dead front member");

            // The player formation change above must have no bearing on enemy-formation indexing.
            var damageEffect = new EffectData(EffectKeys.Damage, 3) { TargetFaction = CardTargetFaction.Enemy };
            var card = Card("slash", Side.Player, damageEffect, CardTargetRange.BackOne);
            var result = EffectHarness.Apply(new DamageHandler(), state, card, damageEffect);

            Assert.AreEqual(10, state.Enemies[0].Hp, "e1 untouched");
            Assert.AreEqual(7, state.Enemies[1].Hp, "e2 hit at its original index");
            CollectionAssert.AreEqual(new[] { "e2" }, result.TargetIds);
        }
    }
}
