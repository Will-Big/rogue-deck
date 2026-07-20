using NUnit.Framework;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunNodeKeyTests
    {
        [Test]
        public void SameId_AreEqual()
        {
            Assert.That(new RunNodeKey("combat_normal"), Is.EqualTo(RunNodeKeys.NormalCombat));
            Assert.That(RunNodeKeys.NormalCombat == new RunNodeKey("combat_normal"), Is.True);
            Assert.That(RunNodeKeys.NormalCombat != RunNodeKeys.BossCombat, Is.True);
        }

        [Test]
        public void RunDefinition_ExposesNodesInOrder()
        {
            var nodes = new[]
            {
                new RunNodeData(RunNodeKeys.NormalCombat, null),
                new RunNodeData(RunNodeKeys.RecruitHeal, null)
            };
            var definition = new RunDefinition(nodes);
            Assert.That(definition.Nodes.Count, Is.EqualTo(2));
            Assert.That(definition.Nodes[1].Key, Is.EqualTo(RunNodeKeys.RecruitHeal));
        }
    }
}
