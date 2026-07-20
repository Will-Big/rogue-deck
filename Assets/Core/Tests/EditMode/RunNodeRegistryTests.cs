using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunNodeRegistryTests
    {
        private sealed class FakeHandler : IRunNodeHandler
        {
            public FakeHandler(RunNodeKey key) => Key = key;
            public RunNodeKey Key { get; }
        }

        [Test]
        public void Resolve_ReturnsRegisteredHandler_AndThrowsOnUnknown()
        {
            var registry = new RunNodeRegistry();
            var handler = new FakeHandler(RunNodeKeys.RecruitHeal);
            registry.Register(handler);

            Assert.That(registry.Contains(RunNodeKeys.RecruitHeal), Is.True);
            Assert.That(registry.Resolve(RunNodeKeys.RecruitHeal), Is.SameAs(handler));
            Assert.Throws<KeyNotFoundException>(() => registry.Resolve(RunNodeKeys.BossCombat));
        }

        [Test]
        public void Validator_FlagsUnregisteredKeyAndNullPayload()
        {
            var registry = new RunNodeRegistry();
            registry.Register(new FakeHandler(RunNodeKeys.NormalCombat));

            var definition = new RunDefinition(new[]
            {
                new RunNodeData(RunNodeKeys.NormalCombat, null),      // payload 없음 → 에러
                new RunNodeData(RunNodeKeys.RecruitHeal, new DummyPayload()) // 핸들러 미등록 → 에러
            });

            var errors = RunDefinitionValidator.Validate(definition, registry);
            Assert.That(errors.Count, Is.EqualTo(2));
        }

        private sealed class DummyPayload : IRunNodePayload
        {
        }
    }
}
