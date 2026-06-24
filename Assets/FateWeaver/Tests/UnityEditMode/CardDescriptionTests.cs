using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardDescriptionTests
    {
        [Test]
        public void Player_cards_have_curated_text()
        {
            Assert.AreEqual("피해 4.", PlaytestKoreanText.CardDescription("slash"));
            Assert.AreEqual("방어 4.", PlaytestKoreanText.CardDescription("guard"));
            Assert.AreEqual("피해 4. 바로 앞이 적 공격이면 +5.", PlaytestKoreanText.CardDescription("counter_stance"));
            Assert.AreEqual("방어 2. 바로 뒤가 적 공격이면 +5.", PlaytestKoreanText.CardDescription("cover"));
        }

        [Test]
        public void Goblin_cards_have_updated_names_and_descriptions()
        {
            Assert.AreEqual("찌르기", PlaytestKoreanText.CardName("goblin_jab", "fallback"));
            Assert.AreEqual("피해 4.", PlaytestKoreanText.CardDescription("goblin_jab"));
            Assert.AreEqual("조잡한 방어", PlaytestKoreanText.CardName("crude_guard", "fallback"));
            Assert.AreEqual("방어 3.", PlaytestKoreanText.CardDescription("crude_guard"));
            Assert.AreEqual("약삭빠른 찌르기", PlaytestKoreanText.CardName("sly_jab", "fallback"));
            Assert.AreEqual(
                "피해 3. 앞에 플레이어 카드가 없으면 +3.",
                PlaytestKoreanText.CardDescription("sly_jab"));
        }

        [Test]
        public void Suffixed_ids_match_by_prefix()
        {
            Assert.AreEqual(
                "피해 2. 첫 발동이면 피해 8.",
                PlaytestKoreanText.CardDescription("quick_cut_t1"));
        }

        [Test]
        public void Unknown_id_returns_empty()
        {
            Assert.AreEqual(string.Empty, PlaytestKoreanText.CardDescription("nope"));
        }
    }
}
