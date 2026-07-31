using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Tests
{
    public class NewEffectSpecTests
    {
        [Test]
        public void Consume_status_spec_maps_payload_selector_and_condition()
        {
            var spec = new ConsumeStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Poison),
                MaxAmount = 3,
                DamageBonusPerConsumed = 2,
                Selector = TargetSelectorRef.FrontMost
            };
            var effect = spec.ToEffectData();

            Assert.AreEqual(EffectKeys.ConsumeStatus, effect.Key);
            var payload = (ConsumeStatusPayload)effect.Payload;
            Assert.AreEqual(StatusKeys.Poison, payload.Key);
            Assert.AreEqual(3, payload.MaxAmount);
            Assert.AreEqual(2, payload.DamageBonusPerConsumed);
            Assert.IsEmpty(spec.Validate(AuthoringContext.Default()).ToList());
        }

        [Test]
        public void Condition_spec_maps_new_kinds_and_skip_on_basic()
        {
            var noFollowing = new ConditionSpec
                { Kind = ConditionKind.NoFollowingPlayerCard, SuccessEffectValue = 2 };
            Assert.IsInstanceOf<NoFollowingCardOfSide>(noFollowing.ToCondition());
            Assert.AreEqual(FateWeaver.Core.Cards.Side.Player,
                ((NoFollowingCardOfSide)noFollowing.ToCondition()).Side);

            var consumed = new ConditionSpec
                { Kind = ConditionKind.ConsumedStatusAtLeast, N = 1, SuccessEffectValue = 4, SkipOnBasic = true };
            Assert.AreEqual(1, ((ConsumedStatusAtLeast)consumed.ToCondition()).N);

            var spec = new GrantNextTurnFateSpec { Value = 1, Condition = consumed };
            var effect = spec.ToEffectData();
            Assert.IsTrue(effect.SkipOnBasic);
            Assert.AreEqual(EffectKeys.GrantNextTurnFate, effect.Key);
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
            var catalog = FateWeaver.Simulation.Descriptions.KoreanDescriptionCatalog.CreateDefault();
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.ConsumeStatus));
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.TriggerStatus));
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.GrantNextTurnFate));
        }
    }
}
