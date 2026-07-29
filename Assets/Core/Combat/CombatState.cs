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
