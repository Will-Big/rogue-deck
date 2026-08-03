using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>부팅 시 한 번 만들어져 상주하는 id → 카드 id 목록 사전. 카드 규칙은 담지 않는다 —
    /// 그 원본은 CardContentCatalog 하나다(설계 §4.5). 목록은 저작 순서를 그대로 보존하고 같은
    /// 카드 id가 여러 번 올 수 있다.</summary>
    public sealed class DeckContentCatalog
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _decks;
        private readonly List<string> _ids;

        public DeckContentCatalog(Dictionary<string, IReadOnlyList<string>> decks)
        {
            _decks = decks;
            _ids = new List<string>(decks.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public bool Contains(string id) => _decks.ContainsKey(id);

        /// <summary>덱의 카드 id 목록. 저작 순서 그대로다.</summary>
        public IReadOnlyList<string> Get(string id)
        {
            if (!_decks.TryGetValue(id, out var cards))
            {
                throw new KeyNotFoundException("No deck content with id '" + id + "'.");
            }

            return cards;
        }
    }
}
