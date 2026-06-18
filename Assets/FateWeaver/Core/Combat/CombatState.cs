using System.Collections.Generic;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>Mutable combat state. FateEnergyPerTurn is a variable (NOT fixed 3).</summary>
    public sealed class CombatState
    {
        public int PlayerHp { get; set; }
        public StatusBag PlayerStatuses { get; } = new();
        public List<Enemy> Enemies { get; } = new();
        public FutureZone Zone { get; } = new();
        public int FateEnergy { get; set; }
        public int FateEnergyPerTurn { get; set; }
        public int RngSeed { get; set; }
    }
}
