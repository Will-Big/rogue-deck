using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class DamageHandlerTests
    {
        private static ExecutionCardInstance Card(Side side, int amount)
        {
            var def = new CardDefinition("c", "c", side, 1,
                new[] { new EffectData(EffectKeys.Damage, amount) });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void Player_damage_hits_first_enemy()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            var ctx = new EffectContext { Card = Card(Side.Player, 5), State = state, EffectValue = 5 };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual(7, state.Enemies[0].Hp);
            Assert.AreEqual(5, ctx.DamageDealt);
            Assert.AreEqual("goblin", ctx.TargetId);
        }

        [Test]
        public void Player_damage_honors_card_target_id()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("a", 10));
            state.Enemies.Add(new Enemy("b", 10));
            var card = Card(Side.Player, 4);
            card.TargetId = "b";
            var ctx = new EffectContext { Card = card, State = state, EffectValue = 4 };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual(10, state.Enemies[0].Hp); // "a" untouched
            Assert.AreEqual(6, state.Enemies[1].Hp);  // "b" hit
            Assert.AreEqual("b", ctx.TargetId);
        }

        [Test]
        public void Enemy_damage_hits_player()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            var ctx = new EffectContext { Card = Card(Side.Enemy, 4), State = state, EffectValue = 4 };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual(26, state.PlayerHp);
            Assert.AreEqual(4, ctx.DamageDealt);
            Assert.AreEqual("player", ctx.TargetId);
        }

        [Test]
        public void Registry_resolves_handler_by_key_and_throws_on_unknown()
        {
            var registry = new EffectRegistry();
            var handler = new DamageHandler();
            registry.Register(handler);

            Assert.AreSame(handler, registry.Resolve(EffectKeys.Damage));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => registry.Resolve(new EffectKey("nope")));
        }
    }
}
