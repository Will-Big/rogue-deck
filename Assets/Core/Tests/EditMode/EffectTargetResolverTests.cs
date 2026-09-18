using System;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>효과마다 그 시작 시점의 위치 규칙으로 대상을 고른다(전투 실행 계약 스펙 §2·§4, 계획 T3).</summary>
    public class EffectTargetResolverTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());
            effects.Register(new MoveFormationHandler());
            effects.Register(new GrantNextTurnFateHandler());
            return effects;
        }

        private static ExecutionCardInstance Card(Side side, params EffectData[] effects)
            => new ExecutionCardInstance(new CardDefinition("card", "card", side, 1, effects));

        private static ExecutionCardInstance Card(
            CardTargetRange? ally, CardTargetRange? enemy, params EffectData[] effects)
            => new ExecutionCardInstance(new CardDefinition("card", "card", Side.Player, 1, effects)
            {
                AllyTarget = ally,
                EnemyTarget = enemy
            })
            {
                OwnerId = CombatState.SoloPlayerId
            };

        private static EffectData Hit(string id, int value)
            => new EffectData(EffectKeys.Damage, value) { Id = id, TargetFaction = CardTargetFaction.Enemy };

        [Test]
        public void A_new_effect_selects_the_new_front_enemy()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 3);
            var b = new Enemy("b", 10);
            state.Enemies.Add(a);
            state.Enemies.Add(b);
            var card = new ExecutionCardInstance(new CardDefinition(
                "hit", "hit", Side.Player, 1, Array.Empty<EffectData>()));
            var key = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne);
            var resolver = new EffectTargetResolver();
            Assert.AreSame(a, resolver.Resolve(state, card, key).EnemyTargets(key).Single());
            a.Hp = 0;
            Assert.AreSame(b, resolver.Resolve(state, card, key).EnemyTargets(key).Single());
        }

        [Test]
        public void Resolve_keeps_the_captured_list_when_the_formation_later_changes()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var front = new Enemy("front", 10);
            var back = new Enemy("back", 10);
            state.Enemies.Add(front);
            state.Enemies.Add(back);
            var key = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.All);

            var targets = new EffectTargetResolver().Resolve(state, Card(Side.Player), key);
            state.Enemies.Reverse();

            CollectionAssert.AreEqual(new[] { front, back }, targets.EnemyTargets(key));
        }

        [Test]
        public void Ownerless_self_resolves_the_only_living_ally()
        {
            var state = new CombatState(TestContent.Statuses());
            var only = state.AddSoloPlayer(10);
            var key = new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self);

            var targets = new EffectTargetResolver().Resolve(state, Card(Side.Player), key);

            CollectionAssert.AreEqual(new[] { only }, targets.PartyTargets(key));
        }

        [Test]
        public void Ownerless_self_among_several_allies_has_no_target_and_does_not_cancel()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Add(new PartyMember("a", "A", 10));
            state.Party.Add(new PartyMember("b", "B", 10));
            var card = Card(Side.Player);
            var key = new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self);

            var targets = new EffectTargetResolver().Resolve(state, card, key);

            CollectionAssert.IsEmpty(targets.PartyTargets(key));
            Assert.IsNull(card.CancellationReason);
        }

        [Test]
        public void A_different_key_reads_as_no_targets()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("a", 10));
            var front = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne);
            var back = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.BackOne);

            var targets = new EffectTargetResolver().Resolve(state, Card(Side.Player), front);

            CollectionAssert.IsEmpty(targets.EnemyTargets(back));
        }

        // V05: 첫 타격으로 전방 적이 죽으면 둘째 타격은 새 전방 적을 친다.
        [Test]
        public void Second_hit_selects_the_new_front_after_the_first_hit_kills()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 3);
            var b = new Enemy("b", 10);
            state.Enemies.Add(a);
            state.Enemies.Add(b);
            state.Zone.Add(Card(null, CardTargetRange.FrontOne, Hit("e0", 3), Hit("e1", 3)));

            new TurnResolver(Effects(), new StatusRegistry()).Resolve(state, 0);

            Assert.AreEqual(0, a.Hp);
            Assert.AreEqual(7, b.Hp);
        }

        // V06: 이동 다음의 FrontOne 방어는 이동 후 전방 아군에게 간다.
        [Test]
        public void Block_after_a_move_goes_to_the_member_now_in_front()
        {
            var state = new CombatState(TestContent.Statuses());
            state.Party.Clear();
            var a = new PartyMember("a", "A", 10);
            var b = new PartyMember("b", "B", 10);
            state.Party.Add(a);
            state.Party.Add(b);
            state.Enemies.Add(new Enemy("enemy", 10));
            var move = new EffectData(EffectKeys.MoveFormation, -1) { Id = "move" };
            var block = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, count: 3) with { Id = "block" };
            // 카드의 아군 축은 하나다. 이동은 아군 Self, 방어는 아군 FrontOne이라 한 카드에 둘 수 없다(로딩이 거부하는
            // 조합) — 이동 카드와 방어 카드로 나눠 같은 주인이 차례로 쓴다. 방어 카드는 차례가 왔을 때의 대형으로 고른다.
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition(
                "step", "step", Side.Player, 1,
                new[] { move with { TargetFaction = CardTargetFaction.Ally } })
            {
                AllyTarget = CardTargetRange.Self
            })
            {
                OwnerId = "b"
            });
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition(
                "guard", "guard", Side.Player, 2, new[] { block })
            {
                AllyTarget = CardTargetRange.FrontOne
            })
            {
                OwnerId = "b"
            });

            var events = new TurnResolver(Effects(), new StatusRegistry()).Resolve(state, 0);

            // 방어는 이번 턴 상태라 턴 끝에 만료되므로 부여 사건으로 확인한다.
            Assert.AreSame(b, state.Party[0]);
            CollectionAssert.AreEqual(
                new[] { "b" },
                events.OfType<StatusApplied>().Select(e => e.HolderId).ToArray());
        }

        [Test]
        public void An_effect_without_targets_is_not_applied_and_the_card_still_resolves()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var only = new Enemy("only", 2);
            state.Enemies.Add(only);
            var fate = new EffectData(EffectKeys.GrantNextTurnFate, 1) { Id = "fate" };
            state.Zone.Add(Card(null, CardTargetRange.FrontOne, Hit("e0", 2), Hit("e1", 2), fate));

            var events = new TurnResolver(Effects(), new StatusRegistry()).Resolve(state, 0);

            Assert.AreEqual(0, only.Hp);
            Assert.AreEqual(1, state.PendingNextTurnFateEnergy, "대상 없는 효과 뒤의 효과도 수행된다");
            Assert.IsEmpty(events.OfType<CardCancelled>());
            var resolved = events.OfType<CardResolved>().Single();
            Assert.AreEqual(2, resolved.DamageDealt);
        }

        [Test]
        public void Executor_reports_an_effect_without_targets_as_not_applied()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var hit = Hit("hit", 2);
            var card = Card(null, CardTargetRange.FrontOne, hit);
            var context = new CardExecutionContext(
                card, ConditionTier.Basic, state, ResolutionContext.From(state));

            var result = new EffectExecutor(Effects(), new StatusRegistry()).Apply(context, hit);

            Assert.IsFalse(result.Applied);
            Assert.IsNull(card.CancellationReason);
            CollectionAssert.IsEmpty(result.TargetIds);
        }

        [Test]
        public void All_applies_to_the_list_captured_at_the_start_of_the_effect()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var a = new Enemy("a", 2);
            var b = new Enemy("b", 5);
            var c = new Enemy("c", 5);
            state.Enemies.Add(a);
            state.Enemies.Add(b);
            state.Enemies.Add(c);
            var sweep = Hit("sweep", 2);
            var card = Card(null, CardTargetRange.All, sweep);
            var context = new CardExecutionContext(
                card, ConditionTier.Basic, state, ResolutionContext.From(state));

            var result = new EffectExecutor(Effects(), new StatusRegistry()).Apply(context, sweep);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(6, result.DamageDealt);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result.TargetIds);
            Assert.AreEqual(new[] { 0, 3, 3 }, state.Enemies.Select(e => e.Hp).ToArray());
        }

        [Test]
        public void Card_resolved_target_is_the_first_target_of_the_first_applied_effect()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("a", 10));
            state.Enemies.Add(new Enemy("b", 10));
            var fate = new EffectData(EffectKeys.GrantNextTurnFate, 1) { Id = "fate" };
            var guard = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, count: 1) with { Id = "guard" };
            state.Zone.Add(Card(CardTargetRange.Self, CardTargetRange.All, fate, Hit("sweep", 1), guard));

            var events = new TurnResolver(Effects(), new StatusRegistry()).Resolve(state, 0);

            Assert.AreEqual("a", events.OfType<CardResolved>().Single().TargetId);
        }

        [Test]
        public void Card_resolved_target_is_null_when_no_effect_had_a_target()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            var fate = new EffectData(EffectKeys.GrantNextTurnFate, 1) { Id = "fate" };
            state.Zone.Add(new ExecutionCardInstance(new CardDefinition(
                "fate_only", "fate_only", Side.Player, 1, new[] { fate }))
            {
                OwnerId = CombatState.SoloPlayerId
            });

            var events = new TurnResolver(Effects(), new StatusRegistry()).Resolve(state, 0);

            Assert.IsNull(events.OfType<CardResolved>().Single().TargetId);
        }

        [Test]
        public void Target_key_is_the_effect_faction_with_the_card_range_on_that_side()
        {
            var hit = Hit("hit", 2);
            var guard = EffectData.ApplyStatus(StatusKeys.Block, CardTargetFaction.Ally, count: 1);
            var fate = new EffectData(EffectKeys.GrantNextTurnFate, 1);
            var card = new CardDefinition("c", "c", Side.Player, 1, new[] { hit, guard, fate })
            {
                AllyTarget = CardTargetRange.FrontTwo,
                EnemyTarget = CardTargetRange.BackOne
            };

            Assert.AreEqual(new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.BackOne), card.TargetOf(hit));
            Assert.AreEqual(new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.FrontTwo), card.TargetOf(guard));
            Assert.IsNull(card.TargetOf(fate), "진영이 없는 효과는 대상을 고르지 않는다");
        }

        [Test]
        public void A_faction_without_a_card_range_is_a_contract_violation()
        {
            var hit = Hit("hit", 2);
            var card = new CardDefinition("c", "c", Side.Player, 1, new[] { hit }) { AllyTarget = CardTargetRange.Self };

            Assert.Throws<InvalidOperationException>(() => card.TargetOf(hit));
        }

        [Test]
        public void A_targeted_handler_without_a_faction_reports_the_missing_faction()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(10);
            state.Enemies.Add(new Enemy("a", 10));
            var unaimed = new EffectData(EffectKeys.Damage, 2);

            var error = Assert.Throws<InvalidOperationException>(
                () => EffectHarness.Apply(new DamageHandler(), state, Card(Side.Player, unaimed)));
            StringAssert.Contains("TargetFaction", error.Message);
        }
    }
}
