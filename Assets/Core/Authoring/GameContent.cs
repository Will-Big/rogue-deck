using FateWeaver.Core.Authoring.Battles;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Enemies;
using FateWeaver.Core.Authoring.Rules;
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
            CharacterContentCatalog characters,
            EnemyContentCatalog enemies,
            BattleContentCatalog battles,
            CombatRules combatRules)
        {
            Statuses = statuses;
            Cards = cards;
            Decks = decks;
            Pools = pools;
            Characters = characters;
            Enemies = enemies;
            Battles = battles;
            CombatRules = combatRules;
        }

        public StatusContentCatalog Statuses { get; }
        public CardContentCatalog Cards { get; }
        public DeckContentCatalog Decks { get; }
        public PoolContentCatalog Pools { get; }
        public CharacterContentCatalog Characters { get; }
        public EnemyContentCatalog Enemies { get; }
        public BattleContentCatalog Battles { get; }
        public CombatRules CombatRules { get; }
    }
}
