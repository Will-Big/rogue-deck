using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardAssetAuthoringTests
    {
        private CardAsset _card;

        [SetUp]
        public void SetUp()
        {
            _card = ScriptableObject.CreateInstance<CardAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_card);
        }

        [Test]
        public void ToSpec_preserves_intervention_target_side_and_adjacency()
        {
            var serialized = new SerializedObject(_card);
            serialized.FindProperty("_interventionTargetSide").enumValueIndex =
                (int)InterventionTargetSideRef.Enemy;
            serialized.FindProperty("_interventionRequireAdjacent").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var spec = _card.ToSpec();

            Assert.AreEqual(InterventionTargetSideRef.Enemy, spec.InterventionTargetSide);
            Assert.IsTrue(spec.InterventionRequireAdjacent);
        }

        /// <summary>등급·태그는 계획 3b 전까지 Unity 전용 메타데이터였다. 이제 CardSpec을 거쳐
        /// 카드 JSON으로 간다 — 풀 구성이 콘텐츠이지 표현이 아니기 때문이다. 옛 불변식(CardSpec에
        /// 두 필드가 없다)을 잠그던 테스트를 새 불변식으로 뒤집었다.</summary>
        [Test]
        public void Grade_and_tags_flow_into_the_card_spec()
        {
            var serialized = new SerializedObject(_card);
            serialized.FindProperty("_grade").enumValueIndex = (int)CardGrade.Common;
            var tags = serialized.FindProperty("_tags");
            tags.arraySize = 2;
            tags.GetArrayElementAtIndex(0).stringValue = "시작";
            tags.GetArrayElementAtIndex(1).stringValue = "실행력";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(CardGrade.Common, _card.Grade);
            CollectionAssert.AreEqual(new[] { "시작", "실행력" }, _card.Tags.ToArray());

            var spec = _card.ToSpec();

            Assert.AreEqual(CardGrade.Common, spec.Grade);
            CollectionAssert.AreEqual(new[] { "시작", "실행력" }, spec.Tags);
        }
    }
}
