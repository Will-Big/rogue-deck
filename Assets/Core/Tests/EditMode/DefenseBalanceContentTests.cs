using FateWeaver.Core.Cards;
using NUnit.Framework;
using static FateWeaver.Tests.CardContentAssertions;

namespace FateWeaver.Tests
{
    /// <summary>방어 한 장이 적 공격 한 장을 정확히 0으로 만들지 않는다는 것이 이 설계의 축이다
    /// (전투 템포 설계 변경 2). 수치를 되돌리면 전투가 다시 상쇄로 굳는다.</summary>
    public class DefenseBalanceContentTests
    {
        private static CardDefinition Card(string id) => TestContent.Content().Cards.Get(id);

        private static int Block(string id) => BlockOf(Card(id));

        private static int Damage(string id) => DamageOf(Card(id));

        [TestCase("quick_cover", 3)]
        [TestCase("early_guard", 3)]
        [TestCase("toxic_reclaim", 3)]
        public void Guard_cards_grant_three_block(string id, int expected)
        {
            Assert.AreEqual(expected, Block(id));
        }

        [Test]
        public void Goblin_jab_out_damages_a_single_guard_card()
        {
            Assert.AreEqual(5, Damage("goblin_jab"));
            Assert.AreEqual(
                2, Damage("goblin_jab") - Block("quick_cover"),
                "방어 한 장을 뚫고 2가 들어와야 한다.");
        }
    }
}
