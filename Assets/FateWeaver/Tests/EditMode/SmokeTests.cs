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

        [Test]
        public void Can_build_a_card_and_enemy()
        {
            var def = new FateWeaver.Core.Cards.CardDefinition(
                "strike", "Strike", Side.Player, CardType.Attack, 2,
                new[] { new FateWeaver.Core.Cards.EffectData(FateWeaver.Core.Effects.EffectKeys.Damage, 5) });

            var card = new FateWeaver.Core.Combat.ExecutionCardInstance(def);
            var enemy = new FateWeaver.Core.Combat.Enemy("goblin", 12);

            Assert.AreEqual("strike", def.Id);
            Assert.AreEqual(2, card.ExecutionOrder);
            Assert.AreEqual(12, enemy.Hp);
        }
    }
}
