using System;
using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>Mutable combat state. FateEnergyPerTurn is a variable (NOT fixed 3).
    /// Party and Enemies are two independent side formations; index 0 is each side's front.</summary>
    public sealed class CombatState
    {
        /// <summary>Id of the single legacy party member that PlayerHp/PlayerStatuses delegate to.
        /// Only the pre-party single-player shim below may reference this id; party-mode code must
        /// read/write CombatState.Party directly instead.</summary>
        public const string LegacyPlayerId = "player";
        private const string LegacyPlayerName = "Player";
        private const int LegacyPlayerDefaultMaxHp = 0;

        private readonly PartyMember _legacyPlayer;
        private Random _rng;

        public CombatState()
        {
            _legacyPlayer = new PartyMember(LegacyPlayerId, LegacyPlayerName, LegacyPlayerDefaultMaxHp);
            Party.Add(_legacyPlayer);
        }

        /// <summary>Independent party formation; index 0 is the party's front.</summary>
        public List<PartyMember> Party { get; } = new();

        /// <summary>Independent enemy formation; index 0 is the enemy side's front.</summary>
        public List<Enemy> Enemies { get; } = new();

        public FutureZone Zone { get; } = new();
        public int FateEnergy { get; set; }
        public int FateEnergyPerTurn { get; set; }
        public int RngSeed { get; set; }

        /// <summary>Seeded RNG shared by all combat rule logic (AGENTS.md rule 7: no ad-hoc `new Random()`
        /// elsewhere). Lazily created from RngSeed so RngSeed can still be assigned via object initializer.</summary>
        public Random Rng => _rng ??= new Random(RngSeed);

        // --- Legacy single-player shim: delegates to the first party member (LegacyPlayerId). Keeps
        // pre-party single-player code/tests working untouched. New party-mode code must not use this. ---
        public int PlayerHp
        {
            get => _legacyPlayer.Hp;
            set => _legacyPlayer.Hp = value;
        }

        public StatusBag PlayerStatuses => _legacyPlayer.Statuses;
    }
}
