using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Authoring.Rules;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation.Run
{
    public enum CombatNodePhase
    {
        Combat,
        Reward,
        Defeated,
        Done
    }

    /// <summary>전투 노드가 쓰는 규칙과 공급자. 규칙은 combat_rules.json 하나에서 온다.</summary>
    public sealed class CombatNodeContext
    {
        public CombatNodeContext(
            StatusContentCatalog statuses,
            CombatRules rules,
            IEncounterSource encounters,
            IRewardCandidateSource rewardCandidates)
        {
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            RewardCandidates = rewardCandidates ?? throw new ArgumentNullException(nameof(rewardCandidates));
        }

        public StatusContentCatalog Statuses { get; }
        public CombatRules Rules { get; }
        public IEncounterSource Encounters { get; }
        public IRewardCandidateSource RewardCandidates { get; }
    }

    /// <summary>전투 한 판의 단계(전투·보상·패배·완료)를 전이시키고 결과를 런에 반영한다.
    /// 화면·콘텐츠 파일·적의 출처를 모른다. 무작위는 노드 시드에서 목적별로 파생한 스트림만 쓴다
    /// (전투 노드 설계 1.2·1.3).</summary>
    public sealed class CombatNode
    {
        private readonly RunState _run;
        private readonly CombatNodeContext _context;

        private CombatNode(
            RunState run, CombatNodeContext context, int nodeIndex, int nodeSeed, DeckCombatSession session)
        {
            _run = run;
            _context = context;
            NodeIndex = nodeIndex;
            NodeSeed = nodeSeed;
            Session = session;
        }

        public int NodeIndex { get; }
        public int NodeSeed { get; }
        public DeckCombatSession Session { get; }
        public CombatNodePhase Phase { get; private set; } = CombatNodePhase.Combat;

        /// <summary>승리 후(Reward·Done)에만 있다.</summary>
        public RewardOffer Offer { get; private set; }

        public static CombatNode Begin(RunState run, CombatNodeContext context)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (run.Outcome != RunOutcome.InProgress)
            {
                throw new InvalidOperationException("Cannot begin a combat node on a finished run.");
            }

            var setup = context.Encounters.Pick(
                new Random(SeedDerivation.Stream(SeedDerivation.NodeSeed(run.RunSeed, run.NodesEntered), SeedStream.Encounter)));
            if (setup == null)
            {
                throw new InvalidOperationException("The encounter source returned no setup.");
            }

            if (setup.Enemies.Count != 1)
            {
                // 세션이 정책 하나만 받는다. 다중 적은 세션이 적마다 정책을 받게 된 뒤 지원한다(필수 후속 작업).
                throw new InvalidOperationException(
                    "The combat session supports exactly one enemy until per-enemy policies land.");
            }

            var nodeIndex = run.EnterNode();
            var nodeSeed = SeedDerivation.NodeSeed(run.RunSeed, nodeIndex);
            var loadouts = run.LivingMembers
                .Select(member => new PartyMemberLoadout(member.Id, member.Name, member.MaxHp, member.Cards.ToList()))
                .ToList();
            var session = new DeckCombatSession(
                context.Statuses,
                loadouts,
                new[] { setup.Enemies[0].Enemy },
                setup.Enemies[0].Policy,
                context.Rules.Party,
                partyCards: null,
                fateEnergyPerTurn: context.Rules.FateEnergyPerTurn,
                seed: SeedDerivation.Stream(nodeSeed, SeedStream.Combat));

            return new CombatNode(run, context, nodeIndex, nodeSeed, session);
        }

        public void Conclude()
        {
            if (Phase != CombatNodePhase.Combat || !Session.IsComplete)
            {
                throw new InvalidOperationException("Conclude requires a finished combat in the combat phase.");
            }

            if (Session.Outcome == Outcome.Lose)
            {
                _run.SetOutcome(RunOutcome.Defeat);
                Phase = CombatNodePhase.Defeated;
                return;
            }

            Offer = BuildOffer();
            Phase = CombatNodePhase.Reward;
        }

        public void Choose(int index)
        {
            RequireRewardPhase();
            if (index < 0 || index >= Offer.Candidates.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var candidate = Offer.Candidates[index];
            _run.Party.First(member => member.Id == candidate.OwnerId).Cards.Add(candidate.Card);
            Phase = CombatNodePhase.Done;
        }

        public void Skip()
        {
            RequireRewardPhase();
            Phase = CombatNodePhase.Done;
        }

        private void RequireRewardPhase()
        {
            if (Phase != CombatNodePhase.Reward)
            {
                throw new InvalidOperationException("Rewards can only be chosen in the reward phase.");
            }
        }

        /// <summary>생존 캐릭터 풀의 (카드, 소유자) 쌍에서 카드가 겹치지 않게 뽑는다. 소유자는 쌍에 이미
        /// 있으므로 따로 뽑지 않는다. 뽑기 한 번 = RNG 한 번이고, 뽑힌 카드의 다른 소유자 쌍은 지운다 —
        /// 카드마다 풀이 하나뿐인 진짜 데이터에서는 뽑힌 쌍 하나만 지워진다.</summary>
        private RewardOffer BuildOffer()
        {
            var rng = new Random(SeedDerivation.Stream(NodeSeed, SeedStream.Reward));
            var living = Session.State.Party.Where(member => member.IsAlive).Select(member => member.Id).ToList();
            var pairs = new List<RewardCandidate>(_context.RewardCandidates.Eligible(living));
            var distinct = pairs.Select(pair => pair.Card.Id).Distinct().Count();
            var count = Math.Min(_context.Rules.RewardChoices, distinct);

            var picked = new List<RewardCandidate>();
            for (int i = 0; i < count; i++)
            {
                var pick = pairs[rng.Next(pairs.Count)];
                picked.Add(pick);
                pairs.RemoveAll(pair => pair.Card.Id == pick.Card.Id);
            }

            return new RewardOffer(picked);
        }
    }
}
