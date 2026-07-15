using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class PlaytestKoreanTextTests
    {
        [Test]
        public void Maps_playtest_domain_text_to_Korean()
        {
            Assert.AreEqual(
                "8장 3턴 도입부",
                PlaytestKoreanText.ScenarioName("chapter-8-three-turn-opening", "fallback"));
            Assert.AreEqual("표식 연계", PlaytestKoreanText.ScenarioName("mark-combo", "fallback"));
            Assert.AreEqual("선제 찌르기", PlaytestKoreanText.CardName("preemptive_thrust_t1", "fallback"));
            Assert.AreEqual("찰나의 베기", PlaytestKoreanText.CardName("quick_cut_t2", "fallback"));
            Assert.AreEqual("플레이어", PlaytestKoreanText.SideName(Side.Player));
            Assert.AreEqual("성공", PlaytestKoreanText.ConditionName(ConditionTier.Success));
            Assert.AreEqual("승리", PlaytestKoreanText.OutcomeName(Outcome.Win));
            Assert.AreEqual("방어", PlaytestKoreanText.StatusName(StatusKeys.Block));
            Assert.AreEqual("실행 순서 변경", PlaytestKoreanText.InterventionActionName(InterventionActionKeys.ChangeExecutionOrder));
            Assert.AreEqual("fallback", PlaytestKoreanText.CardName("unknown", "fallback"));
        }

        [Test]
        public void Party_owner_name_has_one_localized_source()
        {
            Assert.AreEqual("파티", PlaytestKoreanText.PartyOwnerName());
        }
    }
}
