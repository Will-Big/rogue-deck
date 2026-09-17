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
        }

        [Test]
        public void Enemy_cards_use_json_name_fallback()
        {
            // 적 카드 이름은 카드 JSON의 name이 원본이다 — 호출자가 def.Name을 fallback으로 넘긴다.
            foreach (var id in new[] { "goblin_jab", "crude_guard", "sly_jab" })
            {
                Assert.AreEqual("fallback", PlaytestKoreanText.CardName(id, "fallback"), id);
            }
        }

        [Test]
        public void Suffixed_ids_match_by_prefix()
        {
            Assert.AreEqual("찰나의 베기", PlaytestKoreanText.CardName("quick_cut_t1", "fallback"));
        }
    }
}
