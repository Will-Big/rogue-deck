using System;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Typed wrapper over a run-map node type id (open set, type-safe) —
    /// same pattern as EffectKey. Plain readonly struct to stay within Unity 6's C# 9.</summary>
    public readonly struct RunNodeKey : IEquatable<RunNodeKey>
    {
        public string Id { get; }

        public RunNodeKey(string id) => Id = id;

        public bool Equals(RunNodeKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RunNodeKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(RunNodeKey a, RunNodeKey b) => a.Equals(b);
        public static bool operator !=(RunNodeKey a, RunNodeKey b) => !a.Equals(b);
    }

    public static class RunNodeKeys
    {
        public static readonly RunNodeKey NormalCombat = new RunNodeKey("combat_normal");
        public static readonly RunNodeKey EliteCombat = new RunNodeKey("combat_elite");
        public static readonly RunNodeKey BossCombat = new RunNodeKey("combat_boss");
        public static readonly RunNodeKey RecruitHeal = new RunNodeKey("recruit_heal");
    }
}
