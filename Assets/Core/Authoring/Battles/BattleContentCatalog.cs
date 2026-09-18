using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Battles
{
    public sealed class BattleDefinition
    {
        public BattleDefinition(string id, IReadOnlyList<string> enemies)
        {
            Id = id;
            Enemies = enemies;
        }

        public string Id { get; }

        /// <summary>적 정의 id 목록. 전투 안 id는 "{적 id}#{이 목록의 순번}"이다.</summary>
        public IReadOnlyList<string> Enemies { get; }
    }

    public sealed class BattleContentCatalog
    {
        private readonly Dictionary<string, BattleDefinition> _battles;
        private readonly List<string> _ids;

        public BattleContentCatalog(Dictionary<string, BattleDefinition> battles)
        {
            _battles = battles;
            _ids = new List<string>(battles.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>서수 정렬된 id. 편성 스트림이 이 순서의 인덱스를 뽑으므로 순서가 시드 결과의 일부다.</summary>
        public IReadOnlyList<string> Ids => _ids;

        public BattleDefinition Get(string id)
        {
            if (id == null || !_battles.TryGetValue(id, out var battle))
            {
                throw new KeyNotFoundException("No battle content with id '" + id + "'.");
            }

            return battle;
        }
    }
}
