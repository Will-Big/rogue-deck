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
                new[] { new EffectData(EffectKeys.Damage, amount) { TargetFaction = CardFixtures.Opposing(side) } })
            {
                AllyTarget = CardTargetRange.FrontOne,
                EnemyTarget = CardTargetRange.FrontOne
            };
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void Player_damage_hits_first_enemy()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            var result = EffectHarness.Apply(new DamageHandler(), state, Card(Side.Player, 5));

            Assert.AreEqual(7, state.Enemies[0].Hp);
            Assert.AreEqual(5, result.DamageDealt);
            CollectionAssert.AreEqual(new[] { "goblin" }, result.TargetIds);
        }

        [Test]
        public void Enemy_damage_hits_player()
        {
            var state = new CombatState(TestContent.Statuses());
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 12));
            var result = EffectHarness.Apply(new DamageHandler(), state, Card(Side.Enemy, 4));

            Assert.AreEqual(26, state.Party[0].Hp);
            Assert.AreEqual(4, result.DamageDealt);
            CollectionAssert.AreEqual(new[] { "player" }, result.TargetIds);
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
