using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Json;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>덱·풀·캐릭터 저작 타입이 ContentJson으로 왕복하는지 잠근다. 세 타입 모두 다형이
    /// 아니므로 전용 컨버터 없이 ContentJson.Settings만 쓴다. 빈 목록·빈 문자열이 살아남는지도
    /// 함께 단언한다 — DefaultValueHandling.Ignore가 기본값을 지우기 때문이다.</summary>
    public class DeckPoolCharacterSpecJsonTests
    {
        [Test]
        public void DeckSpecRoundTripsThroughJson()
        {
            var spec = new DeckSpec
            {
                Id = "starter",
                Cards = new[] { "probing_strike", "delayed_strike", "quick_cover" }
            };

            var read = ContentJson.Read<DeckSpec>(ContentJson.Write(spec));

            Assert.AreEqual(spec.Id, read.Id);
            CollectionAssert.AreEqual(spec.Cards, read.Cards);
        }

        [Test]
        public void DeckSpecKeepsRepeatedCardIds()
        {
            var spec = new DeckSpec
            {
                Id = "party_prototype",
                Cards = new[] { "fixture_attack", "fixture_attack", "fixture_all_block" }
            };

            var read = ContentJson.Read<DeckSpec>(ContentJson.Write(spec));

            CollectionAssert.AreEqual(spec.Cards, read.Cards);
        }

        [Test]
        public void DeckSpecKeepsAnEmptyCardList()
        {
            var spec = new DeckSpec { Id = "empty", Cards = new string[0] };

            var read = ContentJson.Read<DeckSpec>(ContentJson.Write(spec));

            Assert.IsNotNull(read.Cards, "빈 배열이 직렬화에서 사라졌다.");
            Assert.AreEqual(0, read.Cards.Length);
        }

        [Test]
        public void PoolSpecRoundTripsThroughJson()
        {
            var spec = new PoolSpec
            {
                Id = "starter",
                Cards = new[] { "vanguard_slash", "parry_strike" }
            };

            var read = ContentJson.Read<PoolSpec>(ContentJson.Write(spec));

            Assert.AreEqual(spec.Id, read.Id);
            CollectionAssert.AreEqual(spec.Cards, read.Cards);
        }

        [Test]
        public void PoolSpecKeepsAnEmptyCardList()
        {
            var spec = new PoolSpec { Id = "empty", Cards = new string[0] };

            var read = ContentJson.Read<PoolSpec>(ContentJson.Write(spec));

            Assert.IsNotNull(read.Cards, "빈 배열이 직렬화에서 사라졌다.");
            Assert.AreEqual(0, read.Cards.Length);
        }

        [Test]
        public void CharacterSpecRoundTripsThroughJson()
        {
            var spec = new CharacterSpec
            {
                Id = "member_a",
                DisplayName = "파티원 A",
                Deck = "starter"
            };

            var read = ContentJson.Read<CharacterSpec>(ContentJson.Write(spec));

            Assert.AreEqual(spec.Id, read.Id);
            Assert.AreEqual(spec.DisplayName, read.DisplayName);
            Assert.AreEqual(spec.Deck, read.Deck);
        }

        [Test]
        public void CharacterSpecKeepsEmptyStrings()
        {
            var spec = new CharacterSpec { Id = "", DisplayName = "", Deck = "" };

            var read = ContentJson.Read<CharacterSpec>(ContentJson.Write(spec));

            Assert.AreEqual("", read.Id, "빈 문자열이 직렬화에서 사라졌다.");
            Assert.AreEqual("", read.DisplayName, "빈 문자열이 직렬화에서 사라졌다.");
            Assert.AreEqual("", read.Deck, "빈 문자열이 직렬화에서 사라졌다.");
        }
    }
}
