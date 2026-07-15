using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>One party member and the cards they contribute to the combined combat deck.</summary>
    public sealed class PartyMemberLoadout
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public IReadOnlyList<CardDefinition> Cards { get; }

        public PartyMemberLoadout(
            string id,
            string name,
            int maxHp,
            IReadOnlyList<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Cards = cards;
        }
    }
}
