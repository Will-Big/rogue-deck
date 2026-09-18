using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class NewEffectSpecTests
    {
        [Test]
        public void Consume_status_spec_maps_payload_and_selector()
        {
            var spec = new ConsumeStatusSpec
            {
                Id = "pay",
                TargetFaction = CardTargetFaction.Enemy,
                Status = StatusKeyRef.Of(StatusKeys.Poison),
                Amount = 3,
                Mode = ConsumptionMode.UpTo
            };
            var effect = spec.ToEffectData(
                Side.Player, new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne));

            Assert.AreEqual(EffectKeys.ConsumeStatus, effect.Key);
            var payload = (ConsumeStatusPayload)effect.Payload;
            Assert.AreEqual(StatusKeys.Poison, payload.Key);
            Assert.AreEqual(3, payload.Amount);
            Assert.AreEqual(ConsumptionMode.UpTo, payload.Mode);
            Assert.AreEqual(TargetSelector.FrontOne, effect.TargetSelector);
            Assert.IsEmpty(spec.Validate(AuthoringContext.Default()).ToList());
            Assert.IsTrue(spec.ProducesConsumption);
        }

        [Test]
        public void Start_condition_spec_maps_new_kinds()
        {
            var noFollowing = new StartConditionSpec { Kind = ConditionKind.NoFollowingPlayerCard };
            Assert.IsInstanceOf<NoFollowingCardOfSide>(noFollowing.ToCondition());
            Assert.AreEqual(Side.Player, ((NoFollowingCardOfSide)noFollowing.ToCondition()).Side);
        }

        [Test]
        public void Requirement_and_skip_are_carried_onto_the_effect()
        {
            var spec = new GrantNextTurnFateSpec
            {
                Id = "reward",
                Value = 1,
                Requires = new EffectRequirementSpec { SourceEffectId = "pay", MinimumConsumed = 1 }
            };

            var effect = spec.ToEffectData(Side.Player, null);

            Assert.AreEqual(EffectKeys.GrantNextTurnFate, effect.Key);
            Assert.AreEqual("reward", effect.Id);
            Assert.AreEqual(new EffectResultRequirement("pay", 1), effect.Requirement);
        }

        [Test]
        public void Catalog_lists_the_three_new_specs()
        {
            var types = EffectSpecCatalog.All().Select(i => i.SpecType).ToList();
            CollectionAssert.Contains(types, typeof(ConsumeStatusSpec));
            CollectionAssert.Contains(types, typeof(TriggerStatusSpec));
            CollectionAssert.Contains(types, typeof(GrantNextTurnFateSpec));
        }

        [Test]
        public void Descriptions_resolve_for_all_new_effect_keys()
        {
            var catalog = FateWeaver.Simulation.Descriptions.KoreanDescriptionCatalog.CreateDefault(TestContent.Statuses());
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.ConsumeStatus));
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.TriggerStatus));
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.GrantNextTurnFate));
        }
    }
}
