using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>Result of a single TakeDamage call: whether the member is still alive or the hit was lethal.</summary>
    public enum DamageOutcome
    {
        Damaged,
        Died
    }

    /// <summary>One member of the player's party. Independent HP and status bag from every other
    /// member and from enemies (Assets/Core/Status/StatusBag.cs is per-holder, never shared).</summary>
    public sealed class PartyMember : IStatusHolder
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public bool IsAlive => Hp > 0;
        public StatusBag Statuses { get; } = new();

        public PartyMember(string id, string name, int maxHp)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
        }

        /// <summary>Applies damage. A hit that brings HP to zero or below kills.</summary>
        public DamageOutcome TakeDamage(int amount)
        {
            Hp -= amount;
            return Hp > 0 ? DamageOutcome.Damaged : DamageOutcome.Died;
        }
    }
}
