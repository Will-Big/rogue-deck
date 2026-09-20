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

        /// <summary>이 전투를 시작할 HP. 런에서 이어받은 값이며, 생략하면 최대 HP다 — 런 바깥에서
        /// 세션을 직접 만드는 테스트·시뮬레이션이 그 경로를 쓴다.</summary>
        public int Hp { get; }

        public IReadOnlyList<CardDefinition> Cards { get; }

        public PartyMemberLoadout(
            string id,
            string name,
            int maxHp,
            IReadOnlyList<CardDefinition> cards,
            int? hp = null)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Hp = hp ?? maxHp;
            Cards = cards;
        }
    }
}
