using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>커밋된 덱·풀·캐릭터 JSON을 공인 목록과 대조해 잠근다. 지금은 두 원본(C# 스펙과
    /// JSON)이 공존하므로 교차 대조가 가능하다 — 계획 3d가 C# 스펙을 지울 때 이 테스트는 골든
    /// 문자열 배열로 바뀐다.</summary>
    public class DeckPoolCharacterContentTests
    {
        private const int StarterDeckSize = 10;
        private const int StarterPoolSize = 22;

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
        public void StarterDeckJsonMatchesTheAuthoredTenCards()
        {
            var cards = Decks().Get(ContentExportWriter.StarterDeckId);

            Assert.AreEqual(StarterDeckSize, cards.Count);
            CollectionAssert.AreEqual(
                StarterDeckSpecs.Build().Select(spec => spec.Id).ToArray(), cards);
        }

        [Test]
        public void PartyPrototypeDeckJsonMatchesTheAuthoredDeck()
        {
            CollectionAssert.AreEqual(
                PartyPrototypeDeckSpecs.Build().Select(spec => spec.Id).ToArray(),
                Decks().Get(ContentExportWriter.PartyPrototypeDeckId));
        }

        [Test]
        public void StarterPoolJsonMatchesTheAuthoredTwentyTwoCards()
        {
            var cards = Pools().Get(ContentExportWriter.StarterPoolId);

            Assert.AreEqual(StarterPoolSize, cards.Count);
            CollectionAssert.AreEqual(
                StarterPoolSpecs.Build().Select(spec => spec.Id).ToArray(), cards);
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
                ContentExportWriter.StarterDeckId,
                characters.Get(PartyPrototypeRoster.MemberAId).Deck);
            Assert.AreEqual(
                ContentExportWriter.PartyPrototypeDeckId,
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
