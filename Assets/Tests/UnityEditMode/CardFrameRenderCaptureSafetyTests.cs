using System;
using System.Linq;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardFrameRenderCaptureSafetyTests
    {
        private const string FontPath =
            "Assets/Unity/Resources/Fonts/KoreanTMP.asset";

        [Test]
        public void Capture_uses_static_nonpersistent_font_clones_without_clearing_source_dirty_state()
        {
            var root = new GameObject("CaptureCanvas", typeof(RectTransform));
            var textObject = new GameObject(
                "CaptureText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            var text = textObject.GetComponent<TMP_Text>();
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Assert.IsNotNull(source);
            bool wasDirty = EditorUtility.IsDirty(source);
            IDisposable isolation = null;
            try
            {
                text.font = source;
                EditorUtility.SetDirty(source);
                var method = typeof(CardFrameRenderCapture).GetMethod(
                    "IsolateFontsForCapture",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(
                    method,
                    "Capture must isolate TMP assets before forcing canvas updates.");

                isolation = (IDisposable)method.Invoke(
                    null,
                    new object[] { root });

                Assert.AreNotSame(source, text.font);
                Assert.IsFalse(AssetDatabase.Contains(text.font));
                Assert.AreEqual(
                    AtlasPopulationMode.Static,
                    text.font.atlasPopulationMode);
                Assert.AreNotSame(source.material, text.font.material);
                Assert.IsFalse(AssetDatabase.Contains(text.font.material));
                Assert.That(text.font.atlasTextures, Is.Not.Empty);
                Assert.IsTrue(
                    Array.TrueForAll(
                        text.font.atlasTextures,
                        atlas => atlas == null || !AssetDatabase.Contains(atlas)));
                Assert.IsTrue(EditorUtility.IsDirty(source));
            }
            finally
            {
                isolation?.Dispose();
                Object.DestroyImmediate(root);
                if (wasDirty)
                {
                    EditorUtility.SetDirty(source);
                }
                else
                {
                    EditorUtility.ClearDirty(source);
                }
            }
        }

        [Test]
        public void Capture_catalog_templates_are_font_isolated_before_card_binding()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            bool wasDirty = EditorUtility.IsDirty(sourceFont);
            IDisposable resources = null;
            IDisposable renderIsolation = null;
            GameObject cardRoot = null;
            try
            {
                int characterCount = sourceFont.characterTable.Count;
                int glyphCount = sourceFont.glyphTable.Count;
                EditorUtility.SetDirty(sourceFont);
                var method = typeof(CardFrameRenderCapture).GetMethod(
                    "CloneCatalogForCapture",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(
                    method,
                    "Capture catalog templates must be cloned and isolated before Bind.");
                object[] arguments =
                {
                    CardPrefabCatalogTests.LoadCatalog(),
                    null
                };
                resources = (IDisposable)method.Invoke(null, arguments);
                var catalog = (CardPrefabCatalog)arguments[1];

                AssertTemplateFontsAreIsolated(
                    catalog.Resolve(CardCategory.Execution).gameObject);
                AssertTemplateFontsAreIsolated(
                    catalog.Resolve(CardCategory.Intervention).gameObject);
                AssertTemplateFontsAreIsolated(
                    CardPrefabCatalogTests.Field<DescriptionLineView>(
                        catalog,
                        "_descriptionLine").gameObject);
                cardRoot = new GameObject(
                    "CardRoot",
                    typeof(RectTransform),
                    typeof(Canvas));
                var presentation = CardPrefabCatalogTests.Presentation(
                    CardCategory.Execution,
                    Array.Empty<CardTargetKey>(),
                    new[] { new CardDescriptionLine(null, "꾼독") });
                var view = catalog.Create(
                    presentation,
                    (RectTransform)cardRoot.transform);
                view.Bind(presentation, null);
                foreach (var text in view.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate(true, true);
                }

                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(
                    view.GetComponentsInChildren<TMP_Text>(true)
                        .Any(text => text.font.HasCharacter('독')));
                renderIsolation = InvokeFontIsolation(cardRoot);
                Assert.IsTrue(view.GetComponentsInChildren<TMP_Text>(true)
                    .All(text => text.font.atlasPopulationMode
                        == AtlasPopulationMode.Static));

                Assert.AreEqual(characterCount, sourceFont.characterTable.Count);
                Assert.AreEqual(glyphCount, sourceFont.glyphTable.Count);
                Assert.IsTrue(EditorUtility.IsDirty(sourceFont));
            }
            finally
            {
                renderIsolation?.Dispose();
                Object.DestroyImmediate(cardRoot);
                resources?.Dispose();
                if (wasDirty)
                {
                    EditorUtility.SetDirty(sourceFont);
                }
                else
                {
                    EditorUtility.ClearDirty(sourceFont);
                }
            }
        }

        private static void AssertTemplateFontsAreIsolated(GameObject template)
        {
            var texts = template.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(texts, Is.Not.Empty);
            foreach (var text in texts)
            {
                Assert.IsFalse(AssetDatabase.Contains(text.font));
                Assert.AreEqual(
                    AtlasPopulationMode.Dynamic,
                    text.font.atlasPopulationMode);
            }
        }

        private static IDisposable InvokeFontIsolation(GameObject root)
        {
            var method = typeof(CardFrameRenderCapture).GetMethod(
                "IsolateFontsForCapture",
                BindingFlags.Static | BindingFlags.NonPublic);
            return (IDisposable)method.Invoke(null, new object[] { root });
        }
    }
}
