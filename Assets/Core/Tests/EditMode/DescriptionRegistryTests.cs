using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation.Descriptions;
using NUnit.Framework;

namespace FateWeaver.Tests.EditMode
{
    public class DescriptionRegistryTests
    {
        private static EffectDescriptionRequest FakeEffect(EffectData effect, int value, DescriptionRequestContext context)
            => new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.fake", ("amount", value)));

        private static LocalizedTextRequest FakeAction(InterventionActionData action)
            => DescriptionRequests.Text("intervention.fake");

        [Test]
        public void Effect_registry_is_typed_and_fail_fast()
        {
            var key = new EffectKey("test_effect");
            Func<EffectData, int, DescriptionRequestContext, EffectDescriptionRequest> handler = FakeEffect;
            var registry = new RequestHandlerRegistry<EffectKey, Func<EffectData, int, DescriptionRequestContext, EffectDescriptionRequest>>();
            registry.Register(key, handler);

            Assert.AreSame(handler, registry.Resolve(key));
            Assert.Throws<ArgumentNullException>(() => registry.Register(new EffectKey("null_handler"), null));
            Assert.Throws<ArgumentException>(() => registry.Register(key, FakeEffect));
            Assert.Throws<KeyNotFoundException>(() =>
                registry.Resolve(new EffectKey("missing")));
        }

        [Test]
        public void Intervention_registry_is_fail_fast()
        {
            var actionKey = new InterventionActionKey("test_action");
            Func<InterventionActionData, LocalizedTextRequest> handler = FakeAction;
            var actions = new RequestHandlerRegistry<InterventionActionKey, Func<InterventionActionData, LocalizedTextRequest>>();
            actions.Register(actionKey, handler);

            Assert.AreSame(handler, actions.Resolve(actionKey));
            Assert.Throws<ArgumentNullException>(() => actions.Register(new InterventionActionKey("null_handler"), null));
            Assert.Throws<ArgumentException>(() => actions.Register(actionKey, FakeAction));
            Assert.Throws<KeyNotFoundException>(() =>
                actions.Resolve(new InterventionActionKey("missing")));
        }

        [Test]
        public void Default_catalog_rejects_duplicate_and_unregistered_keys()
        {
            var catalog = DescriptionRequestCatalog.CreateDefault(TestContent.Statuses());

            Assert.Throws<ArgumentException>(() =>
                catalog.Effects.Register(EffectKeys.Damage, FakeEffect));
            Assert.Throws<KeyNotFoundException>(() =>
                catalog.Interventions.Resolve(new InterventionActionKey("missing")));
        }
    }
}
