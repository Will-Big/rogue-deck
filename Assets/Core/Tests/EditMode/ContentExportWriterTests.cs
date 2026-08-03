using System.Collections.Generic;
using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Characters;
using FateWeaver.Core.Authoring.Decks;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>내보내기가 Unity 없이 도는지, 그리고 덱·풀·캐릭터 JSON이 공인 목록의 순서를
    /// 그대로 옮기는지 잠근다. 리포지토리의 StreamingAssets에는 쓰지 않는다 — 커밋된 콘텐츠를
    /// 테스트가 덮어쓰면 안 된다. 실제 콘텐츠에 대고 돌리는 경로는 아래 [Explicit] 테스트다.</summary>
    public class ContentExportWriterTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), nameof(ContentExportWriterTests), "Content");
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        private IReadOnlyList<string> WriteAll()
            => ContentExportWriter.WriteAll(_root, PartyPrototypeCharacterSpecs.Build());

        private static string[] JsonIn(string directory)
        {
            var names = Directory.GetFiles(directory, "*.json").Select(Path.GetFileName).ToArray();
            System.Array.Sort(names, System.StringComparer.Ordinal);
            return names;
        }

        [Test]
        public void WriteAllReportsEveryFileItWrote()
        {
            var written = WriteAll();

            Assert.AreEqual(
                StatusContentDefaults.Specs().Count
                    + 2 // Decks/starter.json, Decks/party_prototype.json
                    + 1 // Pools/starter.json
                    + PartyPrototypeCharacterSpecs.Build().Count,
                written.Count,
                string.Join("\n", written));

            foreach (var path in written)
            {
                Assert.IsTrue(File.Exists(path), path + "가 실제로 쓰이지 않았다.");
            }
        }

        /// <summary>계획 3b 이후 카드는 내보내지 않는다 — 등급·태그가 JSON에만 있어 C# 스펙에서
        /// 다시 쓰면 지워지기 때문이다. 이 단언이 그 회귀를 막는다.</summary>
        [Test]
        public void WriteAllDoesNotTouchCards()
        {
            WriteAll();

            Assert.IsFalse(
                Directory.Exists(Path.Combine(_root, CardContentFiles.CardsFolderName)),
                "카드를 다시 쓰면 등급·태그가 지워진다.");
        }

        [Test]
        public void WriteAllFillsEveryContentFolder()
        {
            WriteAll();

            Assert.AreEqual(
                StatusContentDefaults.Specs().Count,
                JsonIn(Path.Combine(_root, CardContentFiles.StatusesFolderName)).Length);
            CollectionAssert.AreEqual(
                new[] { "party_prototype.json", "starter.json" },
                JsonIn(Path.Combine(_root, CardContentFiles.DecksFolderName)));
            CollectionAssert.AreEqual(
                new[] { "starter.json" },
                JsonIn(Path.Combine(_root, CardContentFiles.PoolsFolderName)));
            CollectionAssert.AreEqual(
                new[] { "member_a.json", "member_b.json" },
                JsonIn(Path.Combine(_root, CardContentFiles.CharactersFolderName)));
        }

        [Test]
        public void ExportedStarterDeckKeepsTheAuthoredCardOrder()
        {
            WriteAll();

            var deck = ContentJson.Read<DeckSpec>(File.ReadAllText(
                Path.Combine(_root, CardContentFiles.DecksFolderName, "starter.json")));

            Assert.AreEqual(ContentExportWriter.StarterDeckId, deck.Id);
            CollectionAssert.AreEqual(
                StarterDeckSpecs.Build().Select(spec => spec.Id).ToArray(), deck.Cards);
        }

        [Test]
        public void ExportedPartyPrototypeDeckKeepsRepeatedCardIds()
        {
            WriteAll();

            var deck = ContentJson.Read<DeckSpec>(File.ReadAllText(
                Path.Combine(_root, CardContentFiles.DecksFolderName, "party_prototype.json")));

            CollectionAssert.AreEqual(
                PartyPrototypeDeckSpecs.Build().Select(spec => spec.Id).ToArray(), deck.Cards);
        }

        [Test]
        public void ExportedStarterPoolKeepsTheAuthoredCardOrder()
        {
            WriteAll();

            var pool = ContentJson.Read<PoolSpec>(File.ReadAllText(
                Path.Combine(_root, CardContentFiles.PoolsFolderName, "starter.json")));

            Assert.AreEqual(ContentExportWriter.StarterPoolId, pool.Id);
            CollectionAssert.AreEqual(
                StarterPoolSpecs.Build().Select(spec => spec.Id).ToArray(), pool.Cards);
        }

        [Test]
        public void ExportedCharactersMatchTheRoster()
        {
            WriteAll();

            var memberA = ContentJson.Read<CharacterSpec>(File.ReadAllText(
                Path.Combine(_root, CardContentFiles.CharactersFolderName, "member_a.json")));

            Assert.AreEqual(PartyPrototypeRoster.MemberAId, memberA.Id);
            Assert.AreEqual(PartyPrototypeRoster.MemberAName, memberA.DisplayName);
            Assert.AreEqual(ContentExportWriter.StarterDeckId, memberA.Deck);
        }

        /// <summary>내보낸 덱·풀·캐릭터가 **리포지토리의 실제 카드**에 대고 로드되는지 본다.
        /// 카드는 더 이상 내보내지 않으므로(등급·태그가 JSON에만 있다) 카드 카탈로그는 저장소에서
        /// 읽는다 — 내보낸 id 목록이 진짜 카드를 가리키는지가 이 테스트의 요점이다.</summary>
        [Test]
        public void ExportedContentLoadsAgainstRepositoryCards()
        {
            WriteAll();

            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            var cards = CardContentLoader.Load(
                CardContentFiles.ReadDirectory(Path.Combine(
                    directory, "Assets", "StreamingAssets", "Content",
                    CardContentFiles.CardsFolderName)),
                AuthoringContext.Default());
            Assert.IsTrue(cards.Succeeded, string.Join("\n", cards.Errors));

            var decks = DeckContentLoader.Load(
                CardContentFiles.ReadDirectory(
                    Path.Combine(_root, CardContentFiles.DecksFolderName)),
                cards.Catalog);
            Assert.IsTrue(decks.Succeeded, string.Join("\n", decks.Errors));

            var pools = PoolContentLoader.Load(
                CardContentFiles.ReadDirectory(
                    Path.Combine(_root, CardContentFiles.PoolsFolderName)),
                cards.Catalog);
            Assert.IsTrue(pools.Succeeded, string.Join("\n", pools.Errors));

            var characters = CharacterContentLoader.Load(
                CardContentFiles.ReadDirectory(
                    Path.Combine(_root, CardContentFiles.CharactersFolderName)),
                decks.Catalog);
            Assert.IsTrue(characters.Succeeded, string.Join("\n", characters.Errors));
        }

        /// <summary>리포지토리의 Assets/StreamingAssets/Content에 실제로 내보낸다. 명시적으로
        /// 지목할 때만 돌고 일반 실행에서는 건너뛴다. 계획 3d가 라이터와 함께 이 테스트도 지운다.
        /// <code>
        /// dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 \
        ///   --filter "FullyQualifiedName~ContentExportWriterTests.Export_to_repository"
        /// </code></summary>
        [Test]
        [Explicit]
        public void Export_to_repository()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            var root = Path.Combine(directory, "Assets", "StreamingAssets", "Content");

            var written = ContentExportWriter.WriteAll(root, PartyPrototypeCharacterSpecs.Build());

            TestContext.WriteLine("Exported " + written.Count + " files to " + root);
        }
    }
}
