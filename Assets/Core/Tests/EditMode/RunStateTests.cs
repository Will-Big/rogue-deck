using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class RunStateTests
    {
        private static RunState NewRun(int seed) => new RunState(
            new[]
            {
                new RunMember("member_a", "파티원 A", 25, null),
                new RunMember("member_b", "파티원 B", 25, null)
            },
            seed);

        [Test]
        public void EnterNode_returns_the_current_index_then_advances()
        {
            var run = NewRun(seed: 1);

            Assert.AreEqual(0, run.NodesEntered);
            Assert.AreEqual(0, run.EnterNode());
            Assert.AreEqual(1, run.EnterNode());
            Assert.AreEqual(2, run.NodesEntered);
        }

        [Test]
        public void RunSeed_is_kept()
        {
            Assert.AreEqual(41, NewRun(seed: 41).RunSeed);
        }

        [Test]
        public void LivingMembers_excludes_dead_in_party_order()
        {
            var run = NewRun(seed: 1);
            run.Party[0].Hp = 0;

            Assert.AreEqual(1, run.LivingMembers.Count);
            Assert.AreEqual("member_b", run.LivingMembers[0].Id);
        }

        [Test]
        public void Outcome_starts_in_progress_and_is_settable()
        {
            var run = NewRun(seed: 1);

            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);
            run.SetOutcome(RunOutcome.Defeat);
            Assert.AreEqual(RunOutcome.Defeat, run.Outcome);
        }
    }
}
