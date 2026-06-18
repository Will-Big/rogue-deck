using System.Collections.Generic;

namespace FateWeaver.Core.Combat
{
    /// <summary>Mutable combat state. FateEnergyPerTurn is a variable (NOT fixed 3).</summary>
    public sealed class CombatState
    {
        public int PlayerHp { get; set; }
        public List<Enemy> Enemies { get; } = new();
        public FutureZone Zone { get; } = new();
        public int FateEnergy { get; set; }
        public int FateEnergyPerTurn { get; set; }
        public int RngSeed { get; set; }
    }
}
