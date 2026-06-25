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
    }
}
