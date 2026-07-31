using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 시 한 번 만들어져 상주하는 id → CardDefinition 사전. 같은 카드를 여러 장
    /// 소유해도 정의 객체는 하나이고, 소유 카드는 이것을 참조한다(설계 §4.5).</summary>
    public sealed class CardContentCatalog
    {
        private readonly Dictionary<string, CardDefinition> _cards;
        private readonly List<string> _ids;

        public CardContentCatalog(Dictionary<string, CardDefinition> cards)
        {
            _cards = cards;
            _ids = new List<string>(cards.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, CardDefinition> Cards => _cards;

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public CardDefinition Get(string id)
        {
            if (!_cards.TryGetValue(id, out var card))
            {
                throw new KeyNotFoundException("No card content with id '" + id + "'.");
            }

            return card;
        }
    }
}
