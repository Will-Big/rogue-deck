using System.Linq;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>콘텐츠에서 시작 파티를 조립하는 경로를 잠근다. ContentDrivenLoadoutTests를 대체한다.</summary>
    public class RunSetupTests
    {
        [Test]
        public void NewRun_builds_each_member_from_the_authored_deck()
        {
            var content = TestContent.Content();

            var run = RunSetup.NewRun(
                content, new[] { "member_a", "member_b" }, runSeed: 7);

            Assert.AreEqual(7, run.RunSeed);
            Assert.AreEqual(0, run.NodesEntered);
            Assert.AreEqual(2, run.Party.Count);
            var a = run.Party[0];
            Assert.AreEqual("member_a", a.Id);
            Assert.AreEqual("파티원 A", a.Name);
            Assert.AreEqual(25, a.MaxHp);
            Assert.AreEqual(a.MaxHp, a.Hp);
            CollectionAssert.AreEqual(
                content.Decks.Get("starter").ToArray(),
                a.Cards.Select(card => card.Id).ToArray());
        }

        [Test]
        public void NewRun_shares_one_definition_per_card_id()
        {
            var run = RunSetup.NewRun(
                TestContent.Content(), new[] { "member_b" }, runSeed: 1);
            var attacks = run.Party[0].Cards.Where(card => card.Id == "cleave").ToArray();

            Assert.AreEqual(2, attacks.Length, "striker 덱은 cleave를 둘 갖는다.");
            Assert.AreSame(attacks[0], attacks[1]);
        }

        [Test]
        public void NewRun_keeps_the_given_party_order()
        {
            var run = RunSetup.NewRun(
                TestContent.Content(), new[] { "member_b", "member_a" }, runSeed: 1);

            CollectionAssert.AreEqual(
                new[] { "member_b", "member_a" }, run.Party.Select(m => m.Id).ToArray());
        }

        [Test]
        public void NewRun_resolves_every_authored_character()
        {
            var content = TestContent.Content();

            var run = RunSetup.NewRun(content, content.Characters.Ids, runSeed: 1);

            CollectionAssert.AreEqual(content.Characters.Ids, run.Party.Select(m => m.Id).ToArray());
            foreach (var member in run.Party)
            {
                Assert.Greater(member.Cards.Count, 0, member.Id + "의 덱이 비었다.");
            }
        }
    }
}
