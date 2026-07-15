using UnityEngine;

namespace FateWeaver.Unity
{
    [CreateAssetMenu(menuName = "Fate Weaver/Character")]
    public sealed class CharacterAsset : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private Color _color;
        [SerializeField] private DeckAsset _deck;

        public string Id => _id;
        public string DisplayName => _displayName;
        public Color Color => _color;
        public DeckAsset Deck => _deck;
    }
}
