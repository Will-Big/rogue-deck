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

        private static string[] Ids(FutureZone zone)
            => zone.ResolutionOrder().Select(card => card.Def.Id).ToArray();

        // V01
        [Test]
        public void Moving_to_an_occupied_number_joins_after_existing_peers()
        {
            var zone = new FutureZone();
            var a = Card("a", 6);
            var b = Card("b", 5);
            zone.Add(a);
            zone.Add(b);

            zone.MoveTo(a, 5);

            CollectionAssert.AreEqual(new[] { "b", "a" }, Ids(zone));
            Assert.AreEqual(5, a.ExecutionOrder);
        }

        [Test]
        public void Moving_to_the_same_number_keeps_the_current_place()
        {
            var zone = new FutureZone();
            var a = Card("a", 5);
            var b = Card("b", 5);
            zone.Add(a);
            zone.Add(b);

            zone.MoveTo(a, 5);

            CollectionAssert.AreEqual(new[] { "a", "b" }, Ids(zone));
        }

        // V02
        [Test]
        public void Exact_swap_preserves_enemy_before_player_on_equal_numbers()
        {
            var zone = new FutureZone();
            var p = Card("p", 5, Side.Player);
            var e = Card("e", 5, Side.Enemy);
            zone.Add(p);
            zone.Add(e);

            zone.SwapPositions(p, e);

            CollectionAssert.AreEqual(new[] { "e", "p" }, Ids(zone));
        }

        [Test]
        public void Swap_exchanges_places_and_numbers_together()
        {
            var zone = new FutureZone();
            var a = Card("a", 2);
            var b = Card("b", 7, Side.Enemy);
            zone.Add(a);
            zone.Add(b);

            zone.SwapPositions(a, b);

            CollectionAssert.AreEqual(new[] { "b", "a" }, Ids(zone));
            Assert.AreEqual(2, b.ExecutionOrder);
            Assert.AreEqual(7, a.ExecutionOrder);
        }

        [Test]
        public void Swap_rejects_the_same_card_or_a_card_outside_the_zone()
        {
            var zone = new FutureZone();
            var a = Card("a", 2);
            zone.Add(a);

            Assert.Throws<System.ArgumentException>(() => zone.SwapPositions(a, a));
            Assert.Throws<System.ArgumentException>(() => zone.SwapPositions(a, Card("stranger", 3)));
            CollectionAssert.AreEqual(new[] { "a" }, Ids(zone));
        }

        /// <summary>교환된 두 카드는 서로의 자리를 물려받는다 — 적은 플레이어 자리에, 플레이어는 적 자리에
        /// 선다. 뒤에 오는 카드는 교환이 있었다는 것을 모르는 것처럼 원래 규칙대로 자리를 찾는다(2026-09-18
        /// 사용자 결정).</summary>
        [Test]
        public void Swapped_cards_take_each_others_slot_for_later_arrivals()
        {
            var zone = new FutureZone();
            var p = Card("p", 5, Side.Player);
            var e = Card("e", 5, Side.Enemy);
            zone.Add(p);
            zone.Add(e);
            zone.SwapPositions(p, e);

            zone.Add(Card("p2", 5, Side.Player));
            zone.Add(Card("e2", 5, Side.Enemy));

            CollectionAssert.AreEqual(new[] { "e", "p2", "p", "e2" }, Ids(zone));
        }

        [Test]
        public void A_card_swapped_across_numbers_holds_the_other_cards_slot()
        {
            var zone = new FutureZone();
            var p = Card("p", 2, Side.Player);
            var e = Card("e", 7, Side.Enemy);
            zone.Add(p);
            zone.Add(e);
            zone.SwapPositions(p, e);

            // p는 7번의 적 자리에 서 있으므로 7번의 새 플레이어 카드가 그 앞에 온다.
            zone.Add(Card("p7", 7, Side.Player));
            // e는 2번의 플레이어 자리에 서 있으므로 2번의 새 적 카드는 그 뒤에 온다.
            zone.Add(Card("e2", 2, Side.Enemy));

            CollectionAssert.AreEqual(new[] { "e", "e2", "p7", "p" }, Ids(zone));
        }

        [Test]
        public void Moving_a_swapped_card_returns_it_to_its_own_sides_slot()
        {
            var zone = new FutureZone();
            var p = Card("p", 5, Side.Player);
            var e = Card("e", 5, Side.Enemy);
            zone.Add(p);
            zone.Add(e);
            zone.SwapPositions(p, e);

            zone.MoveTo(p, 3);
            zone.Add(Card("e3", 3, Side.Enemy));
            zone.Add(Card("p3", 3, Side.Player));

            CollectionAssert.AreEqual(new[] { "p", "p3", "e3", "e" }, Ids(zone));
        }

        [Test]
        public void New_player_card_goes_to_the_front_of_a_number_without_players()
        {
            var zone = new FutureZone();
            zone.Add(Card("e", 5, Side.Enemy));

            zone.Add(Card("p", 5, Side.Player));

            CollectionAssert.AreEqual(new[] { "p", "e" }, Ids(zone));
        }

        [TestCase(1, Side.Player)]
        [TestCase(5, Side.Player)]
        [TestCase(5, Side.Enemy)]
        [TestCase(9, Side.Enemy)]
        public void Preview_matches_the_actual_insertion(int order, Side side)
        {
            var zone = new FutureZone();
            var p = Card("p", 5, Side.Player);
            var e = Card("e", 5, Side.Enemy);
            zone.Add(Card("fast", 2));
            zone.Add(p);
            zone.Add(e);
            zone.SwapPositions(p, e);
            var candidate = Card("candidate", order, side);

            var preview = zone.PreviewInsertionIndex(candidate);
            zone.Add(candidate);

            Assert.AreEqual(preview, zone.ResolutionOrder().ToList().IndexOf(candidate));
        }

        [Test]
        public void Removing_an_owner_takes_only_its_pending_cards()
        {
            var zone = new FutureZone();
            var done = Card("done", 1);
            done.OwnerId = "a";
            var running = Card("running", 2);
            running.OwnerId = "a";
            var pending = Card("pending", 3);
            pending.OwnerId = "a";
            var other = Card("other", 4);
            other.OwnerId = "b";
            zone.Add(done);
            zone.Add(running);
            zone.Add(pending);
            zone.Add(other);
            done.ExecutionState = CardExecutionState.Executed;
            running.ExecutionState = CardExecutionState.Executing;

            var removed = zone.RemovePendingOwnedBy("a");

            CollectionAssert.AreEqual(new[] { pending }, removed);
            Assert.AreEqual(CardExecutionState.Removed, pending.ExecutionState);
            CollectionAssert.AreEqual(new[] { "done", "running", "other" }, Ids(zone));
        }
    }
}
