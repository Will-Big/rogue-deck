using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardDescriptionTests
    {
        [Test]
        public void Cards_have_curated_names()
        {
            Assert.AreEqual("베기", PlaytestKoreanText.CardName("slash", "fallback"));
            Assert.AreEqual("찌르기", PlaytestKoreanText.CardName("goblin_jab", "fallback"));
            Assert.AreEqual("조잡한 방어", PlaytestKoreanText.CardName("crude_guard", "fallback"));
            Assert.AreEqual("약삭빠른 찌르기", PlaytestKoreanText.CardName("sly_jab", "fallback"));
        }

        [Test]
        public void Suffixed_ids_match_by_prefix()
        {
            Assert.AreEqual("찰나의 베기", PlaytestKoreanText.CardName("quick_cut_t1", "fallback"));
        }
    }
}
