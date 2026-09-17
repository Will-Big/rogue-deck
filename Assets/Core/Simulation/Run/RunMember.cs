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

        /// <summary>전투마다 로드아웃으로 전달되는 생존 충전 수. 전투 중 소모는 세션의 PartyMember가 들고, 이 값은 줄지 않는다.</summary>
        public int SurviveCharges { get; }
        public int Hp { get; set; }
        public List<CardDefinition> Cards { get; } = new();
        public bool IsAlive => Hp > 0;

        public RunMember(string id, string name, int maxHp, int surviveCharges, IEnumerable<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            SurviveCharges = surviveCharges;
            Hp = maxHp;
            if (cards != null)
            {
                Cards.AddRange(cards);
            }
        }
    }
}
