using NUnit.Framework;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class StatusTests
    {
        /// <summary>카드 해결을 무조건 무효화하는 카드 범위(CardInstance) 테스트 전용 상태 — 프로덕션의
        /// stun을 대신한다(Task 5에서 제거). 이 상태 자체의 의미는 테스트와 무관하고, CardInstance
        /// 범위 가로채기 배선만 검증하면 된다.</summary>
        private sealed class NullifyingBehavior : StatusBehavior
        {
            public static readonly StatusKey TestKey = new StatusKey("test_nullify");
            public override StatusKey Key => TestKey;
            public override StatusScope Scope => StatusScope.CardInstance;
            public override bool InterceptCardResolve(StatusContext ctx) => true;
        }

        private static EffectRegistry Effects()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new NullifyingBehavior());
            r.Register(new VulnerableBehavior());
            r.Register(new RewardSuppressionBehavior());
            r.Register(new BlockBehavior());
            return r;
        }

        private static ExecutionCardInstance Card(string id, Side side, int executionOrder, int damage)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void StatusBag_add_refresh_remove()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            Assert.IsTrue(bag.Has(StatusKeys.Vulnerable));
            Assert.AreEqual(2, bag.Get(StatusKeys.Vulnerable).Count);

            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(5)); // re-apply refreshes
            Assert.AreEqual(5, bag.Get(StatusKeys.Vulnerable).Count);

            Assert.IsTrue(bag.Remove(StatusKeys.Vulnerable));
            Assert.IsFalse(bag.Has(StatusKeys.Vulnerable));
        }

        [Test]
        public void EndOfTurn_drops_thisturn_ticks_turns_keeps_permanent()
        {
            var bag = new StatusBag();
            bag.Add(NullifyingBehavior.TestKey, StatusLifetime.ThisTurn);
            bag.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            bag.Add(StatusKeys.RewardNullified, StatusLifetime.Permanent);

            bag.EndOfTurn();
            Assert.IsFalse(bag.Has(NullifyingBehavior.TestKey));          // ThisTurn dropped
            Assert.AreEqual(1, bag.Get(StatusKeys.Vulnerable).Count);  // 2 -> 1
            Assert.IsTrue(bag.Has(StatusKeys.RewardNullified));        // permanent kept

            bag.EndOfTurn();
            Assert.IsFalse(bag.Has(StatusKeys.Vulnerable));            // 1 -> 0, removed
        }

        [Test]
        public void Vulnerable_turns_based_modifies_hit_without_being_consumed()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4)); // 4 -> 6

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(24, enemy.Hp);
            // not consumed by the hit; ticked down once by end-of-turn (2 -> 1)
            Assert.AreEqual(1, enemy.Statuses.Get(StatusKeys.Vulnerable).Count);
        }

        [Test]
        public void Vulnerable_until_consumed_applies_once_then_is_gone()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.UntilConsumed(1));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike1", Side.Player, 1, 4)); // 6 (consumes the charge)
            state.Zone.Add(Card("strike2", Side.Player, 2, 4)); // 4 (vulnerable gone)

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(4, ((CardResolved)events[2]).DamageDealt);
            Assert.IsFalse(enemy.Statuses.Has(StatusKeys.Vulnerable));
        }

        [Test]
        public void CardInstance_status_until_consumed_nullifies_one_resolution_then_is_gone()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var card = Card("strike", Side.Player, 1, 5);
            card.Statuses.Add(NullifyingBehavior.TestKey, StatusLifetime.UntilConsumed(1));
            state.Zone.Add(card);

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            var cancelled = (CardCancelled)events[1];
            Assert.AreEqual(CardCancellationReason.StatusIntercepted, cancelled.Reason);
            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.IsFalse(card.Statuses.Has(NullifyingBehavior.TestKey));
        }

        [Test]
        public void Without_status_registry_incoming_damage_is_unmodified()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 20);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(1));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4));

            var events = new TurnResolver(Effects()).Resolve(state, 0); // no StatusRegistry
            Assert.AreEqual(4, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(16, enemy.Hp);
        }

        [Test]
        public void Vulnerable_multiplies_before_block_absorbs_when_block_applied_first()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, 5);   // 방어가 먼저
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 10));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 10 x 1.5 = 15, 그 다음 방어 5가 흡수 -> 10
            Assert.AreEqual(10, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(20, enemy.Hp);
            Assert.IsFalse(enemy.Statuses.Has(StatusKeys.Block)); // ThisTurn 방어는 턴 끝에 사라진다
        }

        [Test]
        public void Vulnerable_and_block_result_is_independent_of_apply_order()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2)); // 취약이 먼저
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, 5);
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 10));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(10, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(20, enemy.Hp);
        }

        [Test]
        public void Vulnerable_multiplier_comes_from_the_combat_status_rules()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.StatusRules.Set(StatusKeys.Vulnerable, new StatusRule { MultiplierPercent = 200 });
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(8, ((CardResolved)events[1]).DamageDealt); // 4 x 2.00
        }

        [Test]
        public void Vulnerable_multiplier_defaults_to_one_hundred_fifty_percent()
        {
            var rules = TestContent.Statuses().Rules;
            Assert.AreEqual(150, rules.For(StatusKeys.Vulnerable).MultiplierPercent);
        }

        [Test]
        public void Unregistered_status_rule_is_a_neutral_multiplier()
        {
            var rules = TestContent.Statuses().Rules;
            Assert.AreEqual(100, rules.For(new StatusKey("no_such_status")).MultiplierPercent);
        }

        [Test]
        public void Vulnerable_multiplier_floors_odd_damage()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 5));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(7, ((CardResolved)events[1]).DamageDealt); // floor(5 x 1.5) = 7
        }
    }
}
