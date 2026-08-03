using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>캐릭터의 표현 데이터. id와 색뿐이다 — 표시명과 덱은 콘텐츠이므로
    /// Content/Characters/*.json이 갖는다(설계 §4.5: Unity는 표현만 담당).</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Character")]
    public sealed class CharacterAsset : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private Color _color;

        public string Id => _id;
        public Color Color => _color;
    }
}
