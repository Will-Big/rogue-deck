using System.Linq;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>검증용 픽스처 카드 두 장의 대상 선택 의미(Self 대 All)를 잠근다. 어느 덱이
    /// 이 카드들을 담았는지가 아니라 카드 자체의 TargetOf 계약을 검증한다 — 덱 구성은
    /// 각 덱의 콘텐츠 테스트(StrikerDeckDataTests 등)가 따로 다룬다.</summary>
    public class FixtureCardTargetingTests
    {
        private static CardDefinition Card(string id) => TestContent.Content().Cards.Get(id);

        [Test]
        public void Owner_block_uses_self_without_direct_target_selection()
        {
            var ownerBlock = Card("fixture_selected_block");

            Assert.AreEqual(
                new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
                ownerBlock.TargetOf(ownerBlock.Effects.Single()));
        }

        [Test]
        public void All_block_does_not_open_direct_target_selection()
        {
            var allBlock = Card("fixture_all_block");

            Assert.AreEqual(
                new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.All),
                allBlock.TargetOf(allBlock.Effects.Single()));
        }
    }
}
