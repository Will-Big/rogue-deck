using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class FutureZoneTests
    {
        private static ExecutionCardInstance Card(string id, int executionOrder, Side side = Side.Player)
        {
            var def = new CardDefinition(id, id, side, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, 1) });
            return new ExecutionCardInstance(def);
        }

        [Test]
        public void ResolutionOrder_is_ascending_and_stable_on_ties()
        {
            var zone = new FutureZone();
            zone.Add(Card("A", 3));
            zone.Add(Card("B", 1));
            zone.Add(Card("C", 1)); // tie with B; inserted after B

            var order = zone.ResolutionOrder().Select(c => c.Def.Id).ToArray();

            // ascending executionOrder; B before C because of stable tie-break
            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, order);
        }

        [Test]
        public void ResolutionOrder_prioritizes_player_cards_when_executionOrder_ties()
        {
            var zone = new FutureZone();
            zone.Add(Card("enemy", 2, Side.Enemy));
            zone.Add(Card("player", 2, Side.Player));
            zone.Add(Card("faster_enemy", 1, Side.Enemy));

            var order = zone.ResolutionOrder().Select(c => c.Def.Id).ToArray();

            CollectionAssert.AreEqual(new[] { "faster_enemy", "player", "enemy" }, order);
        }

        [TestCase(1, 0)]
        [TestCase(3, 1)]
        [TestCase(6, 2)]
        public void Preview_insertion_uses_execution_order_without_mutating_zone(
            int candidateOrder, int expectedIndex)
        {
            var zone = new FutureZone();
            zone.Add(Card("fast", 2));
            zone.Add(Card("slow", 5));
            var before = zone.Cards.ToArray();

            int index = zone.PreviewInsertionIndex(Card("candidate", candidateOrder));

            Assert.AreEqual(expectedIndex, index);
            CollectionAssert.AreEqual(before, zone.Cards);
        }

        [Test]
        public void Preview_insertion_puts_new_player_after_player_ties_and_before_enemy_ties()
        {
            var zone = new FutureZone();
            zone.Add(Card("enemy", 5, Side.Enemy));
            zone.Add(Card("player", 5, Side.Player));

            int index = zone.PreviewInsertionIndex(Card("candidate", 5, Side.Player));

            Assert.AreEqual(1, index);
            CollectionAssert.AreEqual(new[] { "player", "enemy" },
                zone.ResolutionOrder().Select(card => card.Def.Id).ToArray());
        }
    }
}
