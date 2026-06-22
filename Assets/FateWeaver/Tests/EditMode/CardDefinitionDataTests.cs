using System;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;

namespace FateWeaver.Tests
{
    public class CardDefinitionDataTests
    {
        [Test]
        public void Action_card_defaults_to_action_category()
        {
            var card = new CardDefinition(
                "slash", "베기", Side.Player, CardType.Attack, 5,
                new[] { new EffectData(EffectKeys.Damage, 3) }) { Cost = 1 };

            Assert.AreEqual(CardCategory.Action, card.Category);
            Assert.AreEqual(1, card.Cost);
            Assert.IsNull(card.FateAction);
        }

        [Test]
        public void Fate_card_carries_a_fate_action()
        {
            var action = new FateActionData(FateActionKeys.ChangeInitiative, cost: 1, amount: -2);
            var card = new CardDefinition(
                "pull", "앞당김", Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
                { Cost = 1, Category = CardCategory.Fate, FateAction = action };

            Assert.AreEqual(CardCategory.Fate, card.Category);
            Assert.AreSame(action, card.FateAction);
        }
    }
}
