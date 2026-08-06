using FateWeaver.Core.Authoring;
using FateWeaver.Core.Authoring.Json;
using FateWeaver.Core.Cards;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>등급·태그가 CardSpec에 실려 JSON을 왕복하는지 잠근다. 등급은 0번 값(None)이
    /// 생략되지만 그것이 정상 상태다 — fixture 카드는 등급을 갖지 않는다.</summary>
    public class CardSpecGradeTagTests
    {
        private static ExecutionCardSpec Base() => new ExecutionCardSpec
        {
            Id = "sample", Name = "표본", Side = Side.Player, Category = CardCategory.Execution
        };

        [Test]
        public void GradeAndTagsSurviveTheRoundTrip()
        {
            var spec = Base();
            spec.Grade = CardGrade.Common;
            spec.Tags = new[] { "시작", "실행력" };

            var read = ContentJson.Read<CardSpec>(ContentJson.Write(spec));

            Assert.AreEqual(CardGrade.Common, read.Grade);
            CollectionAssert.AreEqual(spec.Tags, read.Tags);
        }

        [Test]
        public void MissingGradeReadsBackAsNone()
        {
            var spec = Base();
            spec.Tags = new string[0];

            var json = ContentJson.Write(spec);
            var read = ContentJson.Read<CardSpec>(json);

            Assert.AreEqual(CardGrade.None, read.Grade);
            StringAssert.DoesNotContain("\"grade\"", json, "None은 생략되어야 한다.");
        }

        [Test]
        public void AnEmptyTagListSurvives()
        {
            var spec = Base();
            spec.Tags = new string[0];

            var read = ContentJson.Read<CardSpec>(ContentJson.Write(spec));

            Assert.IsNotNull(read.Tags, "빈 배열이 직렬화에서 사라졌다.");
            Assert.AreEqual(0, read.Tags.Length);
        }
    }
}
