using NUnit.Framework;
using FateWeaver.Core.Cards;

namespace FateWeaver.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Enums_are_referenceable_from_tests()
        {
            Assert.AreEqual(Side.Player, Side.Player);
            Assert.AreNotEqual(CardType.Attack, CardType.Defense);
        }
    }
}
