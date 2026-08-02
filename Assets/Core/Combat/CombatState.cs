using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Combat
{
    /// <summary>Mutable combat state. FateEnergyPerTurn is a variable (NOT fixed 3).
    /// Party and Enemies are two independent side formations; index 0 is each side's front.</summary>
    public sealed class CombatState
    {
        /// <summary>Id of the single party member that solo (non-party) combats use. Also the OwnerId
        /// stamped on solo deck cards, so deck ownership and party membership agree.</summary>
        public const string SoloPlayerId = "player";
        private const string SoloPlayerName = "Player";

        private Random _rng;

        /// <summary>Independent party formation; index 0 is the party's front.</summary>
        public List<PartyMember> Party { get; } = new();

        /// <summary>Independent enemy formation; index 0 is the enemy side's front.</summary>
        public List<Enemy> Enemies { get; } = new();

        public FutureZone Zone { get; } = new();
        public int FateEnergy { get; set; }
        public int FateEnergyPerTurn { get; set; }
        /// <summary>다음 플레이어 사용 턴의 운명력 리필에 더해지는 1회성 적립분 (grant_next_turn_fate).
        /// 리필 시점에 합산 후 0으로 소거된다.</summary>
        public int PendingNextTurnFateEnergy { get; set; }
        public int RngSeed { get; set; }

        /// <summary>이 전투의 상태 저작 콘텐츠. 규칙(배율)과 수명 종류의 단일 출처다. 전투 단위로
        /// 존재하므로 전투 중 변경이 런으로 새지 않는다. 런 지속 변경(유물 등)은 전투 시작 시 이 값을
        /// 시딩해 반영한다. 파일 없이 도는 헤드리스 테스트와 하니스는 StatusContentDefaults.Catalog()로
        /// 폴백한다; Unity 런타임은 로더가 파일에서 만든 카탈로그를 주입한다.</summary>
        public Authoring.Statuses.StatusContentCatalog StatusContent { get; set; }
            = Authoring.Statuses.StatusContentDefaults.Catalog();

        public Status.StatusRuleSet StatusRules => StatusContent.Rules;

        /// <summary>Seeded RNG shared by all combat rule logic (AGENTS.md rule 7: no ad-hoc `new Random()`
        /// elsewhere). Lazily created from RngSeed so RngSeed can still be assigned via object initializer.</summary>
        public Random Rng => _rng ??= new Random(RngSeed);

        /// <summary>Adds the solo-mode party member. Party mode adds its own members instead.</summary>
        public PartyMember AddSoloPlayer(int hp)
        {
            var member = new PartyMember(SoloPlayerId, SoloPlayerName, hp);
            Party.Add(member);
            return member;
        }
    }
}
