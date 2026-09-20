using FateWeaver.Core.Cards;
using NUnit.Framework;
using static FateWeaver.Tests.CardContentAssertions;

namespace FateWeaver.Tests
{
    /// <summary>파티원 B의 직접 피해 카드 5종이 설계한 수치대로 저작됐는지 잠근다(전투 템포 설계 변경 1).
    /// 수치를 바꾸려면 설계 문서와 함께 바꾼다.</summary>
    public class StrikerCardContentTests
    {
        private static CardDefinition Card(string id) => TestContent.Content().Cards.Get(id);

        [TestCase("cleave", 4, 1, 4)]
        [TestCase("flank_jab", 2, 1, 3)]
        [TestCase("heavy_swing", 6, 2, 7)]
        [TestCase("shield_bash", 5, 1, 3)]
        public void Attack_cards_have_the_designed_order_cost_and_damage(
            string id, int order, int cost, int damage)
        {
            var card = Card(id);

            Assert.AreEqual(Side.Player, card.Side);
            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(order, card.BaseExecutionOrder, id + "의 실행 순서");
            Assert.AreEqual(cost, card.EnergyCost, id + "의 비용");
            Assert.AreEqual(damage, DamageOf(card), id + "의 피해");
        }

        [Test]
        public void Shield_bash_also_grants_block_two()
        {
            Assert.AreEqual(2, BlockOf(Card("shield_bash")));
        }

        /// <summary>짝 전투의 표적 선택이 이 한 장에 걸려 있다(설계 변경 1, 사용자 결정 A안).
        /// 시작 덱의 다른 적 대상 카드는 전부 FrontOne이고 BackOne·All 카드는 풀에만 있다.</summary>
        [Test]
        public void Flank_jab_is_the_only_back_row_attack_in_the_striker_deck()
        {
            Assert.AreEqual(CardTargetRange.BackOne, Card("flank_jab").EnemyTarget);
            Assert.AreEqual(CardTargetRange.FrontOne, Card("cleave").EnemyTarget);
            Assert.AreEqual(CardTargetRange.FrontOne, Card("heavy_swing").EnemyTarget);
            Assert.AreEqual(CardTargetRange.FrontOne, Card("shield_bash").EnemyTarget);
        }

        [Test]
        public void Brace_is_a_block_three_card_with_no_damage()
        {
            var brace = Card("brace");

            Assert.AreEqual(4, brace.BaseExecutionOrder, "brace의 실행 순서");
            Assert.AreEqual(3, BlockOf(brace));
            Assert.AreEqual(0, DamageOf(brace));
            Assert.AreEqual(1, brace.EnergyCost);
        }
    }
}
