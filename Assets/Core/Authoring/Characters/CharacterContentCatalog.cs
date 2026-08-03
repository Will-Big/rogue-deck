using System;
using System.Collections.Generic;

namespace FateWeaver.Core.Authoring.Characters
{
    /// <summary>로드된 캐릭터 하나. 저작 타입(CharacterSpec)과 달리 불변이며, 덱 id는 로드 시점에
    /// 존재가 확인된 것이다.</summary>
    public sealed class CharacterContent
    {
        public CharacterContent(string id, string displayName, string deck)
        {
            Id = id;
            DisplayName = displayName;
            Deck = deck;
        }

        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>시작 덱의 id. DeckContentCatalog가 이것을 푼다.</summary>
        public string Deck { get; }
    }

    /// <summary>부팅 시 한 번 만들어져 상주하는 id → CharacterContent 사전. 색 틴트는 표현
    /// 데이터이므로 여기 없다 — Unity의 CharacterAsset이 id → Color를 맡는다(설계 §4.5).</summary>
    public sealed class CharacterContentCatalog
    {
        private readonly Dictionary<string, CharacterContent> _characters;
        private readonly List<string> _ids;

        public CharacterContentCatalog(Dictionary<string, CharacterContent> characters)
        {
            _characters = characters;
            _ids = new List<string>(characters.Keys);
            _ids.Sort(StringComparer.Ordinal);
        }

        /// <summary>정렬된 id 목록. 반복 순서가 사전 구현에 좌우되지 않게 한다(규칙 7).</summary>
        public IReadOnlyList<string> Ids => _ids;

        public CharacterContent Get(string id)
        {
            if (!_characters.TryGetValue(id, out var character))
            {
                throw new KeyNotFoundException("No character content with id '" + id + "'.");
            }

            return character;
        }
    }
}
