using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class ConditionalEffectResolutionTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            r.Register(new NullifyNextPlayerConditionRewardHandler());
            return r;
        }

        private static ExecutionCardInstance Card(
            string id,
            Side side,
            int executionOrder,
            EffectData effect)
        {
            var def = new CardDefinition(id, id, side, executionOrder, new[] { effect });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void Conditional_damage_uses_success_amount_when_condition_succeeds()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card(
                "quick_cut",
                Side.Player,
                1,
                EffectData.Conditional(EffectKeys.Damage, effectValue: 2, condition: new FirstToTrigger(), successEffectValue: 10)));

            var events = new TurnResolver(Registry()).Resolve(state, 0);
            var resolved = (CardResolved)events[1];

            Assert.AreEqual(2, state.Enemies[0].Hp);
            Assert.AreEqual(10, resolved.DamageDealt);
            Assert.AreEqual(ConditionTier.Success, resolved.ConditionTier);
        }

        [Test]
        public void Conditional_damage_uses_default_amount_when_condition_is_basic()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card("enemy_jab", Side.Enemy, 1, new EffectData(EffectKeys.Damage, 1)));
            state.Zone.Add(Card(
                "late_cut",
                Side.Player,
                2,
                EffectData.Conditional(EffectKeys.Damage, effectValue: 2, condition: new FirstToTrigger(), successEffectValue: 10)));

            var events = new TurnResolver(Registry()).Resolve(state, 0);
            var resolved = (CardResolved)events[2];

            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(2, resolved.DamageDealt);
            Assert.AreEqual(ConditionTier.Basic, resolved.ConditionTier);
        }

        [Test]
        public void Enemy_disruption_reward_nullified_is_consumed_after_downgrading_player()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            var enemy = Card("wrist_cut", Side.Enemy, 1,
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            var player = Card("quick_cut", Side.Player, 2,
                EffectData.Conditional(EffectKeys.Damage, effectValue: 2, condition: new WithinNth(2), successEffectValue: 10));
            state.Zone.Add(enemy);
            state.Zone.Add(player);

            new TurnResolver(Registry()).Resolve(state, 0);

            // one-shot: the disruption is consumed when it downgrades the player card's reward
            Assert.IsFalse(player.Statuses.Has(StatusKeys.RewardNullified));
        }

        [Test]
        public void Reward_nullified_card_uses_default_amount_even_when_condition_would_succeed()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card("wrist_cut", Side.Enemy, 1,
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)));
            state.Zone.Add(Card("quick_cut", Side.Player, 2,
                EffectData.Conditional(EffectKeys.Damage, effectValue: 2, condition: new WithinNth(2), successEffectValue: 10)));

            var events = new TurnResolver(Registry()).Resolve(state, 0);
            var resolved = (CardResolved)events[2];

            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(2, resolved.DamageDealt);
            Assert.AreEqual(ConditionTier.Basic, resolved.ConditionTier);
        }

        [Test]
        public void Mark_success_skips_block_only_card_and_bonuses_next_damage_card()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 20));
            var mark = new ExecutionCardInstance(new CardDefinition(
                "mark_target",
                "Mark Target",
                Side.Player,
                1,
                new[]
                {
                    new EffectData(EffectKeys.GrantNextPlayerDamageCardBonus, 6)
                }));
            var block = EffectData.ApplyStatus(StatusKeys.Block, StatusApplyTarget.TargetEnemy, 2);
            var blockOnly = new ExecutionCardInstance(new CardDefinition(
                "block_only",
                "Block Only",
                Side.Player,
                2,
                new[] { block }));
            var hybridEffects = new[]
            {
                new EffectData(EffectKeys.Damage, 1),
                block
            };
            var hybrid = new ExecutionCardInstance(new CardDefinition(
                "hybrid",
                "Hybrid",
                Side.Player,
                3,
                hybridEffects));
            state.Zone.Add(mark);
            state.Zone.Add(blockOnly);
            state.Zone.Add(hybrid);
            var registry = Registry();
            registry.Register(new ApplyStatusHandler());
            registry.Register(new GrantNextPlayerDamageCardBonusHandler());

            var events = new TurnResolver(registry).Resolve(state, 0);

            Assert.AreEqual(0, ((CardResolved)events[2]).DamageDealt);
            Assert.AreEqual(7, ((CardResolved)events[3]).DamageDealt);
            Assert.AreEqual(13, state.Enemies[0].Hp);
        }

        [Test]
        public void Pending_damage_bonus_applies_per_target_on_an_all_target_damage_card()
        {
            // Pinning test for the deliberate rule (DamageHandler.Apply, All branch): a pending
            // damage-card bonus raises the CARD's damage value, so with TargetSelector.All it applies
            // to every target independently, not as a one-time pool split across hits.
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("a", 20));
            state.Enemies.Add(new Enemy("b", 20));
            var mark = new ExecutionCardInstance(new CardDefinition(
                "mark_target",
                "Mark Target",
                Side.Player,
                1,
                new[]
                {
                    new EffectData(EffectKeys.GrantNextPlayerDamageCardBonus, 3)
                }));
            var sweep = new ExecutionCardInstance(new CardDefinition(
                "sweep",
                "Sweep",
                Side.Player,
                2,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 2) { TargetSelector = TargetSelector.All }
                }));
            state.Zone.Add(mark);
            state.Zone.Add(sweep);
            var registry = Registry();
            registry.Register(new GrantNextPlayerDamageCardBonusHandler());

            var events = new TurnResolver(registry).Resolve(state, 0);

            Assert.AreEqual(15, state.Enemies[0].Hp); // 20 - (2 + 3)
            Assert.AreEqual(15, state.Enemies[1].Hp); // 20 - (2 + 3), same bonus applied again
            var resolved = (CardResolved)events[2];
            Assert.AreEqual(10, resolved.DamageDealt); // 5 + 5
        }
    }
}
