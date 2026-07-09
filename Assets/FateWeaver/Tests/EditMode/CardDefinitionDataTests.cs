using System;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class CardDefinitionDataTests
    {
        [Test]
        public void Execution_card_defaults_to_execution_category()
        {
            var card = new CardDefinition(
                "slash", "베기", Side.Player, CardType.Attack, 5,
                new[] { new EffectData(EffectKeys.Damage, 3) }) { Cost = 1 };

            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(1, card.Cost);
            Assert.IsNull(card.InterventionAction);
        }

        [Test]
        public void Intervention_card_carries_an_intervention_action()
        {
            var action = new InterventionActionData(InterventionActionKeys.ChangeInitiative, cost: 1, amount: -2);
            var card = new CardDefinition(
                "pull", "앞당김", Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
                { Cost = 1, Category = CardCategory.Intervention, InterventionAction = action };

            Assert.AreEqual(CardCategory.Intervention, card.Category);
            Assert.AreSame(action, card.InterventionAction);
        }
    }
}
