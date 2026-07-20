using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class LockedEnemyExecutionOrderTests
    {
        private static CardDefinition PlayerStrike() => new CardDefinition(
            "p_strike", "찌르기", Side.Player, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) }) { EnergyCost = 0, Category = CardCategory.Execution };

        private static CardDefinition EnemyJab(bool locked) => new CardDefinition(
            locked ? "locked_jab" : "enemy_jab", "찌르기", Side.Enemy, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) })
            { EnergyCost = 0, Category = CardCategory.Execution, StartsLocked = locked };

        [Test]
        public void Locked_enemy_cards_ignore_enemy_slow_when_entering_the_zone()
        {
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[]
            {
                new[] { EnemyJab(false) },
                new[] { EnemyJab(true) }
            });
            var session = new DeckCombatSession(
                new[] { PlayerStrike() },
                100,
                new[] { new Enemy("warden", 100) },
                intent,
                3,
                5,
                1);

            session.State.Enemies[0].Statuses.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());

            var jab = session.CurrentOrder.First(c => c.Def.Id == "locked_jab");
            Assert.IsTrue(jab.IsLocked);
            Assert.AreEqual(5, jab.ExecutionOrder);
        }
    }
}
