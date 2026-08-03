using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Decks
{
    /// <summary>부팅 시 한 번 만들어져 상주하는 id → 후보 카드 id 목록 사전. 덱과 달리 같은 카드
    /// id가 두 번 오지 않는다 — 그 판정은 PoolContentLoader가 로드 시점에 끝낸다.</summary>
    public sealed class PoolContentCatalog
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _pools;
        private readonly List<string> _ids;

        public PoolContentCatalog(Dictionary<string, IReadOnlyList<string>> pools)
        {
            _pools = pools;
            _ids = new List<string>(pools.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public bool Contains(string id) => _pools.ContainsKey(id);

        /// <summary>풀의 후보 카드 id 목록. 저작 순서 그대로다.</summary>
        public IReadOnlyList<string> Get(string id)
        {
            if (!_pools.TryGetValue(id, out var cards))
            {
                throw new KeyNotFoundException("No pool content with id '" + id + "'.");
            }

            return cards;
        }
    }
}
