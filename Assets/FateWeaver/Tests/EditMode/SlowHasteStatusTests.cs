using NUnit.Framework;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class SlowHasteStatusTests
    {
        private static StatusContext Ctx(StatusKey key, int magnitude) =>
            new StatusContext { Instance = new StatusInstance(key, StatusLifetime.Turns(2), magnitude) };

        [Test]
        public void Base_behavior_does_not_change_initiative()
        {
            var block = new BlockBehavior();
            Assert.AreEqual(5, block.ModifyInitiative(5, Ctx(StatusKeys.Block, 3)));
        }

        [Test]
        public void Slow_adds_magnitude_to_initiative()
        {
            var slow = new SlowBehavior();
            Assert.AreEqual(StatusScope.Entity, slow.Scope);
            Assert.AreEqual(StatusKeys.Slow, slow.Key);
            Assert.AreEqual(8, slow.ModifyInitiative(5, Ctx(StatusKeys.Slow, 3)));
        }

        [Test]
        public void Haste_subtracts_magnitude_from_initiative()
        {
            var haste = new HasteBehavior();
            Assert.AreEqual(StatusScope.Entity, haste.Scope);
            Assert.AreEqual(StatusKeys.Haste, haste.Key);
            Assert.AreEqual(2, haste.ModifyInitiative(5, Ctx(StatusKeys.Haste, 3)));
        }
        private static StatusRegistry Registry()
        {
            var r = new StatusRegistry();
            r.Register(new SlowBehavior());
            r.Register(new HasteBehavior());
            r.Register(new StunBehavior());
            return r;
        }

        [Test]
        public void Fold_applies_entity_statuses_only()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            Assert.AreEqual(8, StatusInitiative.InitiativeFor(5, bag, Registry()));

            var bag2 = new StatusBag();
            bag2.Add(StatusKeys.Haste, StatusLifetime.Turns(2), 2);
            Assert.AreEqual(3, StatusInitiative.InitiativeFor(5, bag2, Registry()));
        }

        [Test]
        public void Fold_ignores_card_scoped_and_null_inputs()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Stun, StatusLifetime.UntilConsumed(1)); // card-scoped -> ignored
            Assert.AreEqual(5, StatusInitiative.InitiativeFor(5, bag, Registry()));
            Assert.AreEqual(5, StatusInitiative.InitiativeFor(5, bag, null));
            Assert.AreEqual(5, StatusInitiative.InitiativeFor(5, null, Registry()));
        }
    }
}
