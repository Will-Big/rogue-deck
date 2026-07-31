using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    [Explicit("Writes opt-in card frame captures to /private/tmp only.")]
    public class CardFrameRenderCapture
    {
        private const string CaptureDirectory =
            "/private/tmp/primitive-card-frame-captures";

        [TestCase(
            "execution-1280x720",
            1280,
            720,
            CaptureContent.Execution)]
        [TestCase(
            "intervention-1280x720",
            1280,
            720,
            CaptureContent.Intervention)]
        [TestCase(
            "toxic-reclaim-1280x720",
            1280,
            720,
            CaptureContent.ToxicReclaim)]
        [TestCase(
            "mixed-five-960x720",
            960,
            720,
            CaptureContent.MixedFive)]
        [TestCase(
            "mixed-five-1280x800",
            1280,
            800,
            CaptureContent.MixedFive)]
        [TestCase(
            "mixed-five-1280x720",
            1280,
            720,
            CaptureContent.MixedFive)]
        [TestCase(
            "mixed-five-1680x720",
            1680,
            720,
            CaptureContent.MixedFive)]
        public void Render_card_frame_case(
            string caseName,
            int width,
            int height,
            CaptureContent content)
        {
            var cameraObject = new GameObject("CaptureCamera", typeof(Camera));
            var canvasObject = new GameObject(
                "CaptureCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var renderTarget = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            IDisposable fontIsolation = null;
            IDisposable catalogResources = null;
            var previousActive = RenderTexture.active;
            try
            {
                ConfigureCameraAndCanvas(
                    cameraObject.GetComponent<Camera>(),
                    canvasObject.GetComponent<Canvas>(),
                    canvasObject.GetComponent<CanvasScaler>(),
                    renderTarget,
                    width,
                    height);
                catalogResources = CloneCatalogForCapture(
                    CardPrefabCatalogTests.LoadCatalog(),
                    out var captureCatalog);
                BuildHand(
                    (RectTransform)canvasObject.transform,
                    width,
                    Presentations(content),
                    captureCatalog);
                PrewarmFontsForCapture(canvasObject);
                fontIsolation = IsolateFontsForCapture(canvasObject);
                Canvas.ForceUpdateCanvases();

                cameraObject.GetComponent<Camera>().Render();
                RenderTexture.active = renderTarget;
                texture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();

                Directory.CreateDirectory(CaptureDirectory);
                File.WriteAllBytes(
                    Path.Combine(CaptureDirectory, caseName + ".png"),
                    ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                RenderTexture.active = previousActive;
                fontIsolation?.Dispose();
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                cameraObject.GetComponent<Camera>().targetTexture = null;
                renderTarget.Release();
                Object.DestroyImmediate(renderTarget);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(cameraObject);
                catalogResources?.Dispose();
            }
        }

        internal static IDisposable CloneCatalogForCapture(
            CardPrefabCatalog source,
            out CardPrefabCatalog clone)
        {
            clone = Object.Instantiate(source);
            clone.name = source.name + " Capture Clone";
            clone.hideFlags = HideFlags.HideAndDontSave;
            var templateRoot = new GameObject("Capture Catalog Templates");
            templateRoot.hideFlags = HideFlags.HideAndDontSave;
            templateRoot.SetActive(false);
            var isolations = new List<IDisposable>();
            try
            {
                var execution = CloneTemplate(
                    source.Resolve(CardCategory.Execution),
                    templateRoot.transform,
                    isolations);
                var intervention = CloneTemplate(
                    source.Resolve(CardCategory.Intervention),
                    templateRoot.transform,
                    isolations);
                var targetGlyph = CloneTemplate(
                    Field<TargetGlyphView>(source, "_targetGlyph"),
                    templateRoot.transform,
                    isolations);
                var descriptionLine = CloneTemplate(
                    Field<DescriptionLineView>(source, "_descriptionLine"),
                    templateRoot.transform,
                    isolations);
                SetField(clone, "_executionCard", execution);
                SetField(clone, "_interventionCard", intervention);
                SetField(clone, "_targetGlyph", targetGlyph);
                SetField(clone, "_descriptionLine", descriptionLine);
                return new CatalogIsolation(clone, templateRoot, isolations);
            }
            catch
            {
                new CatalogIsolation(clone, templateRoot, isolations).Dispose();
                clone = null;
                throw;
            }
        }

        private static T CloneTemplate<T>(
            T source,
            Transform parent,
            ICollection<IDisposable> isolations)
            where T : Component
        {
            var clone = Object.Instantiate(source, parent);
            clone.gameObject.name = source.gameObject.name + " Capture Clone";
            clone.gameObject.hideFlags = HideFlags.HideAndDontSave;
            isolations.Add(IsolateFonts(
                clone.gameObject,
                AtlasPopulationMode.Dynamic));
            return clone;
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);

        private static void SetField(object target, string name, object value)
            => target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        internal static IDisposable IsolateFontsForCapture(
            GameObject canvasObject)
            => IsolateFonts(canvasObject, AtlasPopulationMode.Static);

        private static IDisposable IsolateFonts(
            GameObject canvasObject,
            AtlasPopulationMode populationMode)
        {
            var fontClones = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
            var materialClones = new Dictionary<Material, Material>();
            var atlasClones = new Dictionary<Texture, Texture2D>();
            var states = new List<FontState>();
            foreach (var text in canvasObject.GetComponentsInChildren<TMP_Text>(true))
            {
                var sourceFont = text.font;
                if (sourceFont == null)
                {
                    continue;
                }

                var sourceMaterial = text.fontSharedMaterial;
                states.Add(new FontState(text, sourceFont, sourceMaterial));
                if (!fontClones.TryGetValue(sourceFont, out var fontClone))
                {
                    fontClone = Object.Instantiate(sourceFont);
                    fontClone.name = sourceFont.name + " Capture Clone";
                    fontClone.hideFlags = HideFlags.HideAndDontSave;
                    fontClone.atlasPopulationMode = populationMode;
                    fontClone.atlasTextures = CloneAtlases(
                        sourceFont.atlasTextures,
                        atlasClones);
                    if (sourceFont.material != null)
                    {
                        fontClone.material = CloneMaterial(
                            sourceFont.material,
                            materialClones,
                            atlasClones);
                    }

                    fontClones.Add(sourceFont, fontClone);
                }

                text.font = fontClone;
                if (sourceMaterial != null)
                {
                    text.fontSharedMaterial = CloneMaterial(
                        sourceMaterial,
                        materialClones,
                        atlasClones);
                }
            }

            return new FontIsolation(states, fontClones, materialClones);
        }

        private static void PrewarmFontsForCapture(GameObject canvasObject)
        {
            foreach (var text in canvasObject.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate(true, true);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static Material CloneMaterial(
            Material source,
            IDictionary<Material, Material> clones,
            IReadOnlyDictionary<Texture, Texture2D> atlasClones)
        {
            if (clones.TryGetValue(source, out var clone))
            {
                return clone;
            }

            clone = Object.Instantiate(source);
            clone.name = source.name + " Capture Clone";
            clone.hideFlags = HideFlags.HideAndDontSave;
            if (source.mainTexture != null
                && atlasClones.TryGetValue(source.mainTexture, out var atlasClone))
            {
                clone.mainTexture = atlasClone;
            }

            clones.Add(source, clone);
            return clone;
        }

        private static Texture2D[] CloneAtlases(
            IReadOnlyList<Texture2D> sources,
            IDictionary<Texture, Texture2D> clones)
        {
            var results = new Texture2D[sources.Count];
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == null)
                {
                    continue;
                }

                if (!clones.TryGetValue(source, out var clone))
                {
                    clone = Object.Instantiate(source);
                    clone.name = source.name + " Capture Clone";
                    clone.hideFlags = HideFlags.HideAndDontSave;
                    clones.Add(source, clone);
                }

                results[i] = clone;
            }

            return results;
        }

        private static void ConfigureCameraAndCanvas(
            Camera camera,
            Canvas canvas,
            CanvasScaler scaler,
            RenderTexture renderTarget,
            int width,
            int height)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.16f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.targetTexture = renderTarget;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            Assert.AreEqual(
                new Vector2(width, height),
                ((RectTransform)canvas.transform).rect.size);
        }

        private static void BuildHand(
            RectTransform canvas,
            int logicalWidth,
            CardPresentation[] presentations,
            CardPrefabCatalog catalog)
        {
            var handObject = new GameObject("HandFan", typeof(RectTransform));
            var handRect = (RectTransform)handObject.transform;
            handRect.SetParent(canvas, false);
            handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 210f);
            handRect.sizeDelta = new Vector2(logicalWidth, 260f);

            var contentObject = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(handRect, false);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var hand = handObject.AddComponent<HandFanView>();
            hand.EditorBuild(catalog, content);
            hand.SetCards(presentations, _ => { }, (_, __) => { });
        }

        private static CardPresentation[] Presentations(CaptureContent content)
        {
            switch (content)
            {
                case CaptureContent.Execution:
                    return new[]
                    {
                        Presentation(
                            "execution",
                            "Execution",
                            CardCategory.Execution,
                            Array.Empty<CardTargetKey>(),
                            new[]
                            {
                                new CardDescriptionLine(
                                    new CardTargetKey(
                                        CardTargetFaction.Enemy,
                                        CardTargetRange.FrontOne),
                                    "피해 3.")
                            })
                    };
                case CaptureContent.Intervention:
                    return new[]
                    {
                        Presentation(
                            "intervention",
                            "Intervention",
                            CardCategory.Intervention,
                            Array.Empty<CardTargetKey>(),
                            new[]
                            {
                                new CardDescriptionLine(
                                    null,
                                    "실행 순서를 바꾼다.")
                            })
                    };
                case CaptureContent.ToxicReclaim:
                    var self = new CardTargetKey(
                        CardTargetFaction.Ally,
                        CardTargetRange.Self);
                    var enemy = new CardTargetKey(
                        CardTargetFaction.Enemy,
                        CardTargetRange.FrontOne);
                    return new[]
                    {
                        Presentation(
                            "toxic_reclaim",
                            "독성 환원",
                            CardCategory.Execution,
                            new[] { self, enemy },
                            new[]
                            {
                                new CardDescriptionLine(enemy, "독을 소비하고 독 1."),
                                new CardDescriptionLine(self, "소비했다면 방어 4.")
                            })
                    };
                case CaptureContent.MixedFive:
                    return new[]
                    {
                        Presentation(
                            "execution-0",
                            "Execution 0",
                            CardCategory.Execution,
                            Array.Empty<CardTargetKey>(),
                            Array.Empty<CardDescriptionLine>()),
                        Presentation(
                            "intervention-1",
                            "Intervention 1",
                            CardCategory.Intervention,
                            Array.Empty<CardTargetKey>(),
                            Array.Empty<CardDescriptionLine>()),
                        Presentation(
                            "execution-2",
                            "Execution 2",
                            CardCategory.Execution,
                            Array.Empty<CardTargetKey>(),
                            Array.Empty<CardDescriptionLine>()),
                        Presentation(
                            "intervention-3",
                            "Intervention 3",
                            CardCategory.Intervention,
                            Array.Empty<CardTargetKey>(),
                            Array.Empty<CardDescriptionLine>()),
                        Presentation(
                            "execution-4",
                            "Execution 4",
                            CardCategory.Execution,
                            Array.Empty<CardTargetKey>(),
                            Array.Empty<CardDescriptionLine>())
                    };
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(content),
                        content,
                        "Undefined capture content.");
            }
        }

        private static CardPresentation Presentation(
            string id,
            string displayName,
            CardCategory category,
            CardTargetKey[] targets,
            CardDescriptionLine[] lines)
            => new CardPresentation(
                id,
                displayName,
                3,
                1,
                Side.Player,
                new CardDescriptionLayout(targets, lines, string.Empty),
                null,
                false,
                category: category);

        public enum CaptureContent
        {
            Execution,
            Intervention,
            ToxicReclaim,
            MixedFive
        }

        private readonly struct FontState
        {
            public FontState(
                TMP_Text text,
                TMP_FontAsset font,
                Material material)
            {
                Text = text;
                Font = font;
                Material = material;
            }

            public TMP_Text Text { get; }
            public TMP_FontAsset Font { get; }
            public Material Material { get; }
        }

        private sealed class FontIsolation : IDisposable
        {
            private readonly IReadOnlyList<FontState> _states;
            private readonly IReadOnlyDictionary<TMP_FontAsset, TMP_FontAsset>
                _fontClones;
            private readonly IReadOnlyDictionary<Material, Material>
                _materialClones;

            public FontIsolation(
                IReadOnlyList<FontState> states,
                IReadOnlyDictionary<TMP_FontAsset, TMP_FontAsset> fontClones,
                IReadOnlyDictionary<Material, Material> materialClones)
            {
                _states = states;
                _fontClones = fontClones;
                _materialClones = materialClones;
            }

            public void Dispose()
            {
                foreach (var state in _states)
                {
                    if (state.Text == null)
                    {
                        continue;
                    }

                    state.Text.font = state.Font;
                    state.Text.fontSharedMaterial = state.Material;
                }

                foreach (var clone in _fontClones.Values)
                {
                    Object.DestroyImmediate(clone);
                }

                foreach (var clone in _materialClones.Values)
                {
                    Object.DestroyImmediate(clone);
                }
            }
        }

        private sealed class CatalogIsolation : IDisposable
        {
            private readonly CardPrefabCatalog _catalog;
            private readonly GameObject _templateRoot;
            private readonly IReadOnlyList<IDisposable> _isolations;

            public CatalogIsolation(
                CardPrefabCatalog catalog,
                GameObject templateRoot,
                IReadOnlyList<IDisposable> isolations)
            {
                _catalog = catalog;
                _templateRoot = templateRoot;
                _isolations = isolations;
            }

            public void Dispose()
            {
                foreach (var isolation in _isolations)
                {
                    isolation.Dispose();
                }

                Object.DestroyImmediate(_templateRoot);
                Object.DestroyImmediate(_catalog);
            }
        }
    }
}
