using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity.Editor
{
    public static class FateWeaverPlaytestSceneCreator
    {
        public const string ScenePath = "Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity";
        public const string PrefabPath = "Assets/FateWeaver/Unity/Resources/CardView.prefab";
        private const string FontAssetPath = "Assets/FateWeaver/Unity/Resources/Fonts/KoreanTMP.asset";

        [MenuItem("Fate Weaver/Build Playtest Scene (uGUI)")]
        public static void Build()
        {
            EnsureSpriteImport();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                Debug.LogWarning("Korean TMP font not found at " + FontAssetPath
                    + " — run 'Fate Weaver/Create Korean TMP Font' first. Building with TMP default font.");
            }

            var cardPrefab = BuildCardPrefab(font);

            Directory.CreateDirectory("Assets/FateWeaver/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                var inputModule = eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }

            var bg = NewImage("Background", canvasGo.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
            Stretch(bg.rectTransform);

            var root = NewColumn("Root", canvasGo.transform);
            var rootRt = (RectTransform)root.transform;
            Stretch(rootRt);
            rootRt.offsetMin = new Vector2(20, 20);
            rootRt.offsetMax = new Vector2(-20, -20);

            var header = NewText("Header", root.transform, font, 26, FontStyles.Bold, Color.white);
            var scenarioRow = NewRow("ScenarioRow", root.transform);
            var scenarioTemplate = NewButton("ScenarioButton", scenarioRow.transform, font, "시나리오");
            var state = NewText("State", root.transform, font, 18, FontStyles.Normal, Color.white);
            var cardRow = NewRow("CardRow", root.transform);
            ((RectTransform)cardRow.transform).sizeDelta = new Vector2(0, 360);
            var selection = NewText("Selection", root.transform, font, 16, FontStyles.Normal, Color.white);

            var fateRow = NewRow("FateRow", root.transform);
            var initMinus = NewButton("InitMinus", fateRow.transform, font, "주도력 -2");
            var initPlus = NewButton("InitPlus", fateRow.transform, font, "주도력 +2");
            var swap = NewButton("Swap", fateRow.transform, font, "주도력 교환");
            var lockBtn = NewButton("Lock", fateRow.transform, font, "주 대상 고정");

            var flowRow = NewRow("FlowRow", root.transform);
            var resolve = NewButton("Resolve", flowRow.transform, font, "턴 실행");
            var next = NewButton("Next", flowRow.transform, font, "다음 턴");
            var reset = NewButton("Reset", flowRow.transform, font, "초기화");

            var message = NewText("Message", root.transform, font, 16, FontStyles.Bold, new Color(1f, 0.82f, 0.3f));
            var timeline = NewText("Timeline", root.transform, font, 16, FontStyles.Normal, Color.white);

            var controllerGo = new GameObject("FateWeaver Playtest");
            var controller = controllerGo.AddComponent<FateWeaverPlaytestController>();

            var so = new SerializedObject(controller);
            so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("_cardRow").objectReferenceValue = cardRow.transform;
            so.FindProperty("_scenarioRow").objectReferenceValue = scenarioRow.transform;
            so.FindProperty("_scenarioButtonTemplate").objectReferenceValue = scenarioTemplate;
            so.FindProperty("_headerText").objectReferenceValue = header;
            so.FindProperty("_stateText").objectReferenceValue = state;
            so.FindProperty("_selectionText").objectReferenceValue = selection;
            so.FindProperty("_messageText").objectReferenceValue = message;
            so.FindProperty("_timelineText").objectReferenceValue = timeline;
            so.FindProperty("_initMinusButton").objectReferenceValue = initMinus;
            so.FindProperty("_initPlusButton").objectReferenceValue = initPlus;
            so.FindProperty("_swapButton").objectReferenceValue = swap;
            so.FindProperty("_lockButton").objectReferenceValue = lockBtn;
            so.FindProperty("_resolveButton").objectReferenceValue = resolve;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_resetButton").objectReferenceValue = reset;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built Fate Weaver uGUI playtest scene at " + ScenePath);
        }

        private static CardView BuildCardPrefab(TMP_FontAsset font)
        {
            Directory.CreateDirectory("Assets/FateWeaver/Unity/Resources");

            var rootGo = new GameObject("CardView", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CardView));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(220, 340);
            var rootImage = rootGo.GetComponent<Image>();
            rootImage.color = new Color(0.16f, 0.18f, 0.22f, 1f);
            var rootButton = rootGo.GetComponent<Button>();
            rootButton.targetGraphic = rootImage;
            var le = rootGo.GetComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.preferredHeight = 340;

            var outline = NewImage("SelectionOutline", rootGo.transform, new Color(0, 0, 0, 0));
            Stretch(outline.rectTransform);
            outline.rectTransform.offsetMin = new Vector2(-3, -3);
            outline.rectTransform.offsetMax = new Vector2(3, 3);
            outline.raycastTarget = false;

            var art = NewImage("Art", rootGo.transform, Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            AnchorTop(art.rectTransform, 220);
            var artFallback = NewImage("ArtFallback", rootGo.transform, new Color(0.22f, 0.28f, 0.36f, 1f));
            artFallback.raycastTarget = false;
            AnchorTop(artFallback.rectTransform, 220);
            artFallback.enabled = false;

            var nameText = NewText("Name", rootGo.transform, font, 18, FontStyles.Bold, Color.white);
            AnchorBand(nameText.rectTransform, 224, 28);
            var initText = NewText("Initiative", rootGo.transform, font, 16, FontStyles.Bold, new Color(0.95f, 0.85f, 0.4f));
            AnchorBand(initText.rectTransform, 224, 28);
            initText.alignment = TextAlignmentOptions.TopRight;
            var descText = NewText("Description", rootGo.transform, font, 14, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f));
            AnchorBand(descText.rectTransform, 254, 80);

            var lockBadge = NewText("LockBadge", rootGo.transform, font, 14, FontStyles.Bold, new Color(1f, 0.5f, 0.5f));
            lockBadge.text = "고정";
            AnchorBand(lockBadge.rectTransform, 224, 24);
            lockBadge.alignment = TextAlignmentOptions.TopLeft;

            var view = rootGo.GetComponent<CardView>();
            var so = new SerializedObject(view);
            so.FindProperty("_art").objectReferenceValue = art;
            so.FindProperty("_artFallback").objectReferenceValue = artFallback;
            so.FindProperty("_nameText").objectReferenceValue = nameText;
            so.FindProperty("_initiativeText").objectReferenceValue = initText;
            so.FindProperty("_descriptionText").objectReferenceValue = descText;
            so.FindProperty("_selectionOutline").objectReferenceValue = outline;
            so.FindProperty("_lockBadge").objectReferenceValue = lockBadge.gameObject;
            so.FindProperty("_button").objectReferenceValue = rootButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);
            return prefab.GetComponent<CardView>();
        }

        // ---- small uGUI builders -------------------------------------------------

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = name;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 8;
            return text;
        }

        private static Button NewButton(string name, Transform parent, TMP_FontAsset font, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.25f, 0.28f, 0.34f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 130;
            le.minHeight = 40;
            var text = NewText("Label", go.transform, font, 16, FontStyles.Normal, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return button;
        }

        private static GameObject NewRow(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return go;
        }

        private static GameObject NewColumn(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(0, height);
        }

        private static void AnchorBand(RectTransform rt, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -topOffset);
            rt.sizeDelta = new Vector2(-12, height);
        }

        private static void EnsureSpriteImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/FateWeaver/Unity/Resources" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer
                    && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void AddToBuildSettings()
        {
            if (EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            {
                return;
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
