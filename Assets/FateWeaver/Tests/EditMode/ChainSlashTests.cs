using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    /// <summary>연쇄 베기 (doc §11.3): "activate once more" modelled as a conditional second hit, gated by
    /// AllOf(previous card is a player action card, within the 3rd slot) so it does not auto-complete.</summary>
    public class ChainSlashTests
    {
        private static CardResolved Resolved(MultiTurnResult result, string id)
            => result.Turns.SelectMany(t => t.Timeline).OfType<CardResolved>().Single(e => e.CardId == id);

        [Test]
        public void Chain_re_triggers_after_a_player_action_card_within_3rd()
        {
            // prep is a player SKILL (not an attack) -> exercises the "any player action card" adjacency.
            var result = new MultiTurnRunner().Run(SampleMultiTurnScenarios.ChainSlash());

            Assert.AreEqual(6, Resolved(result, "chain").DamageDealt); // 1 + (1+4)
            Assert.AreEqual(93, result.FinalState.Enemies[0].Hp);      // 100 - 1 (prep) - 6 (chain)
        }

        [Test]
        public void Chain_does_not_re_trigger_after_an_enemy_card()
        {
            var enemy = new ZoneCardSpec("enemy_jab", "enemy_jab", Side.Enemy, CardType.Attack, 1,
                new[] { new EffectData(EffectKeys.Damage, 3) });
            var chain = new ZoneCardSpec("chain", "Chain Slash", Side.Player, CardType.Attack, 2,
                new[]
                {
                    new EffectData(EffectKeys.Damage, 1),
                    EffectData.Conditional(EffectKeys.Damage, 0,
                        new AllOf(new Condition[]
                        {
                            new AdjacentCardIs(AdjacentDirection.Previous, Side.Player),
                            new WithinNth(3)
                        }), 5)
                });

            var scenario = new MultiTurnScenario("chain-no-trigger", 30,
                new[] { new EnemySpec("goblin", 100) },
                new[] { new TurnScript(3, new[] { enemy, chain }, new FatePlaySpec[0]) });

            var result = new MultiTurnRunner().Run(scenario);

            Assert.AreEqual(1, Resolved(result, "chain").DamageDealt); // prev is an enemy card -> no second hit
            Assert.AreEqual(99, result.FinalState.Enemies[0].Hp);      // only the base 1
        }
    }
}
