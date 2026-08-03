using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회로 만들어져 상주하는 콘텐츠 번들. 상태 카탈로그는 아직 묶지 않는다 —
    /// StatusSpecJsonConverter가 StatusContentDefaults에 의존하므로(계획 3c가 뗀다) 여기 넣으면
    /// "JSON이 원본"이라는 거짓 신호가 된다.</summary>
    public sealed class GameContent
    {
        public GameContent(
            CardContentCatalog cards,
            DeckContentCatalog decks,
            PoolContentCatalog pools,
            CharacterContentCatalog characters)
        {
            Cards = cards;
            Decks = decks;
            Pools = pools;
            Characters = characters;
        }

        public CardContentCatalog Cards { get; }
        public DeckContentCatalog Decks { get; }
        public PoolContentCatalog Pools { get; }
        public CharacterContentCatalog Characters { get; }
    }
}
