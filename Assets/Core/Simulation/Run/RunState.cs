using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Simulation.Run
{
    /// <summary>전투 사이에 이어지는 런 상태: 런 시드, 파티원(덱·HP), 진입한 노드 수.
    /// 노드 목록은 여기 없다 — 노드 목록은 맵의 범주다(전투 노드 설계 결정 6). 무작위는 이 객체가
    /// 들고 있지 않고, SeedDerivation이 런 시드와 노드 순번에서 노드마다 새로 파생한다.</summary>
    public sealed class RunState
    {
        public RunState(IReadOnlyList<RunMember> startingParty, int runSeed)
        {
            Party = new List<RunMember>(startingParty);
            RunSeed = runSeed;
        }

        public int RunSeed { get; }
        public List<RunMember> Party { get; }
        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

        /// <summary>진입한 노드 수. 다음에 진입할 노드의 순번이기도 하다.</summary>
        public int NodesEntered { get; private set; }

        public IReadOnlyList<RunMember> LivingMembers => Party.Where(m => m.IsAlive).ToList();

        /// <summary>현재 순번을 돌려주고 1 늘린다. 노드 시드는 이 순번에서 파생된다.</summary>
        public int EnterNode() => NodesEntered++;

        public void SetOutcome(RunOutcome outcome) => Outcome = outcome;
    }
}
