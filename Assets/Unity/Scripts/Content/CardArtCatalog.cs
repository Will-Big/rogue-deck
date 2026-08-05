using System;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>id → Sprite 매핑. 카드 규칙은 JSON이 갖고 Unity는 표현만 담당한다(설계 §4.5).
    /// 플레이어 카드는 색상 틴트만 쓰므로 여기 항목은 아트가 실제로 있는 카드뿐이다.</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Card Art Catalog", fileName = "CardArt")]
    public sealed class CardArtCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            public Sprite Art;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public int Count => _entries == null ? 0 : _entries.Length;

        public Sprite ArtFor(string id)
        {
            var entries = _entries;
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry.Id == id)
                {
                    return entry.Art;
                }
            }

            return null;
        }
    }
}
