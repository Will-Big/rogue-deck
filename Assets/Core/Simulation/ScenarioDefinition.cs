using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation
{
    public sealed class ScenarioDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int PlayerHp { get; }
        public int FateEnergy { get; }
        public IReadOnlyList<EnemySpec> Enemies { get; }
        public IReadOnlyList<ZoneCardSpec> ZoneCards { get; }
        public IReadOnlyList<InterventionPlaySpec> InterventionPlays { get; }

        public ScenarioDefinition(
            string name,
            int playerHp,
            int fateEnergy,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<ZoneCardSpec> zoneCards,
            IReadOnlyList<InterventionPlaySpec> interventionPlays)
            : this(
                name.ToLowerInvariant().Replace(" ", "-"),
                name,
                playerHp,
                fateEnergy,
                enemies,
                zoneCards,
                interventionPlays)
        {
        }

        public ScenarioDefinition(
            string id,
            string name,
            int playerHp,
            int fateEnergy,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<ZoneCardSpec> zoneCards,
            IReadOnlyList<InterventionPlaySpec> interventionPlays)
        {
            Id = id;
            Name = name;
            PlayerHp = playerHp;
            FateEnergy = fateEnergy;
            Enemies = enemies;
            ZoneCards = zoneCards;
            InterventionPlays = interventionPlays;
        }
    }

    public sealed class EnemySpec
    {
        public string Id { get; }
        public int Hp { get; }

        public EnemySpec(string id, int hp)
        {
            Id = id;
            Hp = hp;
        }
    }

    public sealed class ZoneCardSpec
    {
        public string Id { get; }
        public string Name { get; }
        public Side Side { get; }
        public int ExecutionOrder { get; }
        public IReadOnlyList<EffectData> Effects { get; }

        public ZoneCardSpec(
            string id,
            string name,
            Side side,
            int executionOrder,
            IReadOnlyList<EffectData> effects)
        {
            Id = id;
            Name = name;
            Side = side;
            ExecutionOrder = executionOrder;
            Effects = effects;
        }
    }

    public sealed class InterventionPlaySpec
    {
        public InterventionActionData Intervention { get; }
        public string TargetCardId { get; }
        public string SecondaryTargetCardId { get; }

        public InterventionPlaySpec(InterventionActionData action, string targetCardId, string secondaryTargetCardId = null)
        {
            Intervention = action;
            TargetCardId = targetCardId;
            SecondaryTargetCardId = secondaryTargetCardId;
        }
    }
}
