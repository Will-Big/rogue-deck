using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class MultiTurnRunnerTests
    {
        private static ZoneCardSpec Strike(string id, int damage, int initiative = 1)
            => new ZoneCardSpec(id, id, Side.Player, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });

        private static ZoneCardSpec EnemyHit(string id, int damage, int initiative = 1)
            => new ZoneCardSpec(id, id, Side.Enemy, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });

        private static TurnScript Turn(params ZoneCardSpec[] cards)
            => new TurnScript(fateEnergy: 3, zoneCards: cards, fatePlays: new FatePlaySpec[0]);

        [Test]
        public void Enemy_and_player_hp_carry_across_turns()
        {
            var scenario = new MultiTurnScenario(
                "carry", playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 20) },
                turns: new[]
                {
                    Turn(Strike("s1", 5)),                            // enemy 20 -> 15
                    Turn(EnemyHit("e1", 3, 1), Strike("s2", 5, 2))    // player 30 -> 27, enemy 15 -> 10
                });

            var result = new MultiTurnRunner().Run(scenario);

            Assert.AreEqual(2, result.Turns.Count);
            Assert.AreEqual(10, result.FinalState.Enemies[0].Hp);
            Assert.AreEqual(27, result.FinalState.PlayerHp);
            Assert.AreEqual(Outcome.Ongoing, result.Outcome);
        }

        [Test]
        public void Each_turn_resolves_its_own_zone()
        {
            var scenario = new MultiTurnScenario(
                "zones", playerHp: 30,
                enemies: new[] { new EnemySpec("goblin", 50) },
                turns: new[] { Turn(Strike("alpha", 4)), Turn(Strike("beta", 4)) });

            var result = new MultiTurnRunner().Run(scenario);

            var turn0 = result.Turns[0].Timeline.OfType<CardResolved>().Select(e => e.CardId).ToArray();
            var turn1 = result.Turns[1].Timeline.OfType<CardResolved>().Select(e => e.CardId).ToArray();
            CollectionAssert.Contains(turn0, "alpha");
            CollectionAssert.DoesNotContain(turn0, "beta");
            CollectionAssert.Contains(turn1, "beta");
            CollectionAssert.DoesNotContain(turn1, "alpha");
        }

        [Test]
        public void Loop_stops_when_player_dies()
        {
            var scenario = new MultiTurnScenario(
                "lethal", playerHp: 3,
                enemies: new[] { new EnemySpec("goblin", 20) },
                turns: new[]
                {
                    Turn(EnemyHit("e1", 5)),  // player 3 -> dead => Lose
                    Turn(Strike("s2", 5))     // must NOT run
                });

            var result = new MultiTurnRunner().Run(scenario);

            Assert.AreEqual(1, result.Turns.Count);  // stopped after turn 0
            Assert.AreEqual(Outcome.Lose, result.Outcome);
        }
    }
}
