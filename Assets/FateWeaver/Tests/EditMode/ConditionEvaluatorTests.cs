using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class ConditionEvaluatorTests
    {
        private static ExecutionCardInstance Card(
            string id,
            Side side,
            CardType type,
            int initiative,
            string targetId = null)
        {
            var def = new CardDefinition(id, id, side, type, initiative,
                new[] { new EffectData(EffectKeys.Damage, 1) });
            return new ExecutionCardInstance(def) { TargetId = targetId };
        }

        [Test]
        public void FirstToTrigger_succeeds_only_for_first_resolving_card()
        {
            var state = new CombatState();
            var early = Card("early", Side.Player, CardType.Attack, 1);
            var late = Card("late", Side.Player, CardType.Attack, 2);
            state.Zone.Add(late);
            state.Zone.Add(early);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new FirstToTrigger(), early, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new FirstToTrigger(), late, ctx));
        }

        [Test]
        public void WithinNth_succeeds_for_cards_before_the_nth_slot()
        {
            var state = new CombatState();
            var first = Card("first", Side.Player, CardType.Attack, 1);
            var second = Card("second", Side.Player, CardType.Attack, 2);
            var third = Card("third", Side.Player, CardType.Attack, 3);
            state.Zone.Add(third);
            state.Zone.Add(first);
            state.Zone.Add(second);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new WithinNth(2), first, ctx));
            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new WithinNth(2), second, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new WithinNth(2), third, ctx));
        }

        [Test]
        public void AdjacentCardIs_matches_direction_side_and_type()
        {
            var state = new CombatState();
            var setup = Card("setup", Side.Player, CardType.Skill, 1);
            var strike = Card("strike", Side.Player, CardType.Attack, 2);
            var enemy = Card("jab", Side.Enemy, CardType.Attack, 3);
            state.Zone.Add(strike);
            state.Zone.Add(enemy);
            state.Zone.Add(setup);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(
                ConditionTier.Success,
                ConditionEvaluator.Evaluate(
                    new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, CardType.Skill),
                    strike,
                    ctx));
            Assert.AreEqual(
                ConditionTier.Basic,
                ConditionEvaluator.Evaluate(
                    new AdjacentCardIs(AdjacentDirection.Next, Side.Player, CardType.Skill),
                    strike,
                    ctx));
        }

        [Test]
        public void BeforeNextEnemyAttack_returns_basic_when_an_enemy_attack_already_resolved()
        {
            var state = new CombatState();
            var enemy = Card("jab", Side.Enemy, CardType.Attack, 1);
            var player = Card("counter", Side.Player, CardType.Attack, 2);
            state.Zone.Add(player);
            state.Zone.Add(enemy);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new BeforeNextEnemyAttack(), player, ctx));
            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new BeforeNextEnemyAttack(), enemy, ctx));
        }

        [Test]
        public void SameTarget_succeeds_when_previous_player_card_targets_same_entity()
        {
            var state = new CombatState();
            var mark = Card("mark", Side.Player, CardType.Skill, 1, targetId: "goblin");
            var strike = Card("strike", Side.Player, CardType.Attack, 2, targetId: "goblin");
            var other = Card("other", Side.Player, CardType.Attack, 3, targetId: "slime");
            state.Zone.Add(other);
            state.Zone.Add(strike);
            state.Zone.Add(mark);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new SameTarget(), strike, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new SameTarget(), other, ctx));
        }

        [Test]
        public void NoPrecedingCardOfSide_checks_all_earlier_cards()
        {
            var state = new CombatState();
            var enemyGuard = Card("enemy_guard", Side.Enemy, CardType.Defense, 1);
            var slyJab = Card("sly_jab", Side.Enemy, CardType.Attack, 2);
            var player = Card("slash", Side.Player, CardType.Attack, 3);
            state.Zone.Add(player);
            state.Zone.Add(slyJab);
            state.Zone.Add(enemyGuard);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(
                ConditionTier.Success,
                ConditionEvaluator.Evaluate(new NoPrecedingCardOfSide(Side.Player), slyJab, ctx));
            Assert.AreEqual(
                ConditionTier.Basic,
                ConditionEvaluator.Evaluate(new NoPrecedingCardOfSide(Side.Enemy), slyJab, ctx));
        }

        [Test]
        public void NoFollowingCardOfSide_checks_all_later_cards()
        {
            var state = new CombatState();
            var enemyGuard = Card("enemy_guard", Side.Enemy, CardType.Defense, 1);
            var smash = Card("warden_smash", Side.Enemy, CardType.Attack, 2);
            var player = Card("slash", Side.Player, CardType.Attack, 3);
            var enemyLate = Card("warden_swing", Side.Enemy, CardType.Attack, 4);
            state.Zone.Add(enemyLate);
            state.Zone.Add(player);
            state.Zone.Add(smash);
            state.Zone.Add(enemyGuard);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(
                ConditionTier.Basic,
                ConditionEvaluator.Evaluate(new NoFollowingCardOfSide(Side.Player), smash, ctx));
            Assert.AreEqual(
                ConditionTier.Basic,
                ConditionEvaluator.Evaluate(new NoFollowingCardOfSide(Side.Enemy), smash, ctx));
            Assert.AreEqual(
                ConditionTier.Success,
                ConditionEvaluator.Evaluate(new NoFollowingCardOfSide(Side.Enemy), enemyLate, ctx));
        }

        [Test]
        public void AllOf_uses_the_lowest_tier_from_its_child_conditions()
        {
            var state = new CombatState();
            var mark = Card("mark", Side.Player, CardType.Skill, 1);
            var chain = Card("chain", Side.Player, CardType.Attack, 2);
            state.Zone.Add(mark);
            state.Zone.Add(chain);
            var ctx = ResolutionContext.From(state);

            var success = new AllOf(new Condition[]
            {
                new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, CardType.Skill),
                new WithinNth(3)
            });
            var basic = new AllOf(new Condition[]
            {
                new AdjacentCardIs(AdjacentDirection.Next, Side.Player, CardType.Skill),
                new WithinNth(3)
            });

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(success, chain, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(basic, chain, ctx));
        }
    }
}
