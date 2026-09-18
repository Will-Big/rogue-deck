using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>콘텐츠에서 새 런의 시작 상태를 조립한다. 같은 카드 id는 정의 객체 하나를 공유한다
    /// (카드 카탈로그가 id마다 하나를 든다).</summary>
    public static class RunSetup
    {
        public static RunState NewRun(
            GameContent content,
            IReadOnlyList<string> characterIds,
            int runSeed)
        {
            var party = new List<RunMember>();
            foreach (var id in characterIds)
            {
                var character = content.Characters.Get(id);
                party.Add(new RunMember(
                    character.Id,
                    character.DisplayName,
                    character.MaxHp,
                    DeckCards(content, character.Deck)));
            }

            return new RunState(party, runSeed);
        }

        private static List<CardDefinition> DeckCards(GameContent content, string deckId)
        {
            var cards = new List<CardDefinition>();
            foreach (var cardId in content.Decks.Get(deckId))
            {
                cards.Add(content.Cards.Get(cardId));
            }

            return cards;
        }
    }
}
