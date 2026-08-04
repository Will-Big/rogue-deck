using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class EnemyIntentTests
    {
        [Test]
        public void For_turn_returns_that_turns_cards_and_clamps_past_the_end()
        {
            var t0 = new List<CardDefinition> { CardFixtures.EnemyAttack("jab_0", 4, 3) };
            var t1 = new List<CardDefinition> { CardFixtures.EnemyAttack("jab_1", 4, 5) };
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[] { t0, t1 });

            Assert.AreEqual("jab_0", intent.ForTurn(0)[0].Id);
            Assert.AreEqual("jab_1", intent.ForTurn(1)[0].Id);
            Assert.AreEqual("jab_1", intent.ForTurn(7)[0].Id); // clamps to the last defined turn
        }

        [Test]
        public void Empty_intent_returns_no_cards()
        {
            var intent = new EnemyIntent(new List<IReadOnlyList<CardDefinition>>());
            Assert.AreEqual(0, intent.ForTurn(0).Count);
        }
    }
}
