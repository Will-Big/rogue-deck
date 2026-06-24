using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FateWeaver.Unity.Editor
{
    public static class DeckPlaytestSceneCreator
    {
        public const string ScenePath = "Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity";
        public const string PrefabPath = "Assets/FateWeaver/Unity/Resources/CardView.prefab";
        private const string FontAssetPath = "Assets/FateWeaver/Unity/Resources/Fonts/KoreanTMP.asset";
        private const string DeckAssetPath = "Assets/FateWeaver/Unity/Cards/StarterDeck.asset";

        [MenuItem("Fate Weaver/Build Deck Playtest Scene")]
        public static void Build()
        {
            EnsureSpriteImport();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            var cardPrefab = BuildCardPrefab(font);
            var deck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckAssetPath);
            if (deck == null)
            {
                Debug.LogWarning("No DeckAsset at " + DeckAssetPath + " — run 'Fate Weaver/Seed Starter Card Assets' first.");
            }

            Directory.CreateDirectory("Assets/FateWeaver/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            EnsureEventSystem();

            var bg = NewImage("Background", canvasGo.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
            Stretch(bg.rectTransform);

            var root = NewColumn("Root", canvasGo.transform);
            var rootRt = (RectTransform)root.transform;
            Stretch(rootRt);
            rootRt.offsetMin = new Vector2(20, 20);
            rootRt.offsetMax = new Vector2(-20, -20);

            var state = NewText("State", root.transform, font, 18, FontStyles.Bold, Color.white);
            var zoneLabel = NewText("ZoneLabel", root.transform, font, 14, FontStyles.Normal, new Color(0.4f, 0.85f, 1f));
            zoneLabel.text = "미래 영역 (주도력 순) — 운명 타깃";
            var zoneRow = NewRow("ZoneRow", root.transform);
            ((RectTransform)zoneRow.transform).sizeDelta = new Vector2(0, 300);
            var message = NewText("Message", root.transform, font, 16, FontStyles.Bold, new Color(1f, 0.82f, 0.3f));
            var timeline = NewText("Timeline", root.transform, font, 14, FontStyles.Normal, Color.white);
            var handLabel = NewText("HandLabel", root.transform, font, 14, FontStyles.Normal, new Color(0.4f, 0.85f, 1f));
            handLabel.text = "손패 — 행동=배치, 운명=대상 선택";
            var handRow = NewRow("HandRow", root.transform);
            ((RectTransform)handRow.transform).sizeDelta = new Vector2(0, 300);

            var footer = NewRow("Footer", root.transform);
            var piles = NewText("Piles", footer.transform, font, 14, FontStyles.Normal, Color.white);
            var resolve = NewButton("Resolve", footer.transform, font, "턴 실행");
            var next = NewButton("Next", footer.transform, font, "다음 턴");
            var reset = NewButton("Reset", footer.transform, font, "초기화");

            var controllerGo = new GameObject("Deck Playtest");
            var controller = controllerGo.AddComponent<DeckPlaytestController>();

            var so = new SerializedObject(controller);
            so.FindProperty("_deck").objectReferenceValue = deck;
            WireEnemyArt(so.FindProperty("_enemyArtCards"));
            so.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("_handRow").objectReferenceValue = handRow.transform;
            so.FindProperty("_zoneRow").objectReferenceValue = zoneRow.transform;
            so.FindProperty("_stateText").objectReferenceValue = state;
            so.FindProperty("_pilesText").objectReferenceValue = piles;
            so.FindProperty("_messageText").objectReferenceValue = message;
            so.FindProperty("_timelineText").objectReferenceValue = timeline;
            so.FindProperty("_resolveButton").objectReferenceValue = resolve;
            so.FindProperty("_nextButton").objectReferenceValue = next;
            so.FindProperty("_resetButton").objectReferenceValue = reset;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built deck playtest scene at " + ScenePath);
        }

        private static CardView BuildCardPrefab(TMP_FontAsset font)
        {
            Directory.CreateDirectory("Assets/FateWeaver/Unity/Resources");

            var rootGo = new GameObject("CardView", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CardView));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(200, 280);
            var rootImage = rootGo.GetComponent<Image>();
            rootImage.color = new Color(0.16f, 0.18f, 0.22f, 1f);
            var rootButton = rootGo.GetComponent<Button>();
            rootButton.targetGraphic = rootImage;
            var le = rootGo.GetComponent<LayoutElement>();
            le.preferredWidth = 200;
            le.preferredHeight = 280;

            var outline = NewImage("SelectionOutline", rootGo.transform, new Color(0, 0, 0, 0));
            Stretch(outline.rectTransform);
            outline.rectTransform.offsetMin = new Vector2(-3, -3);
            outline.rectTransform.offsetMax = new Vector2(3, 3);
            outline.raycastTarget = false;

            var art = NewImage("Art", rootGo.transform, Color.white);
            art.preserveAspect = true;
            art.raycastTarget = false;
            AnchorTop(art.rectTransform, 170);
            var artFallback = NewImage("ArtFallback", rootGo.transform, new Color(0.22f, 0.28f, 0.36f, 1f));
            artFallback.raycastTarget = false;
            AnchorTop(artFallback.rectTransform, 170);
            artFallback.enabled = false;

            var cost = NewText("Cost", rootGo.transform, font, 18, FontStyles.Bold, new Color(0.6f, 0.85f, 1f));
            AnchorBand(cost.rectTransform, 4, 26);
            cost.alignment = TextAlignmentOptions.TopLeft;
            var initiative = NewText("Initiative", rootGo.transform, font, 14, FontStyles.Bold, new Color(0.95f, 0.85f, 0.4f));
            AnchorBand(initiative.rectTransform, 4, 26);
            initiative.alignment = TextAlignmentOptions.TopRight;
            var nameText = NewText("Name", rootGo.transform, font, 15, FontStyles.Bold, Color.white);
            AnchorBand(nameText.rectTransform, 172, 24);
            var descText = NewText("Description", rootGo.transform, font, 12, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f));
            AnchorBand(descText.rectTransform, 198, 78);

            var lockBadge = NewText("LockBadge", rootGo.transform, font, 12, FontStyles.Bold, new Color(1f, 0.5f, 0.5f));
            lockBadge.text = "고정";
            AnchorBand(lockBadge.rectTransform, 150, 22);
            lockBadge.alignment = TextAlignmentOptions.Center;

            var view = rootGo.GetComponent<CardView>();
            var so = new SerializedObject(view);
            so.FindProperty("_art").objectReferenceValue = art;
            so.FindProperty("_artFallback").objectReferenceValue = artFallback;
            so.FindProperty("_nameText").objectReferenceValue = nameText;
            so.FindProperty("_initiativeText").objectReferenceValue = initiative;
            so.FindProperty("_costText").objectReferenceValue = cost;
            so.FindProperty("_descriptionText").objectReferenceValue = descText;
            so.FindProperty("_selectionOutline").objectReferenceValue = outline;
            so.FindProperty("_lockBadge").objectReferenceValue = lockBadge.gameObject;
            so.FindProperty("_button").objectReferenceValue = rootButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            Object.DestroyImmediate(rootGo);
            return prefab.GetComponent<CardView>();
        }

        // --- small uGUI builders (same pattern as the prior scene creator) ---

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
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = name;
            text.raycastTarget = false;
            go.AddComponent<LayoutElement>().minHeight = size + 8;
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
            le.minWidth = 120;
            le.minHeight = 44;
            var text = NewText("Label", go.transform, font, 16, FontStyles.Normal, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
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
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0, height);
        }

        private static void AnchorBand(RectTransform rt, float topOffset, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -topOffset);
            rt.sizeDelta = new Vector2(-10, height);
        }

        private const string UiActionsPath = "Assets/FateWeaver/Unity/Resources/UIInputActions.inputactions";

        // InputSystemUIInputModule.AssignDefaultActions() is broken in this Input System build (it builds
        // actions that are not part of an asset). Instead: persist the default UI actions as a project
        // .inputactions asset and wire the module from its generated, serialization-safe references.
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            var module = es.AddComponent<InputSystemUIInputModule>();
            module.actionsAsset = LoadOrCreateUiActions();

            var refs = AssetDatabase.LoadAllAssetsAtPath(UiActionsPath).OfType<InputActionReference>().ToList();
            InputActionReference Find(string action) => refs.FirstOrDefault(r =>
                r != null && r.action != null && r.action.actionMap != null
                && r.action.actionMap.name == "UI" && r.action.name == action);

            module.point = Find("Point");
            module.leftClick = Find("Click");
            module.middleClick = Find("MiddleClick");
            module.rightClick = Find("RightClick");
            module.scrollWheel = Find("ScrollWheel");
            module.move = Find("Navigate");
            module.submit = Find("Submit");
            module.cancel = Find("Cancel");
        }

        private static InputActionAsset LoadOrCreateUiActions()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory("Assets/FateWeaver/Unity/Resources");
            File.WriteAllText(UiActionsPath, new DefaultInputActions().asset.ToJson());
            AssetDatabase.ImportAsset(UiActionsPath);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(UiActionsPath);
        }

        // Wire the controller's enemy-art CardAssets from the seeded Enemies folder (no-op until seeded).
        private static void WireEnemyArt(SerializedProperty arrayProp)
        {
            var cards = AssetDatabase.IsValidFolder(CardCodeGenerator.EnemyCardFolder)
                ? AssetDatabase.FindAssets("t:CardAsset", new[] { CardCodeGenerator.EnemyCardFolder })
                    .Select(g => AssetDatabase.LoadAssetAtPath<CardAsset>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(c => c != null)
                    .ToArray()
                : System.Array.Empty<CardAsset>();

            arrayProp.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }
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
            if (EditorBuildSettings.scenes.Any(s => s.path == ScenePath)) return;
            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
