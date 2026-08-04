using System.IO;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Status;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>부팅이 카드 → 덱·풀 → 캐릭터 순서를 지키고, 실패하면 카탈로그를 내주지 않는지
    /// 잠근다. 리포지토리의 실제 콘텐츠를 읽는다.</summary>
    public class ContentBootstrapTests
    {
        private static string ContentRoot()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            return Path.Combine(directory, "Assets", "StreamingAssets", "Content");
        }

        [Test]
        public void BootstrapLoadsEveryCatalog()
        {
            var result = ContentBootstrap.Load(ContentRoot());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            Assert.AreEqual(26, result.Content.Cards.Ids.Count);
            Assert.AreEqual(2, result.Content.Decks.Ids.Count);
            Assert.AreEqual(1, result.Content.Pools.Ids.Count);
            Assert.AreEqual(2, result.Content.Characters.Ids.Count);
        }

        [Test]
        public void BootstrapResolvesACharacterToItsCards()
        {
            var content = ContentBootstrap.Load(ContentRoot()).Content;

            var memberA = content.Characters.Get("member_a");
            var deck = content.Decks.Get(memberA.Deck);

            Assert.AreEqual(10, deck.Count);
            foreach (var cardId in deck)
            {
                Assert.IsTrue(content.Cards.Cards.ContainsKey(cardId), cardId + "가 없다.");
            }
        }

        [Test]
        public void BootstrapLoadsTheStatusCatalog()
        {
            var content = ContentBootstrap.Load(ContentRoot()).Content;

            Assert.AreEqual(11, content.Statuses.Keys.Count);
            Assert.AreEqual("독", content.Statuses.DisplayNameOf(StatusKeys.Poison));
            Assert.AreEqual(1, content.Statuses.GrowthPerTurnOf(StatusKeys.Poison));
            Assert.AreEqual(2, content.Statuses.ExecutionOrderDeltaOf(StatusKeys.Slow));
        }

        [Test]
        public void BootstrapReportsMissingStatusesBeforeReadingCards()
        {
            var result = ContentBootstrap.Load(
                Path.Combine(Path.GetTempPath(), "fate-weaver-no-such-content"));

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("Statuses", string.Join("\n", result.Errors));
        }

        [Test]
        public void BootstrapFailsWhenTheRootIsMissing()
        {
            var result = ContentBootstrap.Load(
                Path.Combine(Path.GetTempPath(), "fate-weaver-no-such-content"));

            Assert.IsFalse(result.Succeeded);
            Assert.Greater(result.Errors.Count, 0);
            Assert.IsNull(result.Content, "실패하면 카탈로그를 내주지 않는다.");
        }
    }
}
