using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunStateTests
    {
        private static RunDefinition TwoNodes() => new RunDefinition(new[]
        {
            new RunNodeData(RunNodeKeys.NormalCombat, null),
            new RunNodeData(RunNodeKeys.BossCombat, null)
        });

        private static RunState NewRun(int seed) => new RunState(
            TwoNodes(),
            new[] { new RunMember("member_a", "파티원 A", PartyTuning.Prototype.DefaultMemberMaxHp, null) },
            PartyTuning.Prototype,
            seed);

        [Test]
        public void SameSeed_ProducesSameCombatSeedSequence()
        {
            var a = NewRun(seed: 41);
            var b = NewRun(seed: 41);
            Assert.That(a.NextCombatSeed(), Is.EqualTo(b.NextCombatSeed()));
            Assert.That(a.NextCombatSeed(), Is.EqualTo(b.NextCombatSeed()));
        }

        [Test]
        public void Advance_WalksNodesAndStopsAtEnd()
        {
            var run = NewRun(seed: 1);
            Assert.That(run.CurrentNode.Key, Is.EqualTo(RunNodeKeys.NormalCombat));
            Assert.That(run.AdvanceToNextNode(), Is.True);
            Assert.That(run.CurrentNode.Key, Is.EqualTo(RunNodeKeys.BossCombat));
            Assert.That(run.AdvanceToNextNode(), Is.False);
            Assert.That(run.CurrentNodeIndex, Is.EqualTo(1));
        }

        [Test]
        public void LivingMembers_ExcludesDead()
        {
            var run = NewRun(seed: 1);
            run.Party.Add(new RunMember("member_b", "파티원 B", PartyTuning.Prototype.DefaultMemberMaxHp, null));
            run.Party[0].Hp = 0;
            Assert.That(run.LivingMembers.Count, Is.EqualTo(1));
            Assert.That(run.LivingMembers[0].Id, Is.EqualTo("member_b"));
        }

        [Test]
        public void Outcome_StartsInProgress_AndIsSettable()
        {
            var run = NewRun(seed: 1);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.InProgress));
            run.SetOutcome(RunOutcome.Victory);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Victory));
        }
    }
}
