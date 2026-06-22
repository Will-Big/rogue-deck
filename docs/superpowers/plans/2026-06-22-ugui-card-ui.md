# uGUI + TMP Image-Based Card UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the IMGUI text playtest with a uGUI card UI — each future-zone card drawn as an art image + name/initiative + a wrapped Korean description block, built into the scene via an editor menu, text rendered with TextMeshPro + a dynamic Korean (Malgun Gothic) font.

**Architecture:** A pure view-model (`CardPresentation`) decouples the `CardView` prefab from core types. Presentation lookups (`PlaytestCardArt`, `PlaytestKoreanText.CardDescription`) map a card id to its Resources sprite and Korean text. `FateWeaverPlaytestController` (rewritten, no `OnGUI`) instantiates one `CardView` per `MultiTurnPlaytestSession.CurrentOrder` card and binds it. An editor menu builds the Canvas + `CardView.prefab` and wires the controller, plus a menu that creates the Korean TMP font asset.

**Tech Stack:** Unity 6 (C# 9), uGUI (`UnityEngine.UI`), TextMeshPro (`Unity.TextMeshPro`), `MultiTurnPlaytestSession` (pure C#, already done), Resources card art (already committed).

**Verification note:** Unity-layer code (MonoBehaviour, prefab, editor builder) is NOT headless-compilable; the user runs Unity to verify. Pure string lookups (`PlaytestCardArt.ResolveArtName`, `PlaytestKoreanText.CardDescription`) get EditMode tests in the `FateWeaver.Tests.UnityEditMode` assembly, which the **user runs in Unity's Test Runner** (the headless dotnet project cannot compile the Unity layer). Tasks 7–8 (editor tooling) and 6 (controller) require iterative verification against the user's editor.

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `Assets/FateWeaver/Unity/FateWeaver.Unity.asmdef` | add TMP + UI asm refs | Modify |
| `Assets/FateWeaver/Unity/Editor/FateWeaver.Unity.Editor.asmdef` | add TMP + UI asm refs | Modify |
| `Assets/FateWeaver/Unity/PlaytestKoreanText.cs` | add `CardDescription(id)` | Modify |
| `Assets/FateWeaver/Unity/PlaytestCardArt.cs` | id → art name / Sprite | Create |
| `Assets/FateWeaver/Unity/CardPresentation.cs` | view-model from `ActionCardInstance` | Create |
| `Assets/FateWeaver/Unity/CardView.cs` | card prefab component | Create |
| `Assets/FateWeaver/Unity/FateWeaverPlaytestController.cs` | uGUI controller (rewrite) | Modify |
| `Assets/FateWeaver/Unity/RuntimeOsFontLoader.cs` | IMGUI-only OS font (remove) | Delete |
| `Assets/FateWeaver/Unity/Editor/FateWeaverPlaytestSceneCreator.cs` | scene+prefab builder menu | Modify |
| `Assets/FateWeaver/Unity/Editor/KoreanTmpFontCreator.cs` | Korean TMP font menu | Create |
| `Assets/FateWeaver/Tests/UnityEditMode/PlaytestCardArtTests.cs` | EditMode tests (art name) | Create |
| `Assets/FateWeaver/Tests/UnityEditMode/CardDescriptionTests.cs` | EditMode tests (descriptions) | Create |
| `Assets/FateWeaver/Tests/UnityEditMode/RuntimeOsFontLoaderTests.cs` | remove (loader deleted) | Delete |
| `Assets/FateWeaver/Unity/PLAYTEST.md` | new setup/run flow | Modify |

---

## Task 1: Add TMP + uGUI assembly references

**Files:**
- Modify: `Assets/FateWeaver/Unity/FateWeaver.Unity.asmdef`
- Modify: `Assets/FateWeaver/Unity/Editor/FateWeaver.Unity.Editor.asmdef`

- [ ] **Step 1: Add refs to the runtime asmdef**

Replace the `"references"` array in `FateWeaver.Unity.asmdef` with:

```json
    "references": [
        "FateWeaver.Core",
        "FateWeaver.Simulation",
        "Unity.TextMeshPro",
        "UnityEngine.UI"
    ],
```

- [ ] **Step 2: Add refs to the editor asmdef**

Replace the `"references"` array in `Editor/FateWeaver.Unity.Editor.asmdef` with:

```json
    "references": [
        "FateWeaver.Unity",
        "Unity.TextMeshPro",
        "UnityEngine.UI"
    ],
```

- [ ] **Step 3: User verifies compile**

User: let Unity reload. Expected: project still compiles (no new code yet; the IMGUI controller is unchanged). Report any "assembly not found: Unity.TextMeshPro / UnityEngine.UI" — if so, the package assembly name differs and we adjust.

- [ ] **Step 4: Commit**

```bash
git add Assets/FateWeaver/Unity/FateWeaver.Unity.asmdef Assets/FateWeaver/Unity/Editor/FateWeaver.Unity.Editor.asmdef
git commit -m "build(unity): reference TextMeshPro and UnityEngine.UI"
```

---

## Task 2: `PlaytestKoreanText.CardDescription(id)`

**Files:**
- Modify: `Assets/FateWeaver/Unity/PlaytestKoreanText.cs`
- Test: `Assets/FateWeaver/Tests/UnityEditMode/CardDescriptionTests.cs`

- [ ] **Step 1: Write the failing EditMode test**

Create `Assets/FateWeaver/Tests/UnityEditMode/CardDescriptionTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CardDescriptionTests
    {
        [Test]
        public void Player_cards_have_curated_text()
        {
            Assert.AreEqual("피해 2.", PlaytestKoreanText.CardDescription("slash"));
            Assert.AreEqual(
                "방어 2. 바로 앞에서 적이 공격했다면 피해 7 (3번째 안이면 +2).",
                PlaytestKoreanText.CardDescription("counter"));
        }

        [Test]
        public void Suffixed_ids_match_by_prefix()
        {
            Assert.AreEqual(
                "피해 2. 이번 턴에 가장 먼저 발동하면 대신 피해 10.",
                PlaytestKoreanText.CardDescription("quick_cut_t1"));
        }

        [Test]
        public void Unknown_id_returns_empty()
        {
            Assert.AreEqual(string.Empty, PlaytestKoreanText.CardDescription("nope"));
        }
    }
}
```

- [ ] **Step 2: User runs the test to confirm it fails**

User: Window ▸ General ▸ Test Runner ▸ EditMode ▸ run `CardDescriptionTests`.
Expected: FAIL — `CardDescription` does not exist (compile error). (I cannot run Unity EditMode tests; the user reports pass/fail.)

- [ ] **Step 3: Add the method**

In `PlaytestKoreanText.cs`, add after `CardName(...)`:

```csharp
        public static string CardDescription(string id)
        {
            if (id.StartsWith("quick_cut", StringComparison.Ordinal))
                return "피해 2. 이번 턴에 가장 먼저 발동하면 대신 피해 10.";
            if (id.StartsWith("wrist_cut", StringComparison.Ordinal))
                return "피해 3. 다음 플레이어 조건 보상을 무효화.";
            if (id.StartsWith("preemptive_thrust", StringComparison.Ordinal))
                return "선제 일격.";
            if (id.StartsWith("goblin_jab", StringComparison.Ordinal))
                return "고블린의 빠른 찌르기.";

            switch (id)
            {
                case "slash": return "피해 2.";
                case "mark": return "다음 카드가 플레이어 공격이고 적 공격보다 먼저면, 다음 플레이어 공격 피해 +6.";
                case "counter": return "방어 2. 바로 앞에서 적이 공격했다면 피해 7 (3번째 안이면 +2).";
                case "chain": return "피해 1. 바로 앞이 플레이어 행동 카드이고 3번째 안이면 추가 피해 5.";
                case "prep": return "피해 1.";
                default: return string.Empty;
            }
        }
```

(`using System;` is already present in the file for `StringComparison`.)

- [ ] **Step 4: User runs the test to confirm it passes**

User: re-run `CardDescriptionTests`. Expected: 3 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Unity/PlaytestKoreanText.cs Assets/FateWeaver/Tests/UnityEditMode/CardDescriptionTests.cs
git commit -m "feat(unity): curated Korean card descriptions"
```

---

## Task 3: `PlaytestCardArt` (id → art name / Sprite)

**Files:**
- Create: `Assets/FateWeaver/Unity/PlaytestCardArt.cs`
- Test: `Assets/FateWeaver/Tests/UnityEditMode/PlaytestCardArtTests.cs`

- [ ] **Step 1: Write the failing EditMode test (pure resolution only)**

Create `Assets/FateWeaver/Tests/UnityEditMode/PlaytestCardArtTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Unity;

namespace FateWeaver.Tests.UnityEditMode
{
    public class PlaytestCardArtTests
    {
        [Test]
        public void Renamed_ids_map_to_art_files()
        {
            Assert.AreEqual("mark_target", PlaytestCardArt.ResolveArtName("mark"));
            Assert.AreEqual("counter_stance", PlaytestCardArt.ResolveArtName("counter"));
            Assert.AreEqual("chain_slash", PlaytestCardArt.ResolveArtName("chain"));
            Assert.AreEqual("slash", PlaytestCardArt.ResolveArtName("slash"));
        }

        [Test]
        public void Suffixed_ids_normalize_by_prefix()
        {
            Assert.AreEqual("quick_cut", PlaytestCardArt.ResolveArtName("quick_cut_t3"));
            Assert.AreEqual("wrist_cut", PlaytestCardArt.ResolveArtName("wrist_cut_t2"));
            Assert.AreEqual("preemptive_thrust", PlaytestCardArt.ResolveArtName("preemptive_thrust_t1"));
            Assert.AreEqual("goblin_jab", PlaytestCardArt.ResolveArtName("goblin_jab_t1"));
        }

        [Test]
        public void Cards_without_art_resolve_to_null()
        {
            Assert.IsNull(PlaytestCardArt.ResolveArtName("prep"));
            Assert.IsNull(PlaytestCardArt.ResolveArtName(""));
        }
    }
}
```

- [ ] **Step 2: User runs the test to confirm it fails**

User: Test Runner ▸ EditMode ▸ `PlaytestCardArtTests`. Expected: FAIL (type does not exist).

- [ ] **Step 3: Create the class**

Create `Assets/FateWeaver/Unity/PlaytestCardArt.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Maps a card id to its art under Resources/. Pure id→name resolution is unit-tested;
    /// Sprite(...) wraps it with a cached Resources.Load. Resources root holds the PNGs by file name.</summary>
    public static class PlaytestCardArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static string ResolveArtName(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return null;
            }

            if (cardId.StartsWith("quick_cut", StringComparison.Ordinal)) return "quick_cut";
            if (cardId.StartsWith("wrist_cut", StringComparison.Ordinal)) return "wrist_cut";
            if (cardId.StartsWith("preemptive_thrust", StringComparison.Ordinal)) return "preemptive_thrust";
            if (cardId.StartsWith("goblin_jab", StringComparison.Ordinal)) return "goblin_jab";

            switch (cardId)
            {
                case "slash": return "slash";
                case "mark": return "mark_target";
                case "counter": return "counter_stance";
                case "chain": return "chain_slash";
                default: return null;
            }
        }

        public static Sprite Sprite(string cardId)
        {
            var name = ResolveArtName(cardId);
            if (name == null)
            {
                return null;
            }

            if (Cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(name);
            Cache[name] = sprite; // cache null too, to avoid repeated misses
            return sprite;
        }
    }
}
```

- [ ] **Step 4: User runs the test to confirm it passes**

User: re-run `PlaytestCardArtTests`. Expected: 3 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Unity/PlaytestCardArt.cs Assets/FateWeaver/Tests/UnityEditMode/PlaytestCardArtTests.cs
git commit -m "feat(unity): card art lookup (id normalization + cached Sprite load)"
```

---

## Task 4: `CardPresentation` view-model

**Files:**
- Create: `Assets/FateWeaver/Unity/CardPresentation.cs`

> No standalone test: this is a thin assembler over already-tested lookups; it is exercised by the controller in Play.

- [ ] **Step 1: Create the struct + factory**

Create `Assets/FateWeaver/Unity/CardPresentation.cs`:

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
        public int Initiative { get; }
        public Side Side { get; }
        public string Description { get; }
        public Sprite Art { get; }
        public bool IsLocked { get; }

        public CardPresentation(
            string id, string displayName, int initiative, Side side,
            string description, Sprite art, bool isLocked)
        {
            Id = id;
            DisplayName = displayName;
            Initiative = initiative;
            Side = side;
            Description = description;
            Art = art;
            IsLocked = isLocked;
        }

        public static CardPresentation From(ActionCardInstance card)
        {
            var def = card.Def;
            return new CardPresentation(
                def.Id,
                PlaytestKoreanText.CardName(def.Id, def.Name),
                card.Initiative,
                def.Side,
                PlaytestKoreanText.CardDescription(def.Id),
                PlaytestCardArt.Sprite(def.Id),
                card.IsLocked);
        }
    }
}
```

- [ ] **Step 2: User verifies compile**

User: Unity reloads. Expected: compiles. (If `card.Initiative` / `card.IsLocked` / `card.Def` names differ, report — they are the same members the old IMGUI controller used, so they should match.)

- [ ] **Step 3: Commit**

```bash
git add Assets/FateWeaver/Unity/CardPresentation.cs
git commit -m "feat(unity): CardPresentation view-model"
```

---

## Task 5: `CardView` prefab component

**Files:**
- Create: `Assets/FateWeaver/Unity/CardView.cs`

- [ ] **Step 1: Create the component**

Create `Assets/FateWeaver/Unity/CardView.cs`:

```csharp
using System;
using FateWeaver.Core.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>One card widget: art (or side-tinted fallback) + name/initiative + description block,
    /// a selection outline and a lock badge. Bound from a CardPresentation; clicking raises onClick.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public enum SelectionKind { None, Primary, Secondary }

        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _initiativeText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Image _selectionOutline;
        [SerializeField] private GameObject _lockBadge;
        [SerializeField] private Button _button;

        private static readonly Color OutlineNone = new Color(0f, 0f, 0f, 0f);
        private static readonly Color OutlinePrimary = new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color OutlineSecondary = new Color(0.35f, 0.75f, 0.95f, 1f);
        private static readonly Color EnemyTint = new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint = new Color(0.22f, 0.28f, 0.36f, 1f);

        public void Bind(CardPresentation data, Action onClick)
        {
            _nameText.text = data.DisplayName;
            _initiativeText.text = data.Initiative.ToString();
            _descriptionText.text = data.Description;

            if (data.Art != null)
            {
                _art.enabled = true;
                _art.sprite = data.Art;
                _artFallback.enabled = false;
            }
            else
            {
                _art.enabled = false;
                _artFallback.enabled = true;
                _artFallback.color = data.Side == Side.Enemy ? EnemyTint : PlayerTint;
            }

            if (_lockBadge != null)
            {
                _lockBadge.SetActive(data.IsLocked);
            }

            _button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick());
            }

            SetSelection(SelectionKind.None);
        }

        public void SetSelection(SelectionKind kind)
        {
            _selectionOutline.color =
                kind == SelectionKind.Primary ? OutlinePrimary :
                kind == SelectionKind.Secondary ? OutlineSecondary :
                OutlineNone;
        }
    }
}
```

- [ ] **Step 2: User verifies compile**

User: Unity reloads. Expected: compiles (TMPro/UI refs from Task 1 resolve). Serialized fields are wired later by the editor builder (Task 7).

- [ ] **Step 3: Commit**

```bash
git add Assets/FateWeaver/Unity/CardView.cs
git commit -m "feat(unity): CardView card widget component"
```

---

## Task 6: Rewrite `FateWeaverPlaytestController` for uGUI

**Files:**
- Modify (full replace): `Assets/FateWeaver/Unity/FateWeaverPlaytestController.cs`

> The controller exposes `[SerializeField]` references that the editor builder (Task 7) wires. It no longer uses `OnGUI`. Verification is in Play after Task 7 builds the scene.

- [ ] **Step 1: Replace the file contents**

Replace `FateWeaverPlaytestController.cs` entirely with:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>uGUI playtest driver: instantiates a CardView per future-zone card, applies fate actions,
    /// resolves/advances turns. UI objects are wired by FateWeaverPlaytestSceneCreator.</summary>
    public sealed class FateWeaverPlaytestController : MonoBehaviour
    {
        [Header("Card row")]
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private RectTransform _cardRow;

        [Header("Scenario picker")]
        [SerializeField] private RectTransform _scenarioRow;
        [SerializeField] private Button _scenarioButtonTemplate;

        [Header("Text panels")]
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _stateText;
        [SerializeField] private TMP_Text _selectionText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _timelineText;

        [Header("Fate action buttons")]
        [SerializeField] private Button _initMinusButton;
        [SerializeField] private Button _initPlusButton;
        [SerializeField] private Button _swapButton;
        [SerializeField] private Button _lockButton;

        [Header("Flow buttons")]
        [SerializeField] private Button _resolveButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _resetButton;

        private MultiTurnPlaytestSession _session;
        private MultiTurnScenario _currentScenario;
        private string _primaryCardId;
        private string _secondaryCardId;
        private readonly List<CardView> _cardViews = new List<CardView>();

        private void Start()
        {
            BuildScenarioButtons();
            WireButtons();
            LoadScenario(SampleMultiTurnScenarios.All[0].Build());
        }

        private void BuildScenarioButtons()
        {
            if (_scenarioButtonTemplate == null || _scenarioRow == null)
            {
                return;
            }

            _scenarioButtonTemplate.gameObject.SetActive(false);
            foreach (var entry in SampleMultiTurnScenarios.All)
            {
                var capturedId = entry.Id;
                var button = Instantiate(_scenarioButtonTemplate, _scenarioRow);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = PlaytestKoreanText.ScenarioName(capturedId, capturedId);
                }

                button.onClick.AddListener(() => LoadScenario(SampleMultiTurnScenarios.Find(capturedId)));
            }
        }

        private void WireButtons()
        {
            _initMinusButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, -2)));
            _initPlusButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.ChangeInitiative, 1, 2)));
            _swapButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.SwapInitiative, 1, 0), needsSecondary: true));
            _lockButton.onClick.AddListener(() => Apply(new FateActionData(FateActionKeys.Lock, 1, 0)));

            _resolveButton.onClick.AddListener(ResolveTurn);
            _nextButton.onClick.AddListener(NextTurn);
            _resetButton.onClick.AddListener(() => LoadScenario(_currentScenario));
        }

        private void LoadScenario(MultiTurnScenario scenario)
        {
            _currentScenario = scenario;
            _session = new MultiTurnPlaytestSession(scenario);
            _primaryCardId = null;
            _secondaryCardId = null;
            SetMessage("시나리오를 불러왔습니다.");
            RefreshAll();
        }

        private void ResolveTurn()
        {
            if (_session.CurrentTurnResolved)
            {
                return;
            }

            _session.ResolveTurn();
            SetMessage("턴 실행 완료.");
            RefreshAll();
        }

        private void NextTurn()
        {
            if (!_session.AdvanceTurn())
            {
                return;
            }

            _primaryCardId = null;
            _secondaryCardId = null;
            SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            RefreshAll();
        }

        private void Apply(FateActionData action, bool needsSecondary = false)
        {
            if (_primaryCardId == null || (needsSecondary && _secondaryCardId == null))
            {
                SetMessage(needsSecondary ? "주 대상과 보조 대상을 선택하세요." : "주 대상을 선택하세요.");
                return;
            }

            try
            {
                var result = _session.ApplyFateAction(
                    action, _primaryCardId, needsSecondary ? _secondaryCardId : null);
                SetMessage(result.AppliedCount == 1
                    ? PlaytestKoreanText.FateActionName(action.Key) + " 적용 완료."
                    : "액션을 적용할 수 없습니다. 운명력·고정·대상 규칙을 확인하세요.");
            }
            catch (Exception exception)
            {
                SetMessage("오류: " + exception.Message);
            }

            RefreshAll();
        }

        private void SelectCard(string cardId)
        {
            if (_primaryCardId == cardId)
            {
                _primaryCardId = null;
                _secondaryCardId = null;
            }
            else if (_primaryCardId == null || _secondaryCardId != null)
            {
                _primaryCardId = cardId;
                _secondaryCardId = null;
            }
            else
            {
                _secondaryCardId = cardId;
            }

            RefreshSelection();
        }

        private void RefreshAll()
        {
            RefreshCards();
            RefreshState();
            RefreshTimeline();
            RefreshButtons();
        }

        private void RefreshCards()
        {
            for (int i = 0; i < _cardViews.Count; i++)
            {
                Destroy(_cardViews[i].gameObject);
            }

            _cardViews.Clear();

            foreach (var card in _session.CurrentOrder)
            {
                var view = Instantiate(_cardPrefab, _cardRow);
                var capturedId = card.Def.Id;
                view.Bind(CardPresentation.From(card), () => SelectCard(capturedId));
                _cardViews.Add(view);
            }

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (var view in _cardViews)
            {
                view.SetSelection(CardView.SelectionKind.None);
            }

            for (int i = 0; i < _cardViews.Count && i < _session.CurrentOrder.Count; i++)
            {
                var id = _session.CurrentOrder[i].Def.Id;
                if (id == _primaryCardId)
                {
                    _cardViews[i].SetSelection(CardView.SelectionKind.Primary);
                }
                else if (id == _secondaryCardId)
                {
                    _cardViews[i].SetSelection(CardView.SelectionKind.Secondary);
                }
            }

            _selectionText.text = "주 대상: " + NameOf(_primaryCardId) + "    보조 대상: " + NameOf(_secondaryCardId);
        }

        private void RefreshState()
        {
            _headerText.text = PlaytestKoreanText.ScenarioName(_currentScenario.Id, _session.Name)
                + "    턴 " + (_session.TurnIndex + 1) + " / " + _session.TurnCount;

            var sb = new StringBuilder();
            sb.Append("플레이어 HP: ").Append(_session.State.PlayerHp)
              .Append("    운명력: ").Append(_session.State.FateEnergy)
              .Append("    ").Append(StatusText(_session.State.PlayerStatuses));
            foreach (var enemy in _session.State.Enemies)
            {
                var enemyName = enemy.Id == "goblin" ? "고블린" : enemy.Id;
                sb.Append('\n').Append(enemyName).Append(" HP: ").Append(enemy.Hp)
                  .Append("    ").Append(StatusText(enemy.Statuses));
            }

            if (_session.IsComplete)
            {
                sb.Append("\n결과: ").Append(PlaytestKoreanText.OutcomeName(_session.Outcome));
            }

            _stateText.text = sb.ToString();
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
            var canAct = !_session.CurrentTurnResolved;
            _initMinusButton.interactable = canAct;
            _initPlusButton.interactable = canAct;
            _swapButton.interactable = canAct;
            _lockButton.interactable = canAct;
            _resolveButton.interactable = canAct;
            _nextButton.interactable = _session.CurrentTurnResolved && !_session.IsComplete;
        }

        private string NameOf(string cardId)
        {
            if (cardId == null)
            {
                return "-";
            }

            foreach (var card in _session.CurrentOrder)
            {
                if (card.Def.Id == cardId)
                {
                    return PlaytestKoreanText.CardName(card.Def.Id, card.Def.Name);
                }
            }

            return cardId;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null)
            {
                _messageText.text = message;
            }
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

User: Unity reloads. Expected: compiles. (`RuntimeOsFontLoader` is no longer referenced here — it is deleted in Task 9.) Report any missing member errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/FateWeaver/Unity/FateWeaverPlaytestController.cs
git commit -m "feat(unity): rewrite playtest controller on uGUI CardViews"
```

---

## Task 7: Editor scene + `CardView.prefab` builder

**Files:**
- Modify (full replace): `Assets/FateWeaver/Unity/Editor/FateWeaverPlaytestSceneCreator.cs`

> This is the riskiest task: ~UI construction code that I cannot run. Build it, then the user runs the menu and reports. Expect 1–2 fix iterations. Inline execution recommended so I can react to editor errors quickly.

- [ ] **Step 1: Replace the file with the uGUI builder**

Replace `FateWeaverPlaytestSceneCreator.cs` with:

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
    public static class FateWeaverPlaytestSceneCreator
    {
        public const string ScenePath = "Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity";
        public const string PrefabPath = "Assets/FateWeaver/Unity/Prefabs/CardView.prefab";
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

            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
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
            Directory.CreateDirectory("Assets/FateWeaver/Unity/Prefabs");

            var rootGo = new GameObject("CardView", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(CardView));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(220, 340);
            rootGo.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);
            var le = rootGo.GetComponent<LayoutElement>();
            le.preferredWidth = 220;
            le.preferredHeight = 340;

            var outline = NewImage("SelectionOutline", rootGo.transform, new Color(0, 0, 0, 0));
            Stretch(outline.rectTransform);
            outline.rectTransform.offsetMin = new Vector2(-3, -3);
            outline.rectTransform.offsetMax = new Vector2(3, 3);

            var art = NewImage("Art", rootGo.transform, Color.white);
            art.preserveAspect = true;
            AnchorTop(art.rectTransform, 220);
            var artFallback = NewImage("ArtFallback", rootGo.transform, new Color(0.22f, 0.28f, 0.36f, 1f));
            AnchorTop(artFallback.rectTransform, 220);
            artFallback.enabled = false;

            var nameText = NewText("Name", rootGo.transform, font, 18, FontStyles.Bold, Color.white);
            AnchorBand(nameText.rectTransform, 224, 28);
            var initText = NewText("Initiative", rootGo.transform, font, 16, FontStyles.Bold, new Color(0.95f, 0.85f, 0.4f));
            AnchorBand(initText.rectTransform, 224, 28);
            initText.alignment = TextAlignmentOptions.TopRight;
            var descText = NewText("Description", rootGo.transform, font, 14, FontStyles.Normal, new Color(0.9f, 0.9f, 0.9f));
            AnchorBand(descText.rectTransform, 254, 80);
            descText.textWrappingMode = TextWrappingModes.Normal;

            var lockBadge = NewText("LockBadge", rootGo.transform, font, 14, FontStyles.Bold, new Color(1f, 0.5f, 0.5f));
            lockBadge.text = "고정";
            AnchorBand(((RectTransform)lockBadge.transform), 224, 24);
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
            so.FindProperty("_button").objectReferenceValue = rootGo.GetComponent<Button>();
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
            go.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.34f, 1f);
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 130;
            le.minHeight = 40;
            var text = NewText("Label", go.transform, font, 16, FontStyles.Normal, Color.white);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            Stretch(text.rectTransform);
            return go.GetComponent<Button>();
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
```

- [ ] **Step 2: User runs the build menu**

User: after Task 8 creates the font, run `Fate Weaver ▸ Build Playtest Scene (uGUI)`, open the scene, press Play.
Expected: cards render with art + Korean name/description, scenario buttons switch, fate actions + resolve/next work. Report console errors or layout problems (anchors/sizes are the likely first fixes).

- [ ] **Step 3: Commit (after user confirms it builds)**

```bash
git add Assets/FateWeaver/Unity/Editor/FateWeaverPlaytestSceneCreator.cs Assets/FateWeaver/Unity/Prefabs Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity
git commit -m "feat(unity): editor builder for uGUI playtest scene + CardView prefab"
```

---

## Task 8: Korean TMP font creator menu

**Files:**
- Create: `Assets/FateWeaver/Unity/Editor/KoreanTmpFontCreator.cs`

> Dynamic TMP font asset creation is version-sensitive. If the script errors, fall back to the manual path documented in Task 9's PLAYTEST.md.

- [ ] **Step 1: Create the editor script**

Create `Assets/FateWeaver/Unity/Editor/KoreanTmpFontCreator.cs`:

```csharp
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    public static class KoreanTmpFontCreator
    {
        private const string FontFolder = "Assets/FateWeaver/Unity/Resources/Fonts";
        private const string FontAssetPath = FontFolder + "/KoreanTMP.asset";
        private const string SourceTtf = "C:/Windows/Fonts/malgun.ttf";

        [MenuItem("Fate Weaver/Create Korean TMP Font")]
        public static void Create()
        {
            Directory.CreateDirectory(FontFolder);

            var sourceFont = new Font(SourceTtf);
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 36,
                atlasPadding: 5,
                renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("Failed to create TMP font asset from " + SourceTtf
                    + ". Use the manual Font Asset Creator fallback (see PLAYTEST.md).");
                return;
            }

            fontAsset.name = "KoreanTMP";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            fontAsset.material.name = "KoreanTMP Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath);
            Debug.Log("Created Korean TMP font asset at " + FontAssetPath);
        }
    }
}
```

- [ ] **Step 2: User imports TMP Essentials, then runs the menu**

User: ① `Window ▸ TextMeshPro ▸ Import TMP Essential Resources` (one-time). ② `Fate Weaver ▸ Create Korean TMP Font`.
Expected: `KoreanTMP.asset` appears under `Resources/Fonts/`, console logs success. If it errors, report — we switch to the manual Font Asset Creator (Dynamic) fallback and save to the same path.

- [ ] **Step 3: Commit (after user confirms creation)**

```bash
git add Assets/FateWeaver/Unity/Editor/KoreanTmpFontCreator.cs Assets/FateWeaver/Unity/Resources/Fonts
git commit -m "feat(unity): editor menu to create dynamic Korean TMP font"
```

---

## Task 9: Remove `RuntimeOsFontLoader`; update PLAYTEST.md

**Files:**
- Delete: `Assets/FateWeaver/Unity/RuntimeOsFontLoader.cs` (+ `.meta`)
- Delete: `Assets/FateWeaver/Tests/UnityEditMode/RuntimeOsFontLoaderTests.cs` (+ `.meta`)
- Modify: `Assets/FateWeaver/Unity/PLAYTEST.md`

- [ ] **Step 1: Delete the IMGUI font loader and its test**

```bash
git rm Assets/FateWeaver/Unity/RuntimeOsFontLoader.cs Assets/FateWeaver/Unity/RuntimeOsFontLoader.cs.meta
git rm Assets/FateWeaver/Tests/UnityEditMode/RuntimeOsFontLoaderTests.cs Assets/FateWeaver/Tests/UnityEditMode/RuntimeOsFontLoaderTests.cs.meta
```

- [ ] **Step 2: Replace PLAYTEST.md setup section**

Replace `Assets/FateWeaver/Unity/PLAYTEST.md` with:

```markdown
# Fate Weaver Unity Playtest (uGUI)

## 최초 1회 세팅

1. `Window ▸ TextMeshPro ▸ Import TMP Essential Resources`.
2. `Fate Weaver ▸ Create Korean TMP Font` — `Resources/Fonts/KoreanTMP.asset` 생성.
   - 실패 시 수동 대체: `Window ▸ TextMeshPro ▸ Font Asset Creator`에서 `C:/Windows/Fonts/malgun.ttf`를
     Source로, Atlas Population Mode = **Dynamic**으로 생성해 `Assets/FateWeaver/Unity/Resources/Fonts/KoreanTMP.asset`로 저장.
3. `Fate Weaver ▸ Build Playtest Scene (uGUI)` — Canvas/CardView 프리팹/컨트롤러를 생성·연결.

## 실행

1. `Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity`를 열고 Play.
2. 상단 버튼으로 시나리오 선택.
3. 미래 영역의 카드(이미지 + 이름/주도력 + 하단 설명)를 눌러 주/보조 대상 선택.
4. 운명 액션(주도력 ±2 / 교환 / 고정) 적용.
5. `턴 실행` → `다음 턴`으로 진행(HP·상태 이월). 승패가 나거나 마지막 턴이면 종료.

## 범위 / 검증

- 카드 위젯은 `CardView`(프리팹) + `CardPresentation`(뷰모델) + `PlaytestCardArt`/`PlaytestKoreanText`(룩업).
- 멀티턴 진행 로직은 `MultiTurnPlaytestSession`(순수 C#)이며 헤드리스 테스트로 검증된다.
- 컨트롤러/프리팹/에디터 빌더는 헤드리스 컴파일 대상이 아니므로 Unity Play에서만 검증된다.
- `PlaytestCardArt.ResolveArtName` / `PlaytestKoreanText.CardDescription`는 `FateWeaver.Tests.UnityEditMode`
  EditMode 테스트로 가드된다(Unity Test Runner에서 실행).
```

- [ ] **Step 3: User verifies compile + EditMode suite**

User: Unity reloads (no `RuntimeOsFontLoader` references remain — Task 6 already removed the controller's use). Run the full `FateWeaver.Tests.UnityEditMode` suite. Expected: compiles; `PlaytestKoreanTextTests`, `CardDescriptionTests`, `PlaytestCardArtTests` all pass.

- [ ] **Step 4: Commit**

```bash
git add Assets/FateWeaver/Unity/PLAYTEST.md
git commit -m "chore(unity): drop IMGUI font loader; document uGUI playtest setup"
```

---

## Final verification (user, in Unity)

- [ ] TMP Essentials imported; `Create Korean TMP Font` produced `KoreanTMP.asset`.
- [ ] `Build Playtest Scene (uGUI)` produced the scene + `CardView.prefab` with no console errors.
- [ ] Play: each card shows its art (player + enemy), Korean name/initiative, and a wrapped description block; `prep` shows the side-tinted fallback.
- [ ] Scenario buttons switch scenarios; selecting a card outlines it; fate actions change order; `턴 실행` resolves; `다음 턴` carries HP/status; win/lose stops flow.
- [ ] `FateWeaver.Tests.UnityEditMode` suite green.
