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

        /// <summary>이 전투에서 죽을 피해를 버티는 횟수. 캐릭터 JSON이 원본이다.</summary>
        public int SurviveCharges { get; }
        public IReadOnlyList<CardDefinition> Cards { get; }

        public PartyMemberLoadout(
            string id,
            string name,
            int maxHp,
            int surviveCharges,
            IReadOnlyList<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            SurviveCharges = surviveCharges;
            Cards = cards;
        }
    }
}
