using System;
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Run-persistent state between combats: node progress, party, seeded run-level RNG.
    /// All run-level randomness (combat seed derivation, reward rolls) must go through Rng
    /// (AGENTS.md rule 7) so the same run seed replays the same run.</summary>
    public sealed class RunState
    {
        private Random _rng;

        public RunState(
            RunDefinition definition,
            IReadOnlyList<RunMember> startingParty,
            PartyTuning tuning,
            int runSeed)
        {
            Nodes = definition.Nodes;
            Party = new List<RunMember>(startingParty);
            Tuning = tuning;
            RunSeed = runSeed;
        }

        public IReadOnlyList<RunNodeData> Nodes { get; }
        public int CurrentNodeIndex { get; private set; }
        public RunNodeData CurrentNode => Nodes[CurrentNodeIndex];
        public List<RunMember> Party { get; }
        public PartyTuning Tuning { get; }
        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;
        public int RunSeed { get; }

        /// <summary>Seeded run-level RNG (lazy, same pattern as CombatState.Rng).</summary>
        public Random Rng => _rng ??= new Random(RunSeed);

        public IReadOnlyList<RunMember> LivingMembers => Party.Where(m => m.IsAlive).ToList();

        /// <summary>Draws the next combat's seed from the run RNG —
        /// same run seed ⇒ same combat seed sequence (spec §3.1).</summary>
        public int NextCombatSeed() => Rng.Next();

        /// <summary>Returns false when already on the last node.</summary>
        public bool AdvanceToNextNode()
        {
            if (CurrentNodeIndex + 1 >= Nodes.Count)
            {
                return false;
            }

            CurrentNodeIndex++;
            return true;
        }

        public void SetOutcome(RunOutcome outcome) => Outcome = outcome;
    }
}
