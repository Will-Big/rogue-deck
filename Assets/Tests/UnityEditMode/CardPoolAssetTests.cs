using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardPoolAssetTests
    {
        private readonly List<ScriptableObject> _objects = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _objects)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Valid_pool_converts_every_card_in_stored_order()
        {
            var pool = Pool(
                "starter_pool",
                Card("alpha", CardGrade.Common, "시작", "공격"),
                Card("beta", CardGrade.Common, "시작", "방어"));

            CollectionAssert.IsEmpty(pool.Validate());
            CollectionAssert.AreEqual(
                new[] { "alpha", "beta" },
                pool.ToSpecs().Select(spec => spec.Id).ToArray());
        }

        [Test]
        public void Validation_rejects_blank_pool_id_and_null_card()
        {
            var pool = Pool(" ", Card("alpha", CardGrade.Common, "시작"), null);

            var errors = pool.Validate();

            Assert.That(errors.Any(error => error.Contains("pool id")));
            Assert.That(errors.Any(error => error.Contains("null")));
        }

        [Test]
        public void Validation_rejects_blank_and_duplicate_card_ids()
        {
            var pool = Pool(
                "starter_pool",
                Card("", CardGrade.Common, "시작"),
                Card("same", CardGrade.Common, "시작"),
                Card("same", CardGrade.Common, "시작"));

            var errors = pool.Validate();

            Assert.That(errors.Any(error => error.Contains("blank card id")));
            Assert.That(errors.Any(error => error.Contains("duplicate card id")));
        }

        [Test]
        public void Validation_rejects_missing_grade_and_invalid_tags()
        {
            var pool = Pool(
                "starter_pool",
                Card("missing_grade", CardGrade.None, "시작"),
                Card("empty_tag", CardGrade.Common, "시작", " "),
                Card("duplicate_tag", CardGrade.Common, "시작", "시작"));

            var errors = pool.Validate();

            Assert.That(errors.Any(error => error.Contains("grade")));
            Assert.That(errors.Any(error => error.Contains("empty tag")));
            Assert.That(errors.Any(error => error.Contains("duplicate tag")));
        }

        [Test]
        public void ToSpecs_rejects_the_entire_invalid_pool()
        {
            var pool = Pool("starter_pool", Card("same", CardGrade.Common, "시작"), null);

            var exception = Assert.Throws<InvalidOperationException>(() => pool.ToSpecs());

            StringAssert.Contains("null", exception.Message);
        }

        private CardAsset Card(string id, CardGrade grade, params string[] tags)
        {
            var card = ScriptableObject.CreateInstance<CardAsset>();
            _objects.Add(card);
            card.Id = id;

            var serialized = new SerializedObject(card);
            serialized.FindProperty("_grade").enumValueIndex = (int)grade;
            var serializedTags = serialized.FindProperty("_tags");
            serializedTags.arraySize = tags.Length;
            for (int i = 0; i < tags.Length; i++)
            {
                serializedTags.GetArrayElementAtIndex(i).stringValue = tags[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        private CardPoolAsset Pool(string id, params CardAsset[] cards)
        {
            var pool = ScriptableObject.CreateInstance<CardPoolAsset>();
            _objects.Add(pool);

            var serialized = new SerializedObject(pool);
            serialized.FindProperty("_id").stringValue = id;
            var serializedCards = serialized.FindProperty("_cards");
            serializedCards.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                serializedCards.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return pool;
        }
    }
}
