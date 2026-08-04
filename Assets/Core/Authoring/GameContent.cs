using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Statuses;

namespace FateWeaver.Core.Authoring
{
    /// <summary>부팅 1회로 만들어져 상주하는 콘텐츠 번들. 상태 규칙의 유일한 원본은
    /// Content/Statuses/*.json이며 여기 실려 전투·설명 양쪽에 같은 인스턴스로 주입된다.</summary>
    public sealed class GameContent
    {
        public GameContent(
            StatusContentCatalog statuses,
            CardContentCatalog cards,
            DeckContentCatalog decks,
            PoolContentCatalog pools,
            CharacterContentCatalog characters)
        {
            Statuses = statuses;
            Cards = cards;
            Decks = decks;
            Pools = pools;
            Characters = characters;
        }

        public StatusContentCatalog Statuses { get; }
        public CardContentCatalog Cards { get; }
        public DeckContentCatalog Decks { get; }
        public PoolContentCatalog Pools { get; }
        public CharacterContentCatalog Characters { get; }
    }
}
