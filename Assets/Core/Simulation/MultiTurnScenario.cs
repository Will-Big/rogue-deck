using System.Collections.Generic;

namespace FateWeaver.Simulation
{
    /// <summary>A scenario spanning several turns. Player HP and enemies (with their statuses) persist
    /// across turns; each turn rebuilds the future zone and resets fate energy from its TurnScript.</summary>
    public sealed class MultiTurnScenario
    {
        public string Id { get; }
        public string Name { get; }
        public int PlayerHp { get; }
        public IReadOnlyList<EnemySpec> Enemies { get; }
        public IReadOnlyList<TurnScript> Turns { get; }

        public MultiTurnScenario(
            string name,
            int playerHp,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<TurnScript> turns)
            : this(name.ToLowerInvariant().Replace(" ", "-"), name, playerHp, enemies, turns)
        {
        }

        public MultiTurnScenario(
            string id,
            string name,
            int playerHp,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<TurnScript> turns)
        {
            Id = id;
            Name = name;
            PlayerHp = playerHp;
            Enemies = enemies;
            Turns = turns;
        }
    }
}
