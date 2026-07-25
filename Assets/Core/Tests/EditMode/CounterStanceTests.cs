using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    /// <summary>반격 자세 (doc §11.4 후보 A / §2.3 후행 보상): a defensive card that counters only when an
    /// enemy attack resolved immediately before it. Single-enemy targeting for now.</summary>
    public class CounterStanceTests
    {
        private static CardResolved Resolved(MultiTurnResult result, string id)
            => result.Turns.SelectMany(t => t.Timeline).OfType<CardResolved>().Single(e => e.CardId == id);

        [Test]
        public void Counter_fires_after_a_preceding_enemy_attack()
        {
            var result = new MultiTurnRunner().Run(SampleMultiTurnScenarios.CounterStance());

            Assert.AreEqual(9, Resolved(result, "counter").DamageDealt); // 7 + 2 (within 3rd)
            Assert.AreEqual(91, result.FinalState.Enemies[0].Hp);        // 100 - 9
            Assert.AreEqual(27, result.FinalState.Party[0].Hp);          // took the 3 hit
        }

        [Test]
        public void Counter_does_nothing_without_a_preceding_enemy_attack()
        {
            var counter = new ZoneCardSpec("counter", "Counter Stance", Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, magnitude: 2),
                    EffectData.Conditional(EffectKeys.Damage, 0,
                        new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage), 7),
                    EffectData.Conditional(EffectKeys.Damage, 0,
                        new AllOf(new Condition[]
                        {
                            new PreviousExecutedCardHasEffect(Side.Enemy, EffectKeys.Damage),
                            new WithinNth(3)
                        }), 2)
                });
            var enemy = new ZoneCardSpec("goblin_jab", "goblin_jab", Side.Enemy, 2,
                new[] { new EffectData(EffectKeys.Damage, 3) });

            var scenario = new MultiTurnScenario("no-trigger", 30,
                new[] { new EnemySpec("goblin", 100) },
                new[] { new TurnScript(3, new[] { counter, enemy }, new InterventionPlaySpec[0]) });

            var result = new MultiTurnRunner().Run(scenario);

            Assert.AreEqual(0, Resolved(result, "counter").DamageDealt); // no preceding enemy attack
            Assert.AreEqual(100, result.FinalState.Enemies[0].Hp);       // untouched
        }
    }
}
