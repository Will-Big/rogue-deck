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
        private readonly Dictionary<string, CardSpec> _specs;
        private readonly List<string> _ids;

        public CardContentCatalog(
            Dictionary<string, CardDefinition> cards,
            Dictionary<string, CardSpec> specs)
        {
            _cards = cards;
            _specs = specs;
            _ids = new List<string>(cards.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, CardDefinition> Cards => _cards;

        /// <summary>저작 스펙. 전투 규칙이 쓰지 않는 값(등급·태그)을 CardDefinition에 싣지 않기
        /// 위해 따로 둔다 — 코어의 출력은 이벤트 타임라인뿐이다(규칙 11). 풀 로더가 후보 카드의
        /// 등급·태그를 검사할 때만 쓴다.</summary>
        public IReadOnlyDictionary<string, CardSpec> Specs => _specs;

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
