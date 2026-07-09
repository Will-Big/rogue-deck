# Deck/Hand Playtest UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A playable uGUI screen over the pure `DeckCombatSession`: draw a hand, click an execution card to place it on the future zone, click an intervention card then a zone card (2-step) to reorder, resolve and advance turns.

**Architecture:** `DeckPlaytestController` (MonoBehaviour) loads `StarterDeck.asset` (DeckAsset) → `CardSpecMapper` → `DeckCombatSession`, and renders the hand + future zone as reused `CardView` prefabs. Logic lives in the headless-verified `DeckCombatSession`; the UI only displays state and routes clicks. An editor menu builds the Canvas + wires everything.

**Tech Stack:** Unity 6 (6000.5), uGUI, TextMeshPro, Input System UI module. Unity-layer only — the **user verifies in Play** (the pure deck logic is already headless-green). No new headless tests unless a pure helper is added.

**Verification:** Every task here is Unity-layer; the user compiles/Plays in the editor. Task 3 (editor builder) is blind code — expect 1–2 fix iterations from console output.

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `Assets/Unity/CardPresentation.cs` | add `EnergyCost` + `FromDefinition` | Modify |
| `Assets/Unity/CardView.cs` | show cost | Modify |
| `Assets/Unity/DeckPlaytestController.cs` | deck UI driver | Create |
| `Assets/Unity/Editor/DeckPlaytestSceneCreator.cs` | build deck scene + CardView prefab | Create |
| `Assets/Unity/FateWeaverPlaytestController.cs` | old scenario controller | Delete |
| `Assets/Unity/Editor/FateWeaverPlaytestSceneCreator.cs` | old scenario scene builder | Delete |

---

## Task 1: `CardPresentation.EnergyCost` + `FromDefinition`, and `CardView` cost display

**Files:**
- Modify: `Assets/Unity/CardPresentation.cs`
- Modify: `Assets/Unity/CardView.cs`

> Unity-layer (uses `Sprite`); the **user** confirms it compiles. No headless test.

- [ ] **Step 1: Replace `CardPresentation.cs`**

```csharp
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>UI-facing snapshot of a card so CardView never touches core types directly.</summary>
    public readonly struct CardPresentation
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int ExecutionOrder { get; }
        public int EnergyCost { get; }
        public Side Side { get; }
        public string Description { get; }
        public Sprite Art { get; }
        public bool IsLocked { get; }

        public CardPresentation(
            string id, string displayName, int executionOrder, int cost, Side side,
            string description, Sprite art, bool isLocked)
        {
            Id = id;
            DisplayName = displayName;
            ExecutionOrder = executionOrder;
            EnergyCost = cost;
            Side = side;
            Description = description;
            Art = art;
            IsLocked = isLocked;
        }

        /// <summary>Zone card (placed instance) — shows its current execution order.</summary>
        public static CardPresentation From(ExecutionCardInstance card)
        {
            var def = card.Def;
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                card.ExecutionOrder,
                def.EnergyCost,
                def.Side,
                PlaytestKoreanText.CardDescription(def.Id),
                PlaytestCardArt.Sprite(def.Id),
                card.IsLocked);
        }

        /// <summary>Hand card (definition) — execution order is the base value; cost is the key number.</summary>
        public static CardPresentation FromDefinition(CardDefinition def)
        {
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                def.BaseExecutionOrder,
                def.EnergyCost,
                def.Side,
                PlaytestKoreanText.CardDescription(def.Id),
                PlaytestCardArt.Sprite(def.Id),
                false);
        }
    }
}
```

- [ ] **Step 2: Add a cost field to `CardView`**

In `Assets/Unity/CardView.cs`, add the serialized field after `_executionOrderText`:

```csharp
        [SerializeField] private TMP_Text _executionOrderText;
        [SerializeField] private TMP_Text _costText;
```

And in `Bind(...)`, after the `_executionOrderText.text` line, add:

```csharp
            _executionOrderText.text = data.ExecutionOrder.ToString();
            if (_costText != null)
            {
                _costText.text = data.EnergyCost.ToString();
            }
```

- [ ] **Step 3: User verifies compile**

User: Unity reloads. Expected: compiles. (`_costText` is wired later by the builder; null-guarded so an unwired prefab is harmless.)

- [ ] **Step 4: Commit**

```bash
git add Assets/Unity/CardPresentation.cs Assets/Unity/CardView.cs
git commit -m "feat(unity): CardPresentation cost + FromDefinition; CardView cost display"
```

---

## Task 2: `DeckPlaytestController`

**Files:**
- Create: `Assets/Unity/DeckPlaytestController.cs`

> The deck UI driver. Wired by the editor builder (Task 3). Verified in Play (Task 4).

- [ ] **Step 1: Create the controller**

Create `Assets/Unity/DeckPlaytestController.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Playable deck screen over DeckCombatSession: a hand of CardViews (action = one-click place,
    /// intervention = 2-step click targeting) and the future zone of CardViews. UI only — logic is in the session.</summary>
    public sealed class DeckPlaytestController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DeckAsset _deck;

        [Header("Prefab + containers")]
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private RectTransform _handRow;
        [SerializeField] private RectTransform _zoneRow;

        [Header("Text")]
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private TMP_Text _pilesText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _timelineText;

        [Header("Buttons")]
        [SerializeField] private Button _resolveButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _resetButton;

        private DeckCombatSession _session;
        private int _armedInterventionHandIndex = -1;
        private int _firstSwapZoneIndex = -1;
        private readonly List<CardView> _handViews = new List<CardView>();
        private readonly List<CardView> _zoneViews = new List<CardView>();

        private void Awake()
        {
            if (_cardPrefab == null)
            {
                var go = Resources.Load<GameObject>("CardView");
                if (go != null) _cardPrefab = go.GetComponent<CardView>();
            }
        }

        private void Start()
        {
            _resolveButton.onClick.AddListener(ResolveTurn);
            _nextButton.onClick.AddListener(NextTurn);
            _resetButton.onClick.AddListener(StartSession);
            StartSession();
        }

        private void StartSession()
        {
            if (_deck == null)
            {
                SetMessage("DeckAsset이 연결되지 않았습니다. 'Build Deck Playtest Scene'을 다시 실행하세요.");
                return;
            }

            var deckDefs = _deck.ToSpecs().Select(CardSpecMapper.ToDefinition).ToList();
            var enemies = new[] { new Enemy("goblin", 40) };
            _session = new DeckCombatSession(deckDefs, 30, enemies, SampleIntent(), 3, 5, seed: 1);
            ClearArmed();
            SetMessage("전투 시작.");
            RefreshAll();
        }

        private static EnemyIntent SampleIntent()
        {
            IReadOnlyList<CardDefinition> Turn(int dmg) =>
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", 4, dmg) };
            return new EnemyIntent(new[] { Turn(3), Turn(4), Turn(5), Turn(6) });
        }

        // --- input ---

        private void OnHandClicked(int handIndex)
        {
            if (_session.CurrentTurnResolved)
            {
                SetMessage("이미 턴을 해석했습니다. '다음 턴'을 누르세요.");
                return;
            }

            var def = _session.Hand[handIndex];
            if (def.Category == CardCategory.Execution)
            {
                SetMessage(_session.PlayExecutionCard(handIndex)
                    ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
                    : "운명력이 부족하거나 낼 수 없습니다.");
                ClearArmed();
                RefreshAll();
                return;
            }

            _armedInterventionHandIndex = handIndex;
            _firstSwapZoneIndex = -1;
            SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name) + " — 줄에서 대상을 선택하세요.");
            RefreshHand();
            RefreshZone();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_armedInterventionHandIndex < 0)
            {
                return;
            }

            var def = _session.Hand[_armedInterventionHandIndex];
            var needsTwo = def.InterventionAction != null && def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder;

            if (needsTwo && _firstSwapZoneIndex < 0)
            {
                _firstSwapZoneIndex = zoneIndex;
                SetMessage("교환할 두 번째 카드를 선택하세요.");
                RefreshZone();
                return;
            }

            bool ok = needsTwo
                ? _session.PlayInterventionCard(_armedInterventionHandIndex, _firstSwapZoneIndex, zoneIndex)
                : _session.PlayInterventionCard(_armedInterventionHandIndex, zoneIndex);

            SetMessage(ok ? "개입 카드 적용." : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            ClearArmed();
            RefreshAll();
        }

        private void ResolveTurn()
        {
            if (_session.CurrentTurnResolved) return;
            _session.ResolveTurn();
            ClearArmed();
            SetMessage("턴 해석 완료.");
            RefreshAll();
        }

        private void NextTurn()
        {
            if (!_session.BeginNextTurn()) return;
            ClearArmed();
            SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            RefreshAll();
        }

        private void ClearArmed()
        {
            _armedInterventionHandIndex = -1;
            _firstSwapZoneIndex = -1;
        }

        // --- render ---

        private void RefreshAll()
        {
            RefreshZone();
            RefreshHand();
            RefreshState();
            RefreshTimeline();
            RefreshButtons();
        }

        private void RefreshHand()
        {
            foreach (var v in _handViews) Destroy(v.gameObject);
            _handViews.Clear();

            for (int i = 0; i < _session.Hand.Count; i++)
            {
                var view = Instantiate(_cardPrefab, _handRow);
                int captured = i;
                view.Bind(CardPresentation.FromDefinition(_session.Hand[i]), () => OnHandClicked(captured));
                view.SetSelection(i == _armedInterventionHandIndex ? CardView.SelectionKind.Primary : CardView.SelectionKind.None);
                _handViews.Add(view);
            }
        }

        private void RefreshZone()
        {
            foreach (var v in _zoneViews) Destroy(v.gameObject);
            _zoneViews.Clear();

            var order = _session.CurrentOrder;
            for (int i = 0; i < order.Count; i++)
            {
                var view = Instantiate(_cardPrefab, _zoneRow);
                int captured = i;
                view.Bind(CardPresentation.From(order[i]), () => OnZoneClicked(captured));
                view.SetSelection(i == _firstSwapZoneIndex ? CardView.SelectionKind.Secondary : CardView.SelectionKind.None);
                _zoneViews.Add(view);
            }
        }

        private void RefreshState()
        {
            var sb = new StringBuilder();
            sb.Append("턴 ").Append(_session.TurnIndex + 1)
              .Append("    플레이어 HP: ").Append(_session.State.PlayerHp)
              .Append("    운명력: ").Append(_session.FateEnergy)
              .Append("    ").Append(StatusText(_session.State.PlayerStatuses));
            foreach (var enemy in _session.State.Enemies)
            {
                var name = enemy.Id == "goblin" ? "고블린" : enemy.Id;
                sb.Append('\n').Append(name).Append(" HP: ").Append(enemy.Hp)
                  .Append("    ").Append(StatusText(enemy.Statuses));
            }

            if (_session.IsComplete)
            {
                sb.Append("\n결과: ").Append(PlaytestKoreanText.OutcomeName(_session.Outcome));
            }

            _stateText.text = sb.ToString();
            _pilesText.text = "덱 " + _session.DrawCount + " · 버림 " + _session.DiscardCount;
        }

        private void RefreshTimeline()
        {
            if (_session.LastTimeline == null)
            {
                _timelineText.text = string.Empty;
                return;
            }

            var sb = new StringBuilder("해석 결과 (").Append(_session.TurnIndex + 1).Append("턴)\n");
            foreach (var evt in _session.LastTimeline)
            {
                if (evt is CardResolved card)
                {
                    sb.Append("- ").Append(PlaytestKoreanText.CardName(card.CardId, card.CardId))
                      .Append(" | ").Append(PlaytestKoreanText.ConditionName(card.ConditionTier))
                      .Append(" | 피해 ").Append(card.DamageDealt).Append('\n');
                }
                else if (evt is TurnEnded ended)
                {
                    sb.Append("전투 결과: ").Append(PlaytestKoreanText.OutcomeName(ended.Outcome)).Append('\n');
                }
            }

            _timelineText.text = sb.ToString();
        }

        private void RefreshButtons()
        {
            _resolveButton.interactable = !_session.CurrentTurnResolved;
            _nextButton.interactable = _session.CurrentTurnResolved && !_session.IsComplete;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }

        private static string StatusText(StatusBag bag)
        {
            var parts = new List<string>();
            foreach (var status in bag.All)
            {
                var amount = status.Magnitude > 0 ? status.Magnitude : status.Count;
                var name = PlaytestKoreanText.StatusName(status.Key);
                parts.Add(amount > 0 ? name + "(" + amount + ")" : name);
            }

            return parts.Count == 0 ? string.Empty : "[" + string.Join(", ", parts) + "]";
        }
    }
}
```

- [ ] **Step 2: User verifies compile**

User: Unity reloads. Expected: compiles. (Serialized fields wired by the builder next.) Report missing-member errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Unity/DeckPlaytestController.cs
git commit -m "feat(unity): DeckPlaytestController (hand play + 2-step intervention targeting)"
```

---

## Task 3: Editor builder — deck scene + CardView prefab (with cost)

**Files:**
- Create: `Assets/Unity/Editor/DeckPlaytestSceneCreator.cs`

> Blind editor code — the **user** runs the menu and reports. Expect a fix iteration (anchors/sizes/wiring).

- [ ] **Step 1: Create the builder**

Create `Assets/Unity/Editor/DeckPlaytestSceneCreator.cs`:

```csharp
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity.Editor
{
    public static class DeckPlaytestSceneCreator
    {
        public const string ScenePath = "Assets/Scenes/FateWeaverPlaytest.unity";
        public const string PrefabPath = "Assets/Unity/Resources/CardView.prefab";
        private const string FontAssetPath = "Assets/Unity/Resources/Fonts/KoreanTMP.asset";
        private const string DeckAssetPath = "Assets/Unity/Cards/StarterDeck.asset";

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

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            }

            var bg = NewImage("Background", canvasGo.transform, new Color(0.12f, 0.14f, 0.18f, 1f));
            Stretch(bg.rectTransform);

            var root = NewColumn("Root", canvasGo.transform);
            var rootRt = (RectTransform)root.transform;
            Stretch(rootRt);
            rootRt.offsetMin = new Vector2(20, 20);
            rootRt.offsetMax = new Vector2(-20, -20);

            var state = NewText("State", root.transform, font, 18, FontStyles.Bold, Color.white);
            var zoneLabel = NewText("ZoneLabel", root.transform, font, 14, FontStyles.Normal, new Color(0.4f, 0.85f, 1f));
            zoneLabel.text = "미래 영역 (실행 순서 순) — 개입 타깃";
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
            Directory.CreateDirectory("Assets/Unity/Resources");

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

            var cost = NewText("EnergyCost", rootGo.transform, font, 18, FontStyles.Bold, new Color(0.6f, 0.85f, 1f));
            AnchorBand(cost.rectTransform, 4, 26);
            cost.alignment = TextAlignmentOptions.TopLeft;
            var executionOrder = NewText("ExecutionOrder", rootGo.transform, font, 14, FontStyles.Bold, new Color(0.95f, 0.85f, 0.4f));
            AnchorBand(executionOrder.rectTransform, 4, 26);
            executionOrder.alignment = TextAlignmentOptions.TopRight;
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
            so.FindProperty("_executionOrderText").objectReferenceValue = executionOrder;
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

        private static void EnsureSpriteImport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Unity/Resources" }))
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
```

- [ ] **Step 2: User runs the build menu**

User: run `Fate Weaver ▸ Build Deck Playtest Scene`, open the scene, press Play. Expected: hand of cards (with cost), future zone with the goblin card, clicking an execution card places it, clicking an intervention card then a zone card reorders, `턴 실행`/`다음 턴` work. Report console errors / layout issues.

- [ ] **Step 3: Commit (after user confirms it builds)**

```bash
git add Assets/Unity/Editor/DeckPlaytestSceneCreator.cs Assets/Unity/Resources/CardView.prefab Assets/Scenes/FateWeaverPlaytest.unity
git commit -m "feat(unity): editor builder for the deck playtest scene"
```

---

## Task 4: Remove the old scenario controller/builder + user Play verification

**Files:**
- Delete: `Assets/Unity/FateWeaverPlaytestController.cs` (+ `.meta`)
- Delete: `Assets/Unity/Editor/FateWeaverPlaytestSceneCreator.cs` (+ `.meta`)

- [ ] **Step 1: Delete the superseded scenario UI**

```bash
git rm Assets/Unity/FateWeaverPlaytestController.cs Assets/Unity/FateWeaverPlaytestController.cs.meta
git rm Assets/Unity/Editor/FateWeaverPlaytestSceneCreator.cs Assets/Unity/Editor/FateWeaverPlaytestSceneCreator.cs.meta
```

- [ ] **Step 2: User verifies compile + Play**

User: Unity reloads (no references to the deleted controller remain — the scene now uses `DeckPlaytestController`). Re-run `Build Deck Playtest Scene` if needed, Play, and confirm: place actions, 2-step intervention targeting reorders the zone, resolve shows the timeline, next turn redraws, win/lose stops. Report issues.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore(unity): remove scenario-based playtest UI (superseded by deck UI)"
```

---

## Self-review notes (for the implementer)

- **Spec coverage:** layout + 2-step interaction (controller, Task 2), CardView cost + CardPresentation.FromDefinition (Task 1), DeckAsset→CardSpecMapper→DeckCombatSession wiring (controller Start), editor builder + CardView prefab reuse (Task 3), removal of scenario UI (Task 4). Prefab-ization = the reused `CardView` prefab.
- **Out of scope:** drag-drop, animation, multiple enemies, intent assets, reward/map screens.
- **Verification reality:** all Unity-layer → user Plays. The deck logic underneath is headless-green. Task 3 is the blind-risk editor code.
- **Art:** cards without art (guard/heavy_strike/cover/pull_forward/swap_positions) show the side-tinted fallback until art is generated via `Sample/CardArtPrompts.md`.
```
