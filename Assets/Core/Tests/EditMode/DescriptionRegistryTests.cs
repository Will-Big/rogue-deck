using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Descriptions;
using NUnit.Framework;

namespace FateWeaver.Tests.EditMode
{
    public class DescriptionRegistryTests
    {
        private sealed class FakeEffectHandler : IEffectDescriptionHandler
        {
            public EffectKey Key { get; }

            public FakeEffectHandler(EffectKey key) => Key = key;

            public string Describe(EffectData effect, int value, DescriptionContext context)
                => "effect:" + value;
        }

        private sealed class FakeInterventionHandler : IInterventionDescriptionHandler
        {
            public InterventionActionKey Key { get; }
            public string DisplayName => "fake action";

            public FakeInterventionHandler(InterventionActionKey key) => Key = key;

            public string Describe(InterventionActionData action, DescriptionContext context)
                => "action:" + action.EffectValue;
        }

        [Test]
        public void Effect_registry_is_typed_and_fail_fast()
        {
            var key = new EffectKey("test_effect");
            var handler = new FakeEffectHandler(key);
            var registry = new EffectDescriptionRegistry();
            registry.Register(handler);

            Assert.AreSame(handler, registry.Resolve(key));
            Assert.IsTrue(registry.Contains(key));
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentException>(() =>
                registry.Register(new FakeEffectHandler(new EffectKey(null))));
            Assert.Throws<ArgumentException>(() => registry.Register(new FakeEffectHandler(key)));
            Assert.Throws<KeyNotFoundException>(() =>
                registry.Resolve(new EffectKey("missing")));
        }

        [Test]
        public void Intervention_and_status_registries_are_fail_fast()
        {
            var actionKey = new InterventionActionKey("test_action");
            var actions = new InterventionDescriptionRegistry();
            actions.Register(new FakeInterventionHandler(actionKey));

            Assert.AreEqual("fake action", actions.Resolve(actionKey).DisplayName);
            Assert.Throws<ArgumentNullException>(() => actions.Register(null));
            Assert.Throws<ArgumentException>(() => actions.Register(
                new FakeInterventionHandler(new InterventionActionKey(null))));
            Assert.Throws<ArgumentException>(() =>
                actions.Register(new FakeInterventionHandler(actionKey)));
            Assert.Throws<KeyNotFoundException>(() =>
                actions.Resolve(new InterventionActionKey("missing")));

            var statusKey = new StatusKey("test_status");
            var statuses = new StatusDescriptionRegistry();
            statuses.Register(statusKey, "시험 상태");

            Assert.AreEqual("시험 상태", statuses.Resolve(statusKey));
            Assert.Throws<ArgumentException>(() => statuses.Register(statusKey, "중복"));
            Assert.Throws<ArgumentException>(() =>
                statuses.Register(new StatusKey("blank"), ""));
            Assert.Throws<KeyNotFoundException>(() =>
                statuses.Resolve(new StatusKey("missing")));
        }
    }
}
