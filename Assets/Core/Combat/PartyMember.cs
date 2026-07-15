using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    /// <summary>Result of a single TakeDamage call: whether the hit was absorbed normally, survived
    /// on a "death's door" charge (HP steadied to 1), or was actually lethal.</summary>
    public enum DamageOutcome
    {
        Damaged,
        DeathsDoor,
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
        public int SurviveCharges { get; set; }
        public bool IsAlive => Hp > 0;
        public StatusBag Statuses { get; } = new();

        public PartyMember(string id, string name, int maxHp, int surviveCharges = 0)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            SurviveCharges = surviveCharges;
        }

        /// <summary>Applies damage. A lethal hit is absorbed by one SurviveCharges charge (HP steadies
        /// at 1, DeathsDoor); with no charges left a lethal hit kills.</summary>
        public DamageOutcome TakeDamage(int amount)
        {
            Hp -= amount;
            if (Hp > 0)
            {
                return DamageOutcome.Damaged;
            }

            if (SurviveCharges > 0)
            {
                SurviveCharges--;
                Hp = 1;
                return DamageOutcome.DeathsDoor;
            }

            return DamageOutcome.Died;
        }
    }
}
