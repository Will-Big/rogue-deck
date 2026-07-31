using System;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardPrefabCatalogTests
    {
        internal const string CatalogPath =
            "Assets/Unity/CardPrefabCatalog.asset";
        internal const string ExecutionPath =
            "Assets/Unity/Prefabs/ExecutionCardView.prefab";
        internal const string InterventionPath =
            "Assets/Unity/Prefabs/InterventionCardView.prefab";
        internal const string TargetGlyphPath =
            "Assets/Unity/Prefabs/TargetGlyphView.prefab";
        internal const string DescriptionLinePath =
            "Assets/Unity/Prefabs/DescriptionLineView.prefab";

        [TestCase(CardCategory.Execution, "ExecutionCardView")]
        [TestCase(CardCategory.Intervention, "InterventionCardView")]
        public void Catalog_resolves_the_category_prefab(
            CardCategory category,
            string expectedName)
        {
            var catalog = LoadCatalog();

            Assert.AreEqual(expectedName, catalog.Resolve(category).name);
        }

        [Test]
        public void Catalog_rejects_an_undefined_category()
        {
            var catalog = LoadCatalog();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => catalog.Resolve((CardCategory)999));
        }

        [Test]
        public void Authored_catalog_has_all_four_valid_prefab_references()
        {
            var catalog = LoadCatalog();

            Assert.IsEmpty(catalog.Validate());
            Assert.DoesNotThrow(catalog.ValidateOrThrow);
        }

        [Test]
        public void Validation_reports_every_missing_reference()
        {
            var catalog = ScriptableObject.CreateInstance<CardPrefabCatalog>();
            try
            {
                var errors = catalog.Validate();

                Assert.AreEqual(4, errors.Count);
                Assert.That(errors, Has.Some.Contains("execution"));
                Assert.That(errors, Has.Some.Contains("intervention"));
                Assert.That(errors, Has.Some.Contains("target glyph"));
                Assert.That(errors, Has.Some.Contains("description line"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Validation_reports_full_card_category_mismatches()
        {
            var source = LoadCatalog();
            var catalog = ScriptableObject.CreateInstance<CardPrefabCatalog>();
            try
            {
                var serialized = new SerializedObject(catalog);
                serialized.FindProperty("_executionCard").objectReferenceValue =
                    source.Resolve(CardCategory.Intervention);
                serialized.FindProperty("_interventionCard").objectReferenceValue =
                    source.Resolve(CardCategory.Execution);
                serialized.FindProperty("_targetGlyph").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TargetGlyphView>(TargetGlyphPath);
                serialized.FindProperty("_descriptionLine").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<DescriptionLineView>(DescriptionLinePath);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var errors = catalog.Validate();

                Assert.AreEqual(2, errors.Count);
                Assert.That(errors.Count(error => error.Contains("category")), Is.EqualTo(2));
                Assert.That(errors, Has.Some.Contains("Execution"));
                Assert.That(errors, Has.Some.Contains("Intervention"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Create_uses_the_presentation_category_and_configures_subviews()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            CardView view = null;
            try
            {
                var presentation = Presentation(
                    CardCategory.Execution,
                    new[] { new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne) },
                    new[]
                    {
                        new CardDescriptionLine(
                            new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne),
                            "피해 3.")
                    });

                view = LoadCatalog().Create(
                    presentation,
                    (RectTransform)root.transform);
                view.Bind(presentation, null);

                Assert.AreEqual(CardCategory.Execution, view.PrefabCategory);
                Assert.AreSame(root.transform, view.transform.parent);
                Assert.AreEqual(
                    1,
                    Field<RectTransform>(view, "_targetContent").childCount);
                Assert.AreEqual(
                    1,
                    Field<RectTransform>(view, "_descriptionContent").childCount);
            }
            finally
            {
                if (view != null)
                {
                    Object.DestroyImmediate(view.gameObject);
                }

                Object.DestroyImmediate(root);
            }
        }

        internal static CardPrefabCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CardPrefabCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "CardPrefabCatalog.asset must exist.");
            return catalog;
        }

        internal static CardPresentation Presentation(
            CardCategory category,
            CardTargetKey[] targetEntries,
            CardDescriptionLine[] lines)
            => new CardPresentation(
                category == CardCategory.Execution ? "execution" : "intervention",
                category.ToString(),
                3,
                1,
                Side.Player,
                new CardDescriptionLayout(targetEntries, lines, "plain text"),
                null,
                false,
                category: category);

        internal static T Field<T>(object target, string name)
            => (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
    }
}
