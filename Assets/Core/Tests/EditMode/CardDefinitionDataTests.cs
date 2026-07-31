using System;
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class CardDefinitionDataTests
    {
        [Test]
        public void CardType_is_absent_from_core_and_authoring_schemas()
        {
            var assembly = typeof(CardDefinition).Assembly;
            var removedTypeName = "FateWeaver.Core.Cards.Card" + "Type";
            Assert.IsNull(assembly.GetType(removedTypeName));
            Assert.IsNull(typeof(CardDefinition).GetProperty("Type"));
            Assert.IsNull(typeof(CardSpec).GetField("Type"));
            Assert.IsNull(typeof(ZoneCardSpec).GetProperty("Type"));
        }

        [Test]
        public void Execution_card_defaults_to_execution_category()
        {
            var card = new CardDefinition(
                "slash", "베기", Side.Player, 5,
                new[] { new EffectData(EffectKeys.Damage, 3) }) { EnergyCost = 1 };

            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(1, card.EnergyCost);
            Assert.IsNull(card.InterventionAction);
        }

        [Test]
        public void Intervention_card_carries_an_intervention_action()
        {
            var action = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2);
            var card = new CardDefinition(
                "pull", "앞당김", Side.Player, 0, Array.Empty<EffectData>())
                { EnergyCost = 1, Category = CardCategory.Intervention, InterventionAction = action };

            Assert.AreEqual(CardCategory.Intervention, card.Category);
            Assert.AreSame(action, card.InterventionAction);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void HasEffect_derives_damage_capability_from_effect_composition(
            bool hasDamage,
            bool hasBlock)
        {
            var effects = new List<EffectData>();
            if (hasDamage) effects.Add(new EffectData(EffectKeys.Damage, 3));
            if (hasBlock)
                effects.Add(EffectData.ApplyStatus(
                    StatusKeys.Block,
                    StatusLifetime.ThisTurn,
                    StatusApplyTarget.Self,
                    2));

            var card = new CardDefinition(
                "test", "test", Side.Player, 5, effects);

            Assert.AreEqual(hasDamage, card.HasEffect(EffectKeys.Damage));
        }

        [Test]
        public void HasEffect_rejects_an_empty_key()
        {
            var card = new CardDefinition(
                "test", "test", Side.Player, 5,
                Array.Empty<EffectData>());

            Assert.Throws<ArgumentException>(() => card.HasEffect(default));
        }
    }
}
