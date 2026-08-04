using System.IO;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>커밋된 덱·풀·캐릭터 JSON을 골든 문자열 배열과 대조해 잠근다. JSON이 유일 원본이므로
    /// 골든은 JSON 파일 자체에서 옮겨 적은 값이다 — C# 스펙과는 더 이상 대조하지 않는다.</summary>
    public class DeckPoolCharacterContentTests
    {
        private const int StarterDeckSize = 10;
        private const int StarterPoolSize = 22;

        /// <summary>Decks/starter.json·Decks/party_prototype.json·Pools/starter.json의 id
        /// 필드를 그대로 옮겨 적은 값이다. JSON이 유일 원본이라 다른 클래스의 상수를 빌려 쓰지
        /// 않는다.</summary>
        private const string StarterDeckId = "starter";
        private const string PartyPrototypeDeckId = "party_prototype";
        private const string StarterPoolId = "starter";

        /// <summary>추첨으로 고정된 10장. 순서까지 계약이다 — 무작위 시작 덱 설계 §3이
        /// 역할 순서로 고정한다고 정했다. Decks/starter.json에서 그대로 옮겨 적었다.</summary>
        private static readonly string[] StarterDeckGolden =
        {
            "probing_strike", "delayed_strike", "quick_cover", "early_guard", "breather",
            "hasten", "toxic_reclaim", "early_onset", "spore_veil", "last_drop"
        };

        /// <summary>fixture_* 6장. Decks/party_prototype.json에서 그대로 옮겨 적었다.</summary>
        private static readonly string[] PartyPrototypeDeckGolden =
        {
            "fixture_attack", "fixture_attack", "fixture_selected_block", "fixture_selected_block",
            "fixture_all_block", "fixture_move_forward"
        };

        /// <summary>풀 22장. Pools/starter.json에서 그대로 옮겨 적었다.</summary>
        private static readonly string[] StarterPoolGolden =
        {
            "vanguard_slash", "parry_strike", "hasten", "probing_strike", "quick_cover",
            "delay", "delayed_strike", "early_guard", "crossover", "riposte", "foresight",
            "breather", "venom_thrust", "last_drop", "spore_veil", "spread_culture",
            "toxic_reclaim", "condensed_burst", "distill", "early_onset", "stable_culture",
            "posthumous_spread"
        };

        private static string Folder(string name) => Path.Combine(TestContent.Root(), name);

        private static CardContentCatalog Cards()
        {
            var result = CardContentLoader.Load(
                CardContentFiles.ReadDirectory(Folder(CardContentFiles.CardsFolderName)),
                AuthoringContext.Default());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        private static DeckContentCatalog Decks()
        {
            var result = DeckContentLoader.Load(
                CardContentFiles.ReadDirectory(Folder(CardContentFiles.DecksFolderName)),
                Cards());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        private static PoolContentCatalog Pools()
        {
            var result = PoolContentLoader.Load(
                CardContentFiles.ReadDirectory(Folder(CardContentFiles.PoolsFolderName)),
                Cards());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        private static CharacterContentCatalog Characters()
        {
            var result = CharacterContentLoader.Load(
                CardContentFiles.ReadDirectory(Folder(CardContentFiles.CharactersFolderName)),
                Decks());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        [Test]
        public void StarterDeckJsonMatchesTheGoldenTenCards()
        {
            var cards = Decks().Get(StarterDeckId);

            Assert.AreEqual(StarterDeckSize, cards.Count);
            CollectionAssert.AreEqual(StarterDeckGolden, cards);
        }

        [Test]
        public void PartyPrototypeDeckJsonMatchesTheGoldenDeck()
        {
            CollectionAssert.AreEqual(
                PartyPrototypeDeckGolden,
                Decks().Get(PartyPrototypeDeckId));
        }

        [Test]
        public void StarterPoolJsonMatchesTheGoldenTwentyTwoCards()
        {
            var cards = Pools().Get(StarterPoolId);

            Assert.AreEqual(StarterPoolSize, cards.Count);
            CollectionAssert.AreEqual(StarterPoolGolden, cards);
        }

        [Test]
        public void CharacterJsonMatchesTheRoster()
        {
            var characters = Characters();

            CollectionAssert.AreEqual(
                new[] { PartyPrototypeRoster.MemberAId, PartyPrototypeRoster.MemberBId },
                characters.Ids);
            Assert.AreEqual(
                PartyPrototypeRoster.MemberAName,
                characters.Get(PartyPrototypeRoster.MemberAId).DisplayName);
            Assert.AreEqual(
                PartyPrototypeRoster.MemberBName,
                characters.Get(PartyPrototypeRoster.MemberBId).DisplayName);
        }

        [Test]
        public void CharacterJsonPointsAtTheRosterDecks()
        {
            var characters = Characters();

            Assert.AreEqual(
                StarterDeckId,
                characters.Get(PartyPrototypeRoster.MemberAId).Deck);
            Assert.AreEqual(
                PartyPrototypeDeckId,
                characters.Get(PartyPrototypeRoster.MemberBId).Deck);
        }

        [Test]
        public void EveryCatalogLoadsTogetherWithoutErrors()
        {
            Assert.IsNotNull(Cards());
            Assert.IsNotNull(Decks());
            Assert.IsNotNull(Pools());
            Assert.IsNotNull(Characters());
        }

        [Test]
        public void EveryContentFileHasAUnityMetaSibling()
        {
            var folders = new[]
            {
                CardContentFiles.DecksFolderName,
                CardContentFiles.PoolsFolderName,
                CardContentFiles.CharactersFolderName
            };

            foreach (var folder in folders)
            {
                foreach (var path in Directory.GetFiles(Folder(folder), "*.json"))
                {
                    Assert.IsTrue(
                        File.Exists(path + ".meta"),
                        path + "에 1:1 대응하는 .meta가 없다 (규칙 16).");
                }
            }
        }
    }
}
