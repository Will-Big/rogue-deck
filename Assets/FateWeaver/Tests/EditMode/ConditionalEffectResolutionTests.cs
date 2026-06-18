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

        private static ActionCardInstance Card(
            string id,
            Side side,
            int initiative,
            EffectData effect)
        {
            var def = new CardDefinition(id, id, side, CardType.Attack, initiative, new[] { effect });
            return new ActionCardInstance(def);
        }

        [Test]
        public void Conditional_damage_uses_success_amount_when_condition_succeeds()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card(
                "quick_cut",
                Side.Player,
                1,
                EffectData.Conditional(EffectKeys.Damage, amount: 2, condition: new FirstToTrigger(), successAmount: 10)));

            var events = new TurnResolver(Registry()).Resolve(state, 0);
            var resolved = (CardResolved)events[1];

            Assert.AreEqual(2, state.Enemies[0].Hp);
            Assert.AreEqual(10, resolved.DamageDealt);
            Assert.AreEqual(ConditionTier.Success, resolved.ConditionTier);
        }

        [Test]
        public void Conditional_damage_uses_default_amount_when_condition_is_basic()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card("enemy_jab", Side.Enemy, 1, new EffectData(EffectKeys.Damage, 1)));
            state.Zone.Add(Card(
                "late_cut",
                Side.Player,
                2,
                EffectData.Conditional(EffectKeys.Damage, amount: 2, condition: new FirstToTrigger(), successAmount: 10)));

            var events = new TurnResolver(Registry()).Resolve(state, 0);
            var resolved = (CardResolved)events[2];

            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(2, resolved.DamageDealt);
            Assert.AreEqual(ConditionTier.Basic, resolved.ConditionTier);
        }

        [Test]
        public void Enemy_disruption_marks_next_player_card_reward_nullified()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            var enemy = Card("wrist_cut", Side.Enemy, 1,
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0));
            var player = Card("quick_cut", Side.Player, 2,
                EffectData.Conditional(EffectKeys.Damage, amount: 2, condition: new WithinNth(2), successAmount: 10));
            state.Zone.Add(enemy);
            state.Zone.Add(player);

            new TurnResolver(Registry()).Resolve(state, 0);

            Assert.IsTrue(player.Statuses.Has(StatusKeys.RewardNullified));
        }

        [Test]
        public void Reward_nullified_card_uses_default_amount_even_when_condition_would_succeed()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            state.Zone.Add(Card("wrist_cut", Side.Enemy, 1,
                new EffectData(EffectKeys.NullifyNextPlayerConditionReward, 0)));
            state.Zone.Add(Card("quick_cut", Side.Player, 2,
                EffectData.Conditional(EffectKeys.Damage, amount: 2, condition: new WithinNth(2), successAmount: 10)));

            var events = new TurnResolver(Registry()).Resolve(state, 0);
            var resolved = (CardResolved)events[2];

            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(2, resolved.DamageDealt);
            Assert.AreEqual(ConditionTier.Basic, resolved.ConditionTier);
        }
    }
}
