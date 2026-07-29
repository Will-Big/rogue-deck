using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class StatusHookSurfaceTests
    {
        private sealed class InertBehavior : StatusBehavior
        {
            public override StatusKey Key => new StatusKey("inert_test");
            public override StatusScope Scope => StatusScope.Entity;
        }

        [Test]
        public void Base_behavior_defaults_are_no_ops()
        {
            var behavior = new InertBehavior();
            Assert.IsFalse(behavior.StacksMagnitude);
            // 기본 구현이 아무것도 하지 않고 예외 없이 통과해야 한다.
            behavior.OnTurnEnd(new StatusTickContext());
            behavior.OnHolderDied(new StatusDeathContext());
        }

        [Test]
        public void Stack_creates_then_accumulates_magnitude()
        {
            var bag = new StatusBag();
            var key = new StatusKey("poison_test");

            var first = bag.Stack(key, StatusLifetime.Permanent, 2);
            Assert.AreEqual(2, first.Magnitude);

            var second = bag.Stack(key, StatusLifetime.Permanent, 3);
            Assert.AreSame(first, second);          // 같은 인스턴스에 누적
            Assert.AreEqual(5, bag.Get(key).Magnitude);
            Assert.AreEqual(1, CountOf(bag, key));  // 인스턴스는 키당 하나
        }

        [Test]
        public void Stack_keeps_first_lifetime_kind()
        {
            var bag = new StatusBag();
            var key = new StatusKey("block_test");
            bag.Stack(key, StatusLifetime.ThisTurn, 3);
            bag.Stack(key, StatusLifetime.ThisTurn, 1);

            Assert.AreEqual(StatusLifetimeKind.ThisTurn, bag.Get(key).Kind);
            Assert.AreEqual(4, bag.Get(key).Magnitude);
        }

        private static int CountOf(StatusBag bag, StatusKey key)
        {
            var count = 0;
            foreach (var status in bag.All)
            {
                if (status.Key == key) count++;
            }
            return count;
        }
    }
}
