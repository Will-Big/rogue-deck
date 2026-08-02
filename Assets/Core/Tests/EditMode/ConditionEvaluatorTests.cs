using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class ConditionEvaluatorTests
    {
        private static ExecutionCardInstance Card(
            string id,
            Side side,
            int executionOrder,
            params EffectData[] effects)
        {
            var def = new CardDefinition(
                id, id, side, executionOrder, effects);
            return new ExecutionCardInstance(def);
        }

        private static ExecutionCardInstance Card(
            string id,
            Side side,
            int executionOrder,
            string targetId = null)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, 1) });
            return new ExecutionCardInstance(def) { TargetId = targetId };
        }

        private static EffectData Block()
            => EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.Self, 2);

        [Test]
        public void AdjacentCardHasEffect_matches_damage_in_a_composite_card_only()
        {
            var state = new CombatState();
            var subject = Card("subject", Side.Player, 1, Block());
            var hybrid = Card("hybrid", Side.Enemy, 2,
                new EffectData(EffectKeys.Damage, 3), Block());
            state.Zone.Add(subject);
            state.Zone.Add(hybrid);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(
                new AdjacentCardHasEffect(
                    AdjacentDirection.Next, Side.Enemy, EffectKeys.Damage),
                subject,
                ctx));
        }

        [Test]
        public void PreviousExecutedCardHasEffect_rejects_a_block_only_card()
        {
            var state = new CombatState();
            var blockOnly = Card("block", Side.Player, 1, Block());
            var subject = Card("subject", Side.Player, 2,
                new EffectData(EffectKeys.Damage, 1));
            state.Zone.Add(blockOnly);
            state.Zone.Add(subject);
            var ctx = ResolutionContext.From(state);
            ctx.MarkExecuted(blockOnly);

            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(
                new PreviousExecutedCardHasEffect(Side.Player, EffectKeys.Damage),
                subject,
                ctx));
        }

        [Test]
        public void BeforeNextEnemyDamageCard_ignores_an_earlier_block_only_enemy_card()
        {
            var state = new CombatState();
            var blockOnly = Card("block", Side.Enemy, 1, Block());
            var subject = Card("subject", Side.Player, 2,
                new EffectData(EffectKeys.Damage, 1));
            state.Zone.Add(blockOnly);
            state.Zone.Add(subject);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(
                new BeforeNextEnemyDamageCard(), subject, ctx));
        }

        [Test]
        public void FirstToTrigger_succeeds_only_for_first_resolving_card()
        {
            var state = new CombatState();
            var early = Card("early", Side.Player, 1);
            var late = Card("late", Side.Player, 2);
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
            var first = Card("first", Side.Player, 1);
            var second = Card("second", Side.Player, 2);
            var third = Card("third", Side.Player, 3);
            state.Zone.Add(third);
            state.Zone.Add(first);
            state.Zone.Add(second);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new WithinNth(2), first, ctx));
            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new WithinNth(2), second, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new WithinNth(2), third, ctx));
        }

        [Test]
        public void Side_only_conditions_match_the_requested_side()
        {
            var state = new CombatState();
            var setup = Card("setup", Side.Player, 1);
            var strike = Card("strike", Side.Player, 2);
            var enemy = Card("jab", Side.Enemy, 3);
            state.Zone.Add(strike);
            state.Zone.Add(enemy);
            state.Zone.Add(setup);
            var ctx = ResolutionContext.From(state);
            ctx.MarkExecuted(setup); // simulates setup having already resolved, ahead of strike

            Assert.AreEqual(
                ConditionTier.Success,
                ConditionEvaluator.Evaluate(
                    new PreviousExecutedCardIs(Side.Player),
                    strike,
                    ctx));
            Assert.AreEqual(
                ConditionTier.Basic,
                ConditionEvaluator.Evaluate(
                    new AdjacentCardIs(AdjacentDirection.Next, Side.Player),
                    strike,
                    ctx));
        }

        [Test]
        public void BeforeNextEnemyDamageCard_returns_basic_when_an_enemy_damage_card_already_resolved()
        {
            var state = new CombatState();
            var enemy = Card("jab", Side.Enemy, 1);
            var player = Card("counter", Side.Player, 2);
            state.Zone.Add(player);
            state.Zone.Add(enemy);
            var ctx = ResolutionContext.From(state);

            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new BeforeNextEnemyDamageCard(), player, ctx));
            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new BeforeNextEnemyDamageCard(), enemy, ctx));
        }

        [Test]
        public void SameTarget_succeeds_when_previous_player_card_targets_same_entity()
        {
            var state = new CombatState();
            var mark = Card("mark", Side.Player, 1, targetId: "goblin");
            var strike = Card("strike", Side.Player, 2, targetId: "goblin");
            var other = Card("other", Side.Player, 3, targetId: "slime");
            state.Zone.Add(other);
            state.Zone.Add(strike);
            state.Zone.Add(mark);
            var ctx = ResolutionContext.From(state);
            ctx.MarkExecuted(mark); // simulates mark having already resolved, ahead of strike/other

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(new SameTarget(), strike, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(new SameTarget(), other, ctx));
        }

        [Test]
        public void NoPrecedingCardOfSide_checks_all_earlier_cards()
        {
            var state = new CombatState();
            var enemyGuard = Card("enemy_guard", Side.Enemy, 1);
            var slyJab = Card("sly_jab", Side.Enemy, 2);
            var player = Card("slash", Side.Player, 3);
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
            var enemyGuard = Card("enemy_guard", Side.Enemy, 1);
            var smash = Card("warden_smash", Side.Enemy, 2);
            var player = Card("slash", Side.Player, 3);
            var enemyLate = Card("warden_swing", Side.Enemy, 4);
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
            var mark = Card("mark", Side.Player, 1);
            var chain = Card("chain", Side.Player, 2);
            state.Zone.Add(mark);
            state.Zone.Add(chain);
            var ctx = ResolutionContext.From(state);
            ctx.MarkExecuted(mark); // simulates mark having already resolved, ahead of chain

            var success = new AllOf(new Condition[]
            {
                new PreviousExecutedCardIs(Side.Player),
                new WithinNth(3)
            });
            var basic = new AllOf(new Condition[]
            {
                new AdjacentCardIs(AdjacentDirection.Next, Side.Player),
                new WithinNth(3)
            });

            Assert.AreEqual(ConditionTier.Success, ConditionEvaluator.Evaluate(success, chain, ctx));
            Assert.AreEqual(ConditionTier.Basic, ConditionEvaluator.Evaluate(basic, chain, ctx));
        }
    }
}
