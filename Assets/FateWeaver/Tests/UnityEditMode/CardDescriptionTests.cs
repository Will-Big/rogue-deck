using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardDescriptionTests
    {
        [Test]
        public void Player_cards_have_curated_text()
        {
            Assert.AreEqual("피해 2.", PlaytestKoreanText.CardDescription("slash"));
            Assert.AreEqual(
                "방어 2. 바로 앞에서 적이 공격했다면 피해 7 (3번째 안이면 +2).",
                PlaytestKoreanText.CardDescription("counter"));
        }

        [Test]
        public void Suffixed_ids_match_by_prefix()
        {
            Assert.AreEqual(
                "피해 2. 이번 턴에 가장 먼저 발동하면 대신 피해 10.",
                PlaytestKoreanText.CardDescription("quick_cut_t1"));
        }

        [Test]
        public void Unknown_id_returns_empty()
        {
            Assert.AreEqual(string.Empty, PlaytestKoreanText.CardDescription("nope"));
        }
    }
}
