using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Fate;

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
        public IReadOnlyList<FatePlaySpec> FatePlays { get; }

        public ScenarioDefinition(
            string name,
            int playerHp,
            int fateEnergy,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<ZoneCardSpec> zoneCards,
            IReadOnlyList<FatePlaySpec> fatePlays)
            : this(
                name.ToLowerInvariant().Replace(" ", "-"),
                name,
                playerHp,
                fateEnergy,
                enemies,
                zoneCards,
                fatePlays)
        {
        }

        public ScenarioDefinition(
            string id,
            string name,
            int playerHp,
            int fateEnergy,
            IReadOnlyList<EnemySpec> enemies,
            IReadOnlyList<ZoneCardSpec> zoneCards,
            IReadOnlyList<FatePlaySpec> fatePlays)
        {
            Id = id;
            Name = name;
            PlayerHp = playerHp;
            FateEnergy = fateEnergy;
            Enemies = enemies;
            ZoneCards = zoneCards;
            FatePlays = fatePlays;
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
        public CardType Type { get; }
        public int Initiative { get; }
        public IReadOnlyList<EffectData> Effects { get; }

        public ZoneCardSpec(
            string id,
            string name,
            Side side,
            CardType type,
            int initiative,
            IReadOnlyList<EffectData> effects)
        {
            Id = id;
            Name = name;
            Side = side;
            Type = type;
            Initiative = initiative;
            Effects = effects;
        }
    }

    public sealed class FatePlaySpec
    {
        public FateActionData Action { get; }
        public string TargetCardId { get; }
        public string SecondaryTargetCardId { get; }

        public FatePlaySpec(FateActionData action, string targetCardId, string secondaryTargetCardId = null)
        {
            Action = action;
            TargetCardId = targetCardId;
            SecondaryTargetCardId = secondaryTargetCardId;
        }
    }
}
