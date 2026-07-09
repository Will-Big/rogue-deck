using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    /// <summary>Statuses applied via card content (ApplyStatus effect): Block absorbs damage, and a
    /// card-applied Vulnerable persists and expires across turns (exercising lifetime in scenarios).</summary>
    public class StatusContentTests
    {
        private static ZoneCardSpec Strike(string id, int initiative, int damage)
            => new ZoneCardSpec(id, id, Side.Player, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });

        private static ZoneCardSpec EnemyHit(string id, int initiative, int damage)
            => new ZoneCardSpec(id, id, Side.Enemy, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });

        private static ZoneCardSpec Guard(string id, int initiative, int block)
            => new ZoneCardSpec(id, id, Side.Player, CardType.Defense, initiative,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: block)
                });

        private static MultiTurnScenario OneTurn(int playerHp, EnemySpec[] enemies, params ZoneCardSpec[] cards)
            => new MultiTurnScenario("t", playerHp, enemies,
                new[] { new TurnScript(3, cards, new InterventionPlaySpec[0]) });

        [Test]
        public void Block_absorbs_incoming_damage()
        {
            var result = new MultiTurnRunner().Run(OneTurn(
                30, new[] { new EnemySpec("goblin", 30) },
                Guard("guard", 1, block: 5),
                EnemyHit("goblin_hit", 2, 4)));

            Assert.AreEqual(30, result.FinalState.PlayerHp); // 5 block absorbs the whole 4 hit
        }

        [Test]
        public void Block_partially_absorbs_and_overflow_gets_through()
        {
            var result = new MultiTurnRunner().Run(OneTurn(
                30, new[] { new EnemySpec("goblin", 30) },
                Guard("guard", 1, block: 5),
                EnemyHit("goblin_hit", 2, 7)));

            Assert.AreEqual(28, result.FinalState.PlayerHp); // 5 absorbed, 2 through
        }

        [Test]
        public void Card_applied_vulnerable_persists_then_expires_across_turns()
        {
            var expose = new[]
            {
                EffectData.ApplyStatus(
                    StatusKeys.Vulnerable, StatusLifetime.Turns(2), StatusApplyTarget.TargetEnemy)
            };

            var scenario = new MultiTurnScenario(
                "vuln-cross", 30,
                new[] { new EnemySpec("goblin", 100) },
                new[]
                {
                    new TurnScript(3, new[]
                    {
                        new ZoneCardSpec("expose", "Expose", Side.Player, CardType.Skill, 1, expose),
                        Strike("strike1", 2, 4)
                    }, new InterventionPlaySpec[0]),
                    new TurnScript(3, new[] { Strike("strike2", 1, 4) }, new InterventionPlaySpec[0]),
                    new TurnScript(3, new[] { Strike("strike3", 1, 4) }, new InterventionPlaySpec[0])
                });

            var result = new MultiTurnRunner().Run(scenario);
            int Dmg(int turn, string id) =>
                result.Turns[turn].Timeline.OfType<CardResolved>().Single(e => e.CardId == id).DamageDealt;

            Assert.AreEqual(6, Dmg(0, "strike1")); // vulnerable active (4 -> 6)
            Assert.AreEqual(6, Dmg(1, "strike2")); // count 2 -> 1, still active
            Assert.AreEqual(4, Dmg(2, "strike3")); // expired after turn 2
        }
    }
}
