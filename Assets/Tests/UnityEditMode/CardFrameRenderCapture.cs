using System;
using System.IO;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Presentation;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
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
                BuildHand(
                    (RectTransform)canvasObject.transform,
                    width,
                    Presentations(content));
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
                ClearPersistentFontDirtyFlags(canvasObject);
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                cameraObject.GetComponent<Camera>().targetTexture = null;
                renderTarget.Release();
                Object.DestroyImmediate(renderTarget);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void ClearPersistentFontDirtyFlags(GameObject canvasObject)
        {
            foreach (var text in canvasObject.GetComponentsInChildren<TMP_Text>(true))
            {
                var font = text.font;
                if (font == null || !AssetDatabase.Contains(font))
                {
                    continue;
                }

                EditorUtility.ClearDirty(font);
                if (font.material != null)
                {
                    EditorUtility.ClearDirty(font.material);
                }

                foreach (var atlas in font.atlasTextures)
                {
                    if (atlas != null)
                    {
                        EditorUtility.ClearDirty(atlas);
                    }
                }
            }
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
            CardPresentation[] presentations)
        {
            var handObject = new GameObject("HandFan", typeof(RectTransform));
            var handRect = (RectTransform)handObject.transform;
            handRect.SetParent(canvas, false);
            handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 130f);
            handRect.sizeDelta = new Vector2(logicalWidth, 260f);

            var contentObject = new GameObject("Content", typeof(RectTransform));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(handRect, false);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = new Vector2(0f, 80f);
            content.sizeDelta = Vector2.zero;

            var hand = handObject.AddComponent<HandFanView>();
            hand.EditorBuild(CardPrefabCatalogTests.LoadCatalog(), content);
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
    }
}
