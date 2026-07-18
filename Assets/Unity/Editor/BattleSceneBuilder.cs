using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Builds Assets/Scenes/FateWeaverBattle.unity entirely from code so the battle layout
    /// (spec 2026-07-10 §2) is reproducible without hand-authoring. Safe to re-run — overwrites.</summary>
    public static class BattleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/FateWeaverBattle.unity";
        private const string CardPrefabPath = "Assets/Unity/Prefabs/CardView.prefab";
        private const string UnitPrefabPath = "Assets/Unity/Prefabs/UnitView.prefab";
        private const string RailCardPrefabPath = "Assets/Unity/Prefabs/RailCardView.prefab";
        private const string TargetingArrowPrefabPath = "Assets/Unity/Prefabs/TargetingArrowView.prefab";
        private const string MemberAPath = "Assets/Unity/CharacterSO/member_a.asset";
        private const string MemberBPath = "Assets/Unity/CharacterSO/member_b.asset";
        private const string InputActionsPath = "Assets/Unity/Resources/UIInputActions.inputactions";

        private static readonly string[] EnemyArtCardPaths =
        {
            "Assets/Unity/CardSO/Enemies/Goblin/goblin_jab.asset",
            "Assets/Unity/CardSO/Enemies/Goblin/goblin_sly_jab.asset",
            "Assets/Unity/CardSO/Enemies/Goblin/goblin_crude_guard.asset"
        };

        [MenuItem("Fate Weaver/Build Battle Scene")]
        public static void Build()
        {
            var preflightParty = new[]
            {
                AssetDatabase.LoadAssetAtPath<CharacterAsset>(MemberAPath),
                AssetDatabase.LoadAssetAtPath<CharacterAsset>(MemberBPath)
            };
            if (preflightParty[0] == null || preflightParty[1] == null)
            {
                Debug.LogError("BattleSceneBuilder: missing party CharacterAsset. Run 'Fate Weaver/Seed Party Prototype Assets' first.");
                return;
            }

            EnsureCardOwnerChip();
            EnsureUnitPrefab();
            EnsureRailCardPrefab();
            EnsureTargetingArrowPrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Opening a scene in Single mode unloads assets loaded above. Reload every object that will
            // be written through SerializedObject or another serialized scene reference.
            var party = new[]
            {
                AssetDatabase.LoadAssetAtPath<CharacterAsset>(MemberAPath),
                AssetDatabase.LoadAssetAtPath<CharacterAsset>(MemberBPath)
            };
            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath);
            var unitPrefab = AssetDatabase.LoadAssetAtPath<UnitView>(UnitPrefabPath);
            var railCardPrefab = AssetDatabase.LoadAssetAtPath<RailCardView>(RailCardPrefabPath);
            var targetingArrowPrefab = AssetDatabase.LoadAssetAtPath<TargetingArrowView>(TargetingArrowPrefabPath);
            if (cardPrefab == null || unitPrefab == null || railCardPrefab == null || targetingArrowPrefab == null)
            {
                Debug.LogError("BattleSceneBuilder: missing required battle view prefab.");
                return;
            }

            // --- canvas + event system ---
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CreateUiCamera();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            var canvasRect = (RectTransform)canvasGo.transform;

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var uiModule = eventSystemGo.GetComponent<InputSystemUIInputModule>();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actions != null)
            {
                uiModule.actionsAsset = actions;
            }
            else
            {
                Debug.LogWarning("BattleSceneBuilder: UIInputActions asset is missing; UI input needs an assigned actions asset.");
            }

            var background = BattleUiKit.Image(canvasRect, "Background", new Color(0.08f, 0.1f, 0.16f, 1f));
            BattleUiKit.Stretch(background.rectTransform);
            background.raycastTarget = false;

            var clickCatcher = BattleUiKit.Image(canvasRect, "ClickCatcher", new Color(0f, 0f, 0f, 0f));
            BattleUiKit.Stretch(clickCatcher.rectTransform);
            var emptyClickCatcher = clickCatcher.gameObject.AddComponent<Button>();
            emptyClickCatcher.targetGraphic = clickCatcher;
            emptyClickCatcher.transition = Selectable.Transition.None;

            // --- stage: player row left, enemy row right, each unit carries its own HP bar ---
            var stage = BattleUiKit.Rect(canvasRect, "Stage");
            BattleUiKit.Anchor(stage, 0.03f, 0.52f, 0.97f, 0.94f);
            var playerRow = UnitRow(stage, "PlayerUnits", 0f, 0.45f, TextAnchor.LowerRight);
            var enemyRow = UnitRow(stage, "EnemyUnits", 0.55f, 1f, TextAnchor.LowerLeft);

            // --- overlay (popups + hover preview; forced last sibling below) ---
            var overlay = BattleUiKit.Rect(canvasRect, "Overlay");
            BattleUiKit.Stretch(overlay);
            var targetingArrowObject = (GameObject)PrefabUtility.InstantiatePrefab(
                targetingArrowPrefab.gameObject, overlay);
            var targetingArrow = targetingArrowObject.GetComponent<TargetingArrowView>();

            // --- execution rail ---
            var railRect = BattleUiKit.Rect(canvasRect, "ExecutionRail");
            BattleUiKit.Anchor(railRect, 0.03f, 0.30f, 0.97f, 0.51f);
            var rail = railRect.gameObject.AddComponent<ExecutionRailView>();
            rail.EditorBuild(cardPrefab, railCardPrefab, overlay);

            // --- hand fan ---
            var handRect = BattleUiKit.Rect(canvasRect, "HandFan");
            handRect.anchorMin = handRect.anchorMax = new Vector2(0.5f, 0f);
            handRect.anchoredPosition = new Vector2(0f, 130f);
            handRect.sizeDelta = new Vector2(900f, 260f);
            var hand = handRect.gameObject.AddComponent<HandFanView>();
            hand.EditorBuild(cardPrefab);

            // --- HUD texts ---
            var energy = BattleUiKit.Text(canvasRect, "Energy", 34f, TextAlignmentOptions.Center);
            energy.color = new Color(0.95f, 0.72f, 0.25f, 1f);
            Place((RectTransform)energy.transform, new Vector2(0f, 0f), new Vector2(120f, 190f), new Vector2(220f, 48f));

            var message = BattleUiKit.Text(canvasRect, "Message", 20f, TextAlignmentOptions.Center);
            Place((RectTransform)message.transform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 36f));

            // --- piles: draw bottom-left, discard bottom-right, full deck top-right (spec §2) ---
            var buttonSize = new Vector2(112f, 72f);
            var drawPile = PileView.Create(canvasRect, overlay, "뽑을 덱", cardPrefab, buttonSize);
            Place((RectTransform)drawPile.transform, new Vector2(0f, 0f), new Vector2(90f, 70f), buttonSize);
            var discardPile = PileView.Create(canvasRect, overlay, "버린 덱", cardPrefab, buttonSize);
            Place((RectTransform)discardPile.transform, new Vector2(1f, 0f), new Vector2(-90f, 70f), buttonSize);
            var fullDeck = PileView.Create(canvasRect, overlay, "전체 덱", cardPrefab, buttonSize);
            Place((RectTransform)fullDeck.transform, new Vector2(1f, 1f), new Vector2(-90f, -60f), buttonSize);

            // --- buttons ---
            var turnButton = MakeButton(canvasRect, "TurnButton", "턴 실행", 24f, out var turnLabel);
            Place((RectTransform)turnButton.transform, new Vector2(1f, 0.3f), new Vector2(-120f, 0f), new Vector2(180f, 56f));
            var resetButton = MakeButton(canvasRect, "ResetButton", "초기화", 18f, out _);
            Place((RectTransform)resetButton.transform, new Vector2(0f, 1f), new Vector2(90f, -40f), new Vector2(120f, 40f));

            var confirmButton = MakeButton(canvasRect, "ConfirmButton", "확인", 22f, out _);
            Place((RectTransform)confirmButton.transform, new Vector2(1f, 0f), new Vector2(-120f, 150f), new Vector2(160f, 52f));
            confirmButton.gameObject.SetActive(false);

            // --- multi-target dim; clicking the dim cancels without consuming the card ---
            var dimLayer = BattleUiKit.Rect(canvasRect, "SelectionDim");
            BattleUiKit.Stretch(dimLayer);
            var dimImage = BattleUiKit.Image(dimLayer, "Dim", new Color(0f, 0f, 0f, 0.6f));
            BattleUiKit.Stretch(dimImage.rectTransform);
            var dimClickCatcher = dimImage.gameObject.AddComponent<Button>();
            dimClickCatcher.targetGraphic = dimImage;
            dimClickCatcher.transition = Selectable.Transition.None;
            dimLayer.gameObject.SetActive(false);

            // Z-order: each candidate view sits above the global dim and owns its local noncandidate
            // dim. Confirmation, guidance, popups, and the targeting arrow stay on top.
            dimLayer.SetAsLastSibling();
            stage.SetAsLastSibling();
            railRect.SetAsLastSibling();
            handRect.SetAsLastSibling();
            ((RectTransform)confirmButton.transform).SetAsLastSibling();
            ((RectTransform)message.transform).SetAsLastSibling();
            overlay.SetAsLastSibling();

            var selectionGo = new GameObject("CardSelectionController");
            var selection = selectionGo.AddComponent<CardSelectionController>();
            var selectionSo = new SerializedObject(selection);
            selectionSo.FindProperty("_hand").objectReferenceValue = hand;
            selectionSo.FindProperty("_rail").objectReferenceValue = rail;
            selectionSo.FindProperty("_dimLayer").objectReferenceValue = dimLayer.gameObject;
            selectionSo.FindProperty("_confirmButton").objectReferenceValue = confirmButton;
            selectionSo.FindProperty("_arrow").objectReferenceValue = targetingArrow;
            selectionSo.ApplyModifiedPropertiesWithoutUndo();

            // --- controller wiring (field names must match BattleScreenController) ---
            var controllerGo = new GameObject("BattleScreenController");
            var controller = controllerGo.AddComponent<BattleScreenController>();
            var so = new SerializedObject(controller);
            var serializedParty = so.FindProperty("_party");
            serializedParty.arraySize = party.Length;
            for (int i = 0; i < party.Length; i++)
            {
                serializedParty.GetArrayElementAtIndex(i).objectReferenceValue = party[i];
            }
            var arts = so.FindProperty("_enemyArtCards");
            arts.arraySize = EnemyArtCardPaths.Length;
            for (int i = 0; i < EnemyArtCardPaths.Length; i++)
            {
                arts.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CardAsset>(EnemyArtCardPaths[i]);
            }

            so.FindProperty("_hand").objectReferenceValue = hand;
            so.FindProperty("_rail").objectReferenceValue = rail;
            so.FindProperty("_unitPrefab").objectReferenceValue = unitPrefab;
            so.FindProperty("_playerUnitsRow").objectReferenceValue = playerRow;
            so.FindProperty("_enemyUnitsRow").objectReferenceValue = enemyRow;
            so.FindProperty("_drawPile").objectReferenceValue = drawPile;
            so.FindProperty("_discardPile").objectReferenceValue = discardPile;
            so.FindProperty("_fullDeck").objectReferenceValue = fullDeck;
            so.FindProperty("_energyText").objectReferenceValue = energy;
            so.FindProperty("_messageText").objectReferenceValue = message;
            so.FindProperty("_turnButton").objectReferenceValue = turnButton;
            so.FindProperty("_turnButtonLabel").objectReferenceValue = turnLabel;
            so.FindProperty("_resetButton").objectReferenceValue = resetButton;
            so.FindProperty("_selection").objectReferenceValue = selection;
            so.FindProperty("_emptyClickCatcher").objectReferenceValue = emptyClickCatcher;
            so.FindProperty("_dimClickCatcher").objectReferenceValue = dimClickCatcher;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("BattleSceneBuilder: saved " + ScenePath);
        }

        private static void EnsureCardOwnerChip()
        {
            var root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                var cardView = root.GetComponent<CardView>();
                var serialized = new SerializedObject(cardView);
                if (serialized.FindProperty("_ownerChip").objectReferenceValue != null)
                {
                    return;
                }

                var chip = BattleUiKit.Rect((RectTransform)root.transform, "OwnerChip");
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0f);
                chip.pivot = new Vector2(0f, 0f);
                chip.anchoredPosition = new Vector2(18f, 17f);
                chip.sizeDelta = new Vector2(84f, 24f);
                var background = BattleUiKit.Image(chip, "Background", new Color(0.25f, 0.4f, 0.55f, 1f));
                BattleUiKit.Stretch(background.rectTransform);
                background.raycastTarget = false;
                var label = BattleUiKit.Text(chip, "Label", 12f, TextAlignmentOptions.Center);
                BattleUiKit.Stretch(label.rectTransform);
                label.raycastTarget = false;
                chip.gameObject.SetActive(false);

                serialized.FindProperty("_ownerChip").objectReferenceValue = chip.gameObject;
                serialized.FindProperty("_ownerChipBackground").objectReferenceValue = background;
                serialized.FindProperty("_ownerChipText").objectReferenceValue = label;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureUnitPrefab()
        {
            var temporaryRoot = new GameObject("UnitViewPrefabBuilder", typeof(RectTransform));
            try
            {
                var view = UnitView.EditorCreate((RectTransform)temporaryRoot.transform, new Vector2(180f, 250f));
                view.gameObject.name = "UnitView";
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, UnitPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static void EnsureRailCardPrefab()
        {
            var temporaryRoot = new GameObject("RailCardPrefabBuilder", typeof(RectTransform));
            try
            {
                var view = RailCardView.EditorCreate(
                    (RectTransform)temporaryRoot.transform,
                    new Vector2(96f, 132f));
                view.gameObject.name = "RailCardView";
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, RailCardPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static void EnsureTargetingArrowPrefab()
        {
            var temporaryRoot = new GameObject("TargetingArrowPrefabBuilder", typeof(RectTransform));
            try
            {
                var view = TargetingArrowView.EditorCreate((RectTransform)temporaryRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, TargetingArrowPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static void CreateUiCamera()
        {
            var cameraGo = new GameObject("UICamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.16f, 1f);
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.depth = -100f;
            camera.targetDisplay = 0;
        }

        private static RectTransform UnitRow(RectTransform stage, string name, float xMin, float xMax, TextAnchor align)
        {
            var row = BattleUiKit.Rect(stage, name);
            BattleUiKit.Anchor(row, xMin, 0f, xMax, 1f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = align;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Button MakeButton(RectTransform parent, string name, string label, float fontSize, out TMP_Text labelText)
        {
            var root = BattleUiKit.Rect(parent, name);
            var background = BattleUiKit.Image(root, "Background", new Color(0.22f, 0.28f, 0.42f, 1f));
            BattleUiKit.Stretch(background.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            labelText = BattleUiKit.Text(root, "Label", fontSize, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(labelText.rectTransform);
            labelText.text = label;
            return button;
        }
    }
}
