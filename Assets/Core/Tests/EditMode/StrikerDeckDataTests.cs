using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class StrikerDeckDataTests
    {
        // GameContent를 클래스 상태로 캐시하지 않는다 — Statuses의 Rules가 가변이라 인스턴스를
        // 공유하면 다른 테스트의 변경이 샐 수 있다(TestContent.cs:35-46). 호출마다 새로 읽는다.
        private static IReadOnlyList<CardDefinition> StrikerDeckCards()
        {
            var content = TestContent.Content();
            var cards = new List<CardDefinition>();
            foreach (var id in content.Decks.Get("striker"))
            {
                cards.Add(content.Cards.Get(id));
            }

            return cards;
        }

        [Test]
        public void Striker_deck_is_direct_damage_with_one_guard()
        {
            var cards = StrikerDeckCards();

            Assert.AreEqual(6, cards.Count);
            Assert.AreEqual(2, cards.Count(card => card.Id == "cleave"));
            Assert.IsTrue(
                cards.All(card => !card.Name.StartsWith("[검증]")),
                "픽스처 카드가 실제 덱에 남아 있으면 안 된다.");
        }
    }
}
