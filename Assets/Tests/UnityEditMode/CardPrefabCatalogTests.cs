using System;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        internal const string GoblinPlaytestScenePath =
            "Assets/Scenes/FateWeaverPlaytest.unity";
        internal const string WardenPlaytestScenePath =
            "Assets/Scenes/FateWeaverWardenPlaytest.unity";
        internal const string BattleScenePath =
            "Assets/Scenes/FateWeaverBattle.unity";
        internal const string RailCardPath =
            "Assets/Unity/Prefabs/RailCardView.prefab";

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

        [TestCase(typeof(BattleScreenController))]
        [TestCase(typeof(DeckPlaytestController))]
        public void Battle_controller_start_validates_card_prefabs_before_other_startup_work(
            Type controllerType)
            => AssertCatalogValidationFailsFirst(controllerType, "Start");

        [TestCase(typeof(BattleScreenController))]
        [TestCase(typeof(DeckPlaytestController))]
        public void Battle_controller_session_restart_still_validates_card_prefabs(
            Type controllerType)
            => AssertCatalogValidationFailsFirst(controllerType, "StartSession");

        private static void AssertCatalogValidationFailsFirst(
            Type controllerType,
            string methodName)
        {
            var root = new GameObject("Controller");
            var catalog = ScriptableObject.CreateInstance<CardPrefabCatalog>();
            try
            {
                var controller = root.AddComponent(controllerType);
                controllerType
                    .GetField(
                        "_cardPrefabs",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, catalog);

                var exception = Assert.Throws<TargetInvocationException>(
                    () => controllerType
                        .GetMethod(
                            methodName,
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(controller, null));

                Assert.IsInstanceOf<InvalidOperationException>(
                    exception.InnerException);
                StringAssert.Contains(
                    "Card prefab catalog validation failed",
                    exception.InnerException.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Battle_scene_serializes_every_catalog_consumer_and_rail_prefab()
        {
            var scene = EditorSceneManager.OpenScene(
                BattleScenePath,
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var controller = roots
                    .SelectMany(root => root.GetComponentsInChildren<BattleScreenController>(true))
                    .Single();
                var hand = roots
                    .SelectMany(root => root.GetComponentsInChildren<HandFanView>(true))
                    .Single();
                var rail = roots
                    .SelectMany(root => root.GetComponentsInChildren<ExecutionRailView>(true))
                    .Single();
                var piles = roots
                    .SelectMany(root => root.GetComponentsInChildren<PileView>(true))
                    .ToArray();
                var catalog = LoadCatalog();

                Assert.AreSame(catalog, Field<CardPrefabCatalog>(controller, "_cardPrefabs"));
                Assert.AreSame(hand, Field<HandFanView>(controller, "_hand"));
                Assert.AreSame(rail, Field<ExecutionRailView>(controller, "_rail"));
                Assert.AreEqual(3, piles.Length);
                CollectionAssert.AreEquivalent(
                    piles,
                    new[]
                    {
                        Field<PileView>(controller, "_drawPile"),
                        Field<PileView>(controller, "_discardPile"),
                        Field<PileView>(controller, "_fullDeck")
                    });
                Assert.AreSame(catalog, Field<CardPrefabCatalog>(hand, "_cardPrefabs"));
                Assert.AreSame(hand.transform, Field<RectTransform>(hand, "_content"));
                Assert.AreSame(catalog, Field<CardPrefabCatalog>(rail, "_cardPrefabs"));
                Assert.AreEqual(
                    RailCardPath,
                    AssetDatabase.GetAssetPath(Field<RailCardView>(rail, "_cardPrefab")));
                foreach (var pile in piles)
                {
                    Assert.AreSame(
                        catalog,
                        Field<CardPrefabCatalog>(pile, "_cardPrefabs"));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase(GoblinPlaytestScenePath)]
        [TestCase(WardenPlaytestScenePath)]
        public void Deck_playtest_scene_serializes_the_card_prefab_catalog(
            string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Additive);
            try
            {
                var controllers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DeckPlaytestController>(true))
                    .ToArray();

                Assert.AreEqual(
                    1,
                    controllers.Length,
                    scenePath + " must contain exactly one DeckPlaytestController.");
                var serialized = new SerializedObject(controllers[0]);
                var catalog = serialized.FindProperty("_cardPrefabs")
                    .objectReferenceValue as CardPrefabCatalog;
                Assert.IsNotNull(
                    catalog,
                    scenePath + " must serialize CardPrefabCatalog.");
                Assert.DoesNotThrow(catalog.ValidateOrThrow);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
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
