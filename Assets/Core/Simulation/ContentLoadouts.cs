using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>캐릭터 id 하나를 콘텐츠에서 파티 로드아웃으로 편다. 같은 카드 id는 카탈로그의
    /// 정의 객체 하나를 참조한다 — 소유 카드가 정의를 복제하지 않는다(설계 §4.5).
    ///
    /// FateWeaver.Core가 아니라 여기 있는 이유는 PartyMemberLoadout이 이 어셈블리에 있고
    /// 코어가 이 어셈블리를 참조하지 않기 때문이다(asmdef 경계).</summary>
    public static class ContentLoadouts
    {
        public static PartyMemberLoadout For(GameContent content, string characterId, int maxHp)
        {
            var character = content.Characters.Get(characterId);
            var cards = new List<CardDefinition>();
            foreach (var cardId in content.Decks.Get(character.Deck))
            {
                cards.Add(content.Cards.Get(cardId));
            }

            return new PartyMemberLoadout(character.Id, character.DisplayName, maxHp, cards);
        }
    }
}
