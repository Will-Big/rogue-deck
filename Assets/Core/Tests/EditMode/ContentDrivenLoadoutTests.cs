using System.IO;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>콘텐츠에서 조립한 파티 로드아웃이 공인 목록과 같은지 잠근다. 계획 3b가
    /// BattleScreenController를 이 경로로 옮긴다.</summary>
    public class ContentDrivenLoadoutTests
    {
        private const int SampleMaxHp = 30;

        private static GameContent Content()
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory != null && !Directory.Exists(Path.Combine(directory, "Assets")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.IsNotNull(directory, "저장소 루트를 찾지 못했다.");
            var result = ContentBootstrap.Load(
                Path.Combine(directory, "Assets", "StreamingAssets", "Content"));
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Content;
        }

        [Test]
        public void LoadoutForBuildsTheAuthoredDeck()
        {
            var content = Content();

            var loadout = ContentLoadouts.For(content, "member_a", SampleMaxHp);

            Assert.AreEqual("member_a", loadout.Id);
            Assert.AreEqual("파티원 A", loadout.Name);
            Assert.AreEqual(SampleMaxHp, loadout.MaxHp);
            CollectionAssert.AreEqual(
                content.Decks.Get("starter").ToArray(),
                loadout.Cards.Select(card => card.Id).ToArray());
        }

        [Test]
        public void LoadoutSharesOneDefinitionPerCardId()
        {
            var content = Content();

            var loadout = ContentLoadouts.For(content, "member_b", SampleMaxHp);
            var attacks = loadout.Cards.Where(card => card.Id == "fixture_attack").ToArray();

            Assert.AreEqual(2, attacks.Length, "party_prototype 덱은 fixture_attack을 둘 갖는다.");
            Assert.AreSame(
                attacks[0], attacks[1],
                "같은 카드 id는 정의 객체 하나를 참조해야 한다(설계 §4.5).");
        }

        [Test]
        public void EveryRosterMemberResolves()
        {
            var content = Content();

            foreach (var id in content.Characters.Ids)
            {
                var loadout = ContentLoadouts.For(content, id, SampleMaxHp);

                Assert.AreEqual(id, loadout.Id);
                Assert.Greater(loadout.Cards.Count, 0, id + "의 덱이 비었다.");
            }
        }
    }
}
