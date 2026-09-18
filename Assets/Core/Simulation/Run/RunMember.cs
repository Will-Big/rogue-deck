using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>One party member's run-persistent state: HP carried between combats and the cards
    /// this character owns (party-foundation rule: every card belongs to a character).</summary>
    public sealed class RunMember
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Hp { get; set; }
        public List<CardDefinition> Cards { get; } = new();
        public bool IsAlive => Hp > 0;

        public RunMember(string id, string name, int maxHp, IEnumerable<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            if (cards != null)
            {
                Cards.AddRange(cards);
            }
        }
    }
}
