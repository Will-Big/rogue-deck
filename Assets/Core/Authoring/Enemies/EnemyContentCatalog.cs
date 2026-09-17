using System;
using System.Collections.Generic;
using FateWeaver.Core.Enemies;

namespace FateWeaver.Core.Authoring.Enemies
{
    /// <summary>로드된 적 하나. 묶음의 카드는 카드 카탈로그의 정의 객체를 공유한다.</summary>
    public sealed class EnemyDefinition
    {
        public EnemyDefinition(
            string id, string displayName, int maxHp, EnemyPolicyKey policy, IReadOnlyList<EnemyCardBundle> bundles)
        {
            Id = id;
            DisplayName = displayName;
            MaxHp = maxHp;
            Policy = policy;
            Bundles = bundles;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int MaxHp { get; }
        public EnemyPolicyKey Policy { get; }
        public IReadOnlyList<EnemyCardBundle> Bundles { get; }
    }

    /// <summary>부팅 시 한 번 만들어져 상주하는 id → EnemyDefinition 사전.</summary>
    public sealed class EnemyContentCatalog
    {
        private readonly Dictionary<string, EnemyDefinition> _enemies;
        private readonly List<string> _ids;

        public EnemyContentCatalog(Dictionary<string, EnemyDefinition> enemies)
        {
            _enemies = enemies;
            _ids = new List<string>(enemies.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public bool Contains(string id) => id != null && _enemies.ContainsKey(id);

        public EnemyDefinition Get(string id)
        {
            if (id == null || !_enemies.TryGetValue(id, out var enemy))
            {
                throw new KeyNotFoundException("No enemy content with id '" + id + "'.");
            }

            return enemy;
        }
    }
}
