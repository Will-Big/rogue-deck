using System.Linq;
using FateWeaver.Core.Authoring;
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

        [Test]
        public void Grade_and_tags_remain_Unity_only_metadata()
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
            Assert.IsNull(typeof(CardSpec).GetField("Grade"));
            Assert.IsNull(typeof(CardSpec).GetField("Tags"));
        }
    }
}
