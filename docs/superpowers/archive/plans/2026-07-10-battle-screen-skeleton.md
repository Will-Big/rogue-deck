# 전투 화면 골격 (시각 개편 1단계) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** [스펙](../../specs/2026-07-10-battle-scene-visual-design.md) §9 1단계 — 새 전투 씬에 구도 전체(유닛 무대 + 유닛별 HP 바, 스크롤 실행 레일 + 미니 카드 + 호버 프리뷰, 곡선 손패, 덱 버튼 3종, 운명력, 턴 버튼)와 선택 모드 UX(레일 제외 전체 딤 + 좌측 실행 취소 버튼, 스펙 §6)를 구현한다. 정지 이미지(플레이스홀더 초상), 기존 클릭 입력 임시 유지.

**Architecture:** 게임 로직(`DeckCombatSession` 등 순수 C#)은 동작 변경 없이 그대로 소비하고, 새 UI 컴포넌트(`UnitView`/`RailCardView`/`ExecutionRailView`/`HandFanView`/`PileView`)와 새 컨트롤러(`BattleScreenController`)를 만든다. 씬은 손으로 만들지 않고 에디터 메뉴(`BattleSceneBuilder`)가 코드로 생성·저장한다. UI 위젯의 자식 계층도 프리팹 없이 코드로 조립한다(기존 `CardView.prefab`만 재사용).

**Tech Stack:** Unity 6 uGUI + TextMeshPro, 순수 C# 레이아웃 수학(헤드리스 NUnit), Input System UI 모듈.

## Global Constraints

- `Assets/Core/**`, `Assets/Core/Simulation/**`는 **UnityEngine 참조 금지** (헤드리스 csproj가 같은 소스를 컴파일).
- C# 언어 수준은 **LangVersion 9** (Unity 6 컴파일러 프록시). record struct, file-scoped namespace 등 C# 10+ 금지.
- 헤드리스 테스트 위치는 `Assets/Core/Tests/EditMode/`, 네임스페이스는 `FateWeaver.Tests`, 실행 명령은 `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`.
- 카드 이름/설명은 반드시 `PlaytestKoreanText` / `DescriptionComposer` 경유 (UI에 하드코딩 금지). `CardPresentation.From`/`FromDefinition`이 이미 처리하므로 이를 재사용.
- 기존 로직 동작 변경 금지 — `Deck`/`DeckCombatSession`에는 **읽기 전용 프로퍼티 추가만** 허용.
- 새 프리팹 제작 금지: 위젯 계층은 코드로 조립, 카드 위젯은 기존 `Assets/Unity/Prefabs/CardView.prefab` 재사용.
- 커밋 메시지 prefix 관례: `feat(...)` / `test(...)` / `chore(...)` / `docs(...)`.
- Unity 쪽 코드(`Assets/Unity/**`)는 헤드리스로 컴파일 검증이 안 된다. 컴파일 확인은 Unity 에디터(콘솔 에러 0) 또는 에디터가 닫혀 있을 때 `"/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath . -logFile -` (로그에 `Scripts have compiler errors` 없으면 통과).
- `.meta` 파일은 Unity가 다음 에디터 오픈 시 생성한다 — 마지막 Task에서 일괄 커밋한다.

---

### Task 1: HandFanLayout — 곡선 손패 수학 (순수 C#, TDD)

**Files:**
- Create: `Assets/Core/Simulation/Presentation/HandFanLayout.cs`
- Test: `Assets/Core/Tests/EditMode/HandFanLayoutTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `FateWeaver.Simulation.Presentation.HandFanLayout.PoseFor(int index, int count, float spacing, float anglePerCard, float arcDrop)` → `FanPose { float XOffset; float YOffset; float AngleDegrees; }`. Task 7의 `HandFanView`가 호출한다. 부호 규약: 왼쪽 카드 XOffset<0 · YOffset<0(가장자리가 가라앉음) · AngleDegrees>0(Unity Z+ = 반시계, 왼쪽 카드가 바깥으로 기움).

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Core/Tests/EditMode/HandFanLayoutTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Simulation.Presentation;

namespace FateWeaver.Tests
{
    public class HandFanLayoutTests
    {
        [Test]
        public void Single_card_sits_at_center_with_no_tilt()
        {
            var pose = HandFanLayout.PoseFor(0, 1, 170f, 4f, 12f);

            Assert.AreEqual(0f, pose.XOffset);
            Assert.AreEqual(0f, pose.YOffset);
            Assert.AreEqual(0f, pose.AngleDegrees);
        }

        [Test]
        public void Middle_card_of_odd_hand_is_centered()
        {
            var pose = HandFanLayout.PoseFor(2, 5, 170f, 4f, 12f);

            Assert.AreEqual(0f, pose.XOffset);
            Assert.AreEqual(0f, pose.YOffset);
            Assert.AreEqual(0f, pose.AngleDegrees);
        }

        [Test]
        public void Fan_is_symmetric_around_center()
        {
            var left = HandFanLayout.PoseFor(0, 5, 170f, 4f, 12f);
            var right = HandFanLayout.PoseFor(4, 5, 170f, 4f, 12f);

            Assert.AreEqual(-right.XOffset, left.XOffset, 1e-4f);
            Assert.AreEqual(right.YOffset, left.YOffset, 1e-4f);
            Assert.AreEqual(-right.AngleDegrees, left.AngleDegrees, 1e-4f);
        }

        [Test]
        public void Left_card_sits_left_sinks_and_tilts_counterclockwise()
        {
            var left = HandFanLayout.PoseFor(0, 5, 170f, 4f, 12f);

            Assert.Less(left.XOffset, 0f);
            Assert.Less(left.YOffset, 0f);
            Assert.Greater(left.AngleDegrees, 0f);
        }

        [Test]
        public void Even_hand_straddles_the_center()
        {
            var a = HandFanLayout.PoseFor(1, 4, 170f, 4f, 12f);
            var b = HandFanLayout.PoseFor(2, 4, 170f, 4f, 12f);

            Assert.AreEqual(-85f, a.XOffset, 1e-4f);
            Assert.AreEqual(85f, b.XOffset, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter HandFanLayoutTests`
Expected: 컴파일 에러 — `HandFanLayout`/`FanPose` 미정의 (CS0246).

- [ ] **Step 3: 최소 구현**

`Assets/Core/Simulation/Presentation/HandFanLayout.cs`:

```csharp
namespace FateWeaver.Simulation.Presentation
{
    /// <summary>Pose of one hand card in the fan, in abstract units relative to the fan center.
    /// Views multiply offsets into pixels and apply AngleDegrees as a Z rotation.</summary>
    public readonly struct FanPose
    {
        public float XOffset { get; }
        public float YOffset { get; }
        public float AngleDegrees { get; }

        public FanPose(float xOffset, float yOffset, float angleDegrees)
        {
            XOffset = xOffset;
            YOffset = yOffset;
            AngleDegrees = angleDegrees;
        }
    }

    /// <summary>Curved-fan hand layout. Pure C# (no UnityEngine) so it stays headless-testable.</summary>
    public static class HandFanLayout
    {
        /// <summary>spacing = X per slot, anglePerCard = degrees per slot (left card tilts CCW = positive),
        /// arcDrop = how far edge cards sink per squared slot distance.</summary>
        public static FanPose PoseFor(int index, int count, float spacing, float anglePerCard, float arcDrop)
        {
            float t = index - (count - 1) * 0.5f;
            return new FanPose(t * spacing, -arcDrop * t * t, -t * anglePerCard);
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter HandFanLayoutTests`
Expected: `Passed!` — 5 tests passed.

- [ ] **Step 5: 전체 헤드리스 테스트 회귀 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`
Expected: 전부 통과 (기존 테스트 포함, Failed 0).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Simulation/Presentation/HandFanLayout.cs Assets/Core/Tests/EditMode/HandFanLayoutTests.cs
git commit -m "feat(ui-math): curved hand-fan layout math with headless tests"
```

---

### Task 2: 덱 더미 노출 — Deck/Session 읽기 전용 API (TDD)

**Files:**
- Modify: `Assets/Core/Combat/Deck.cs` (`public int HandCount => _hand.Count;` 아래에 프로퍼티 추가)
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs` (필드/생성자/프로퍼티 추가)
- Test: `Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs`

**Interfaces:**
- Consumes: `Deck._draw`/`_discard` (기존 private 리스트), `DeckCombatSession` 생성자의 `deckCards`
- Produces: `Deck.DrawPile`/`Deck.DiscardPile` (`IReadOnlyList<CardDefinition>`), `DeckCombatSession.DrawPile`/`DiscardPile`/`AllDeckCards` (`IReadOnlyList<CardDefinition>`). Task 9의 덱 팝업이 사용한다. **표시 순서 규약: DrawPile의 실제 순서는 스포일러이므로 UI(Task 9)가 이름순 정렬해 보여준다 — 코어는 정렬하지 않는다.**

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class DeckPileVisibilityTests
    {
        private static DeckCombatSession NewSession()
        {
            var deck = StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition).ToList();
            return new DeckCombatSession(
                deck, 30, new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) },
                GoblinDeck.Policy(1), 3, 5, 1);
        }

        private static int IndexOfAffordableExecution(DeckCombatSession session)
        {
            for (int i = 0; i < session.Hand.Count; i++)
            {
                var def = session.Hand[i];
                if (def.Category == CardCategory.Execution && def.EnergyCost <= session.FateEnergy)
                {
                    return i;
                }
            }

            Assert.Fail("opening hand has no affordable execution card (seed drift?)");
            return -1;
        }

        [Test]
        public void All_deck_cards_survive_construction()
        {
            Assert.AreEqual(StarterDeckSpecs.Build().Count, NewSession().AllDeckCards.Count);
        }

        [Test]
        public void Piles_and_hand_partition_the_deck()
        {
            var session = NewSession();

            Assert.AreEqual(
                session.AllDeckCards.Count,
                session.DrawPile.Count + session.DiscardPile.Count + session.Hand.Count);
        }

        [Test]
        public void Played_execution_card_lands_in_the_discard_pile()
        {
            var session = NewSession();
            int index = IndexOfAffordableExecution(session);
            var id = session.Hand[index].Id;

            Assert.IsTrue(session.PlayExecutionCard(index));
            Assert.IsTrue(session.DiscardPile.Any(c => c.Id == id));
        }

        [Test]
        public void Next_turn_discards_the_leftover_hand()
        {
            var session = NewSession();
            int handBefore = session.Hand.Count;
            session.ResolveTurn();

            Assert.IsTrue(session.BeginNextTurn());
            // 이월된 손패는 버림 더미를 거쳤다가 재드로우된다 — 분할 불변식은 유지된다.
            Assert.AreEqual(
                session.AllDeckCards.Count,
                session.DrawPile.Count + session.DiscardPile.Count + session.Hand.Count);
            Assert.GreaterOrEqual(handBefore, 1);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter DeckPileVisibilityTests`
Expected: 컴파일 에러 — `AllDeckCards`/`DrawPile`/`DiscardPile` 미정의 (CS1061).

- [ ] **Step 3: 최소 구현**

`Assets/Core/Combat/Deck.cs` — `public int HandCount => _hand.Count;` 바로 아래에 추가:

```csharp
        /// <summary>Read-only pile views for deck-viewer UI. Draw order is real — UI must sort for display.</summary>
        public IReadOnlyList<CardDefinition> DrawPile => _draw;
        public IReadOnlyList<CardDefinition> DiscardPile => _discard;
```

`Assets/Core/Simulation/DeckCombatSession.cs` — 세 군데 수정:

필드 블록(`private readonly int _handSize;` 아래)에 추가:

```csharp
        private readonly List<CardDefinition> _allCards;
```

생성자에서 `_deck = new Deck(deckCards, seed);` 바로 위에 추가:

```csharp
            _allCards = new List<CardDefinition>(deckCards);
```

프로퍼티 블록(`public int DiscardCount => _deck.DiscardCount;` 아래)에 추가:

```csharp
        /// <summary>Deck-viewer UI: real draw order (UI sorts for display), discard order, and the
        /// full list the player brought into combat (authoring order).</summary>
        public IReadOnlyList<CardDefinition> DrawPile => _deck.DrawPile;
        public IReadOnlyList<CardDefinition> DiscardPile => _deck.DiscardPile;
        public IReadOnlyList<CardDefinition> AllDeckCards => _allCards;
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter DeckPileVisibilityTests`
Expected: `Passed!` — 4 tests passed.

- [ ] **Step 5: 전체 헤드리스 테스트 회귀 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`
Expected: 전부 통과 (Failed 0).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Combat/Deck.cs Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs
git commit -m "feat(core): expose read-only draw/discard/full-deck lists for pile viewers"
```

---

### Task 3: CardPresentation에 Category 추가

레일 미니 카드는 실행/개입을 **프레임 색으로** 구분해야 하는데(스펙 §3) `CardPresentation`에 카테고리가 없다. 추가한다. Unity 쪽 코드라 헤드리스 테스트 불가 — 기존 팩토리 호출부만 갱신하고 컴파일로 검증한다 (`CardPresentation`을 직접 생성하는 외부 호출부는 없음 — `From`/`FromDefinition`만 쓰인다. UnityEditMode 테스트도 팩토리만 사용하므로 수정 불필요).

**Files:**
- Modify: `Assets/Unity/CardPresentation.cs`

**Interfaces:**
- Consumes: `CardDefinition.Category` (`CardCategory.Execution | Intervention`)
- Produces: `CardPresentation.Category` (`CardCategory`). Task 5 `RailCardView.Bind`가 프레임 색 결정에 사용.

- [ ] **Step 1: 프로퍼티/생성자/팩토리 수정**

`Assets/Unity/CardPresentation.cs`에서:

(a) 프로퍼티 블록의 `public bool IsLocked { get; }` 아래에 추가:

```csharp
        public CardCategory Category { get; }
```

(b) 생성자 시그니처의 마지막 파라미터를 다음으로 교체(트레일링 옵션 추가):

```csharp
        public CardPresentation(
            string id, string displayName, int executionOrder, int energyCost, Side side,
            string description, Sprite art, bool isLocked,
            IReadOnlyList<CardStatusIcon> statusIcons = null,
            CardCategory category = CardCategory.Execution)
```

생성자 본문 끝에 추가:

```csharp
            Category = category;
```

(c) `From(ExecutionCardInstance ...)`의 return 마지막 인자 `StatusIconsFor(card)` 뒤에 `, def.Category` 추가.
(d) `FromDefinition(CardDefinition ...)`의 return 마지막 인자 `Array.Empty<CardStatusIcon>()` 뒤에 `, def.Category` 추가.

- [ ] **Step 2: 컴파일 확인**

Unity 에디터 포커스 후 Console 에러 0 확인 (에디터가 닫혀 있으면 Global Constraints의 batchmode 명령 사용).
Expected: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/CardPresentation.cs
git commit -m "feat(ui): expose card category on CardPresentation for rail frames"
```

---

### Task 4: BattleUiKit + UnitView — 코드 조립 UI 기반과 유닛 위젯

**Files:**
- Create: `Assets/Unity/BattleUiKit.cs`
- Create: `Assets/Unity/UnitView.cs`

**Interfaces:**
- Consumes: `Resources/Fonts/KoreanTMP` (PLAYTEST.md가 핀하는 폰트, 없으면 TMP 기본 폰트 폴백)
- Produces:
  - `BattleUiKit.KoreanFont()`, `Rect(parent,name)`, `Image(parent,name,color)`, `Text(parent,name,fontSize,align)`, `Stretch(rect)` — Task 5/8/10이 사용. **public** (에디터 어셈블리 `BattleSceneBuilder`도 호출).
  - `UnitView.Create(RectTransform parent, Vector2 size)` → `UnitView`; `Bind(string displayName, Color portraitTint)`; `SetHp(int current, int max)` — Task 9가 사용.

- [ ] **Step 1: BattleUiKit 작성**

`Assets/Unity/BattleUiKit.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Factory for the battle screen's code-built uGUI nodes (no prefab authoring).
    /// Public so the editor-assembly scene builder can reuse it. The Korean TMP font is the same
    /// Resources asset PLAYTEST.md pins; a missing font falls back to the TMP default.</summary>
    public static class BattleUiKit
    {
        private static TMP_FontAsset _koreanFont;
        private static bool _fontLookedUp;

        public static TMP_FontAsset KoreanFont()
        {
            if (!_fontLookedUp)
            {
                _koreanFont = Resources.Load<TMP_FontAsset>("Fonts/KoreanTMP");
                _fontLookedUp = true;
            }

            return _koreanFont;
        }

        public static RectTransform Rect(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image Image(RectTransform parent, string name, Color color)
        {
            var rect = Rect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TMP_Text Text(RectTransform parent, string name, float fontSize, TextAlignmentOptions align)
        {
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var font = KoreanFont();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = fontSize;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
```

- [ ] **Step 2: UnitView 작성**

`Assets/Unity/UnitView.cs`:

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>One combatant on the stage: placeholder portrait + a per-unit HP bar anchored below it.
    /// Both sides can field several units, so HP never lives in a shared top HUD (spec §2).
    /// Portrait art/sprites land in later phases.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private RectTransform _hpFill;
        [SerializeField] private TMP_Text _hpText;
        [SerializeField] private TMP_Text _nameText;

        private static readonly Color HpColor = new Color(0.35f, 0.75f, 0.5f, 1f);
        private static readonly Color DeadTint = new Color(0.35f, 0.35f, 0.35f, 0.5f);

        private Color _aliveTint = Color.white;

        public void Bind(string displayName, Color portraitTint)
        {
            _aliveTint = portraitTint;
            _portrait.color = portraitTint;
            _nameText.text = displayName;
        }

        public void SetHp(int current, int max)
        {
            float t = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            _hpFill.anchorMin = new Vector2(0f, 0f);
            _hpFill.anchorMax = new Vector2(t, 1f);
            _hpFill.offsetMin = Vector2.zero;
            _hpFill.offsetMax = Vector2.zero;
            _hpText.text = Mathf.Max(0, current) + " / " + max;
            _portrait.color = current > 0 ? _aliveTint : DeadTint;
        }

        /// <summary>Builds the whole child hierarchy in code — no prefab to author.</summary>
        public static UnitView Create(RectTransform parent, Vector2 size)
        {
            var root = BattleUiKit.Rect(parent, "Unit");
            root.sizeDelta = size;
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = size.x;
            layout.preferredHeight = size.y;

            var view = root.gameObject.AddComponent<UnitView>();

            var portrait = BattleUiKit.Image(root, "Portrait", Color.white);
            BattleUiKit.Anchor(portrait.rectTransform, 0f, 0.28f, 1f, 1f);
            portrait.raycastTarget = false;

            var hpBack = BattleUiKit.Image(root, "HpBack", new Color(0f, 0f, 0f, 0.55f));
            BattleUiKit.Anchor(hpBack.rectTransform, 0.05f, 0.16f, 0.95f, 0.26f);
            hpBack.raycastTarget = false;

            var hpFill = BattleUiKit.Image(hpBack.rectTransform, "HpFill", HpColor);
            BattleUiKit.Stretch(hpFill.rectTransform);
            hpFill.raycastTarget = false;

            var hpText = BattleUiKit.Text(hpBack.rectTransform, "HpText", 14f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(hpText.rectTransform);

            var nameText = BattleUiKit.Text(root, "Name", 16f, TextAlignmentOptions.Center);
            BattleUiKit.Anchor(nameText.rectTransform, 0f, 0f, 1f, 0.14f);

            view._portrait = portrait;
            view._hpFill = hpFill.rectTransform;
            view._hpText = hpText;
            view._nameText = nameText;
            return view;
        }
    }
}
```

- [ ] **Step 3: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Unity/BattleUiKit.cs Assets/Unity/UnitView.cs
git commit -m "feat(ui): code-built UI kit and per-unit stage view with HP bar"
```

---

### Task 5: RailCardView — 실행 레일 미니 카드

**Files:**
- Create: `Assets/Unity/RailCardView.cs`

**Interfaces:**
- Consumes: `CardPresentation` (Task 3의 `Category` 포함), `BattleUiKit`, `PlaytestCardArt.StatusIconSprite(CardStatusIcon.Lock)`, `CardView.SelectionKind`
- Produces: `RailCardView.Create(RectTransform parent, Vector2 size)` → `RailCardView`; `Bind(CardPresentation data, Action onClick, Action<bool> onHover)`; `SetSelection(CardView.SelectionKind kind)` — Task 6이 사용.

- [ ] **Step 1: RailCardView 작성**

`Assets/Unity/RailCardView.cs`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Compact execution-rail card: category frame + art + top-center execution-order badge.
    /// No rules text — the rail is too small for it (spec §3); hovering raises a callback so the rail
    /// shows the full CardView preview instead.</summary>
    public sealed class RailCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _frame;
        [SerializeField] private Image _art;
        [SerializeField] private Image _artFallback;
        [SerializeField] private TMP_Text _orderText;
        [SerializeField] private Image _selectionOutline;
        [SerializeField] private Image _lockIcon;
        [SerializeField] private Button _button;

        private static readonly Color ExecutionFrame = new Color(0.55f, 0.42f, 0.22f, 1f);
        private static readonly Color InterventionFrame = new Color(0.24f, 0.45f, 0.55f, 1f);
        private static readonly Color EnemyTint = new Color(0.45f, 0.18f, 0.18f, 1f);
        private static readonly Color PlayerTint = new Color(0.22f, 0.28f, 0.36f, 1f);
        private static readonly Color OutlineNone = new Color(0f, 0f, 0f, 0f);
        private static readonly Color OutlinePrimary = new Color(0.95f, 0.72f, 0.25f, 1f);
        private static readonly Color OutlineSecondary = new Color(0.35f, 0.75f, 0.95f, 1f);

        private Action<bool> _onHover;

        public void Bind(CardPresentation data, Action onClick, Action<bool> onHover)
        {
            _onHover = onHover;
            _frame.color = data.Category == CardCategory.Intervention ? InterventionFrame : ExecutionFrame;
            _orderText.text = data.ExecutionOrder.ToString();

            if (data.Art != null)
            {
                _art.enabled = true;
                _art.sprite = data.Art;
                _art.preserveAspect = true;
                _artFallback.enabled = false;
            }
            else
            {
                _art.enabled = false;
                _artFallback.enabled = true;
                _artFallback.color = data.Side == Side.Enemy ? EnemyTint : PlayerTint;
            }

            _lockIcon.gameObject.SetActive(data.IsLocked);
            _button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                _button.onClick.AddListener(() => onClick());
            }

            SetSelection(CardView.SelectionKind.None);
        }

        public void SetSelection(CardView.SelectionKind kind)
        {
            _selectionOutline.color =
                kind == CardView.SelectionKind.Primary ? OutlinePrimary :
                kind == CardView.SelectionKind.Secondary ? OutlineSecondary :
                OutlineNone;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onHover?.Invoke(true);

        public void OnPointerExit(PointerEventData eventData) => _onHover?.Invoke(false);

        public static RailCardView Create(RectTransform parent, Vector2 size)
        {
            var root = BattleUiKit.Rect(parent, "RailCard");
            root.sizeDelta = size;

            var view = root.gameObject.AddComponent<RailCardView>();

            var selection = BattleUiKit.Image(root, "Selection", OutlineNone);
            var selectionRect = selection.rectTransform;
            BattleUiKit.Stretch(selectionRect);
            selectionRect.offsetMin = new Vector2(-4f, -4f);
            selectionRect.offsetMax = new Vector2(4f, 4f);
            selection.raycastTarget = false;

            var frame = BattleUiKit.Image(root, "Frame", ExecutionFrame);
            BattleUiKit.Stretch(frame.rectTransform);

            var artFallback = BattleUiKit.Image(root, "ArtFallback", PlayerTint);
            BattleUiKit.Stretch(artFallback.rectTransform);
            artFallback.rectTransform.offsetMin = new Vector2(5f, 5f);
            artFallback.rectTransform.offsetMax = new Vector2(-5f, -5f);
            artFallback.raycastTarget = false;

            var art = BattleUiKit.Image(root, "Art", Color.white);
            BattleUiKit.Stretch(art.rectTransform);
            art.rectTransform.offsetMin = new Vector2(5f, 5f);
            art.rectTransform.offsetMax = new Vector2(-5f, -5f);
            art.raycastTarget = false;

            var badge = BattleUiKit.Image(root, "OrderBadge", new Color(0.12f, 0.12f, 0.16f, 0.92f));
            var badgeRect = badge.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, 2f);
            badgeRect.sizeDelta = new Vector2(32f, 24f);
            badge.raycastTarget = false;

            var orderText = BattleUiKit.Text(badgeRect, "Order", 16f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(orderText.rectTransform);

            var lockIcon = BattleUiKit.Image(root, "LockIcon", Color.white);
            var lockRect = lockIcon.rectTransform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(0f, 1f);
            lockRect.anchoredPosition = new Vector2(14f, -14f);
            lockRect.sizeDelta = new Vector2(20f, 20f);
            lockIcon.sprite = PlaytestCardArt.StatusIconSprite(CardStatusIcon.Lock);
            lockIcon.preserveAspect = true;
            lockIcon.raycastTarget = false;
            lockIcon.gameObject.SetActive(false);

            // Click/hover land on the frame graphic; the handlers live on this root (uGUI bubbles up).
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;

            view._frame = frame;
            view._art = art;
            view._artFallback = artFallback;
            view._orderText = orderText;
            view._selectionOutline = selection;
            view._lockIcon = lockIcon;
            view._button = button;
            return view;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음. (`PlaytestCardArt.StatusIconSprite`가 없다고 나오면 시그니처를 `Assets/Unity/PlaytestCardArt.cs`에서 확인 — `CardView.ConfigureStatusIcon`이 쓰는 것과 동일한 메서드다.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/RailCardView.cs
git commit -m "feat(ui): compact rail card with category frame and order badge"
```

---

### Task 6: ExecutionRailView — 스크롤 레일 + 호버 프리뷰

**Files:**
- Create: `Assets/Unity/ExecutionRailView.cs`

**Interfaces:**
- Consumes: `RailCardView` (Task 5), `CardView` 프리팹(프리뷰용), `BattleUiKit`
- Produces:
  - `EditorBuild(CardView previewPrefab, RectTransform previewLayer)` — Task 10 빌더가 에디터 타임에 호출(ScrollRect 계층 구축 + 직렬화 참조 배선)
  - `SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick)`, `SetSelection(int index, CardView.SelectionKind kind)` — Task 9가 사용

- [ ] **Step 1: ExecutionRailView 작성**

`Assets/Unity/ExecutionRailView.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>The execution rail: a horizontally scrollable strip of RailCardViews in resolution order
    /// (spec §2 — the rail can hold many cards). Hovering a card shows the full CardView preview on the
    /// overlay layer since mini cards carry no rules text (spec §3).</summary>
    public sealed class ExecutionRailView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private CardView _previewPrefab;
        [SerializeField] private RectTransform _previewLayer;

        private static readonly Vector2 CardSize = new Vector2(96f, 132f);
        private static readonly Vector2 PreviewSize = new Vector2(200f, 280f);

        private readonly List<RailCardView> _views = new List<RailCardView>();
        private CardView _preview;

        /// <summary>Editor-time construction (called by BattleSceneBuilder); the built children and
        /// references serialize into the scene.</summary>
        public void EditorBuild(CardView previewPrefab, RectTransform previewLayer)
        {
            _previewPrefab = previewPrefab;
            _previewLayer = previewLayer;

            var rect = (RectTransform)transform;
            var scroll = gameObject.AddComponent<ScrollRect>();

            var viewport = BattleUiKit.Rect(rect, "Viewport");
            BattleUiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var backdrop = viewport.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.25f);

            var content = BattleUiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            _content = content;
        }

        public void SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick)
        {
            HidePreview();
            foreach (var view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                var view = RailCardView.Create(_content, CardSize);
                int captured = i;
                var data = cards[i];
                view.Bind(data, () => onClick?.Invoke(captured), hovering => OnHover(view, data, hovering));
                _views.Add(view);
            }
        }

        public void SetSelection(int index, CardView.SelectionKind kind)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetSelection(i == index ? kind : CardView.SelectionKind.None);
            }
        }

        private void OnHover(RailCardView source, CardPresentation data, bool hovering)
        {
            if (!hovering)
            {
                HidePreview();
                return;
            }

            if (_previewPrefab == null || _previewLayer == null)
            {
                return;
            }

            if (_preview == null)
            {
                _preview = Instantiate(_previewPrefab, _previewLayer);
                var previewRect = (RectTransform)_preview.transform;
                previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
                previewRect.sizeDelta = PreviewSize;
                foreach (var graphic in _preview.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }

            _preview.gameObject.SetActive(true);
            _preview.Bind(data, null);

            var screen = RectTransformUtility.WorldToScreenPoint(null, source.transform.position);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewLayer, screen, null, out local);
            local.y += CardSize.y * 0.5f + PreviewSize.y * 0.5f + 14f;
            float maxX = _previewLayer.rect.width * 0.5f - PreviewSize.x * 0.5f - 8f;
            local.x = Mathf.Clamp(local.x, -maxX, maxX);
            ((RectTransform)_preview.transform).anchoredPosition = local;
        }

        private void HidePreview()
        {
            if (_preview != null)
            {
                _preview.gameObject.SetActive(false);
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/ExecutionRailView.cs
git commit -m "feat(ui): scrollable execution rail with hover full-card preview"
```

---

### Task 7: HandFanView — 곡선 손패

**Files:**
- Create: `Assets/Unity/HandFanView.cs`

**Interfaces:**
- Consumes: `HandFanLayout.PoseFor` (Task 1), `CardView` 프리팹, `CardPresentation`
- Produces: `EditorBuild(CardView cardPrefab)` (Task 10 배선용); `SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick)`; `SetSelection(int index, CardView.SelectionKind kind)` — Task 9가 사용

- [ ] **Step 1: HandFanView 작성**

`Assets/Unity/HandFanView.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Presentation;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>The hand as a slight curved fan (spec §2): full CardViews positioned by HandFanLayout,
    /// no layout group — poses are absolute so cards can tilt.</summary>
    public sealed class HandFanView : MonoBehaviour
    {
        [SerializeField] private CardView _cardPrefab;

        private const float Spacing = 150f;
        private const float AnglePerCard = 4f;
        private const float ArcDrop = 10f;
        private static readonly Vector2 CardSize = new Vector2(170f, 238f);

        private readonly List<CardView> _views = new List<CardView>();

        public void EditorBuild(CardView cardPrefab)
        {
            _cardPrefab = cardPrefab;
        }

        public void SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick)
        {
            foreach (var view in _views)
            {
                Destroy(view.gameObject);
            }

            _views.Clear();
            var root = (RectTransform)transform;
            for (int i = 0; i < cards.Count; i++)
            {
                var view = Instantiate(_cardPrefab, root);
                var rect = (RectTransform)view.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = CardSize;
                var pose = HandFanLayout.PoseFor(i, cards.Count, Spacing, AnglePerCard, ArcDrop);
                rect.anchoredPosition = new Vector2(pose.XOffset, pose.YOffset);
                rect.localRotation = Quaternion.Euler(0f, 0f, pose.AngleDegrees);
                int captured = i;
                view.Bind(cards[i], () => onClick?.Invoke(captured));
                _views.Add(view);
            }
        }

        public void SetSelection(int index, CardView.SelectionKind kind)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i].SetSelection(i == index ? kind : CardView.SelectionKind.None);
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/HandFanView.cs
git commit -m "feat(ui): curved hand fan view driven by pure layout math"
```

---

### Task 8: PileView — 덱 버튼 3종 공용 위젯 + 카드 목록 팝업

**Files:**
- Create: `Assets/Unity/PileView.cs`

**Interfaces:**
- Consumes: `BattleUiKit`, `CardView` 프리팹, `CardPresentation`
- Produces: `PileView.Create(RectTransform parent, RectTransform popupLayer, string title, CardView cardPrefab, Vector2 buttonSize)` → `PileView` (Task 10 빌더가 에디터 타임 호출); `Bind(Func<IReadOnlyList<CardPresentation>> cards)`; `SetCount(int count)` — Task 9가 사용

- [ ] **Step 1: PileView 작성**

`Assets/Unity/PileView.cs`:

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>A deck-pile button (draw / discard / full deck — spec §2) that opens a scrollable
    /// popup listing the pile's cards as full CardViews. Contents come from a provider delegate so
    /// the popup always reflects the current session state.</summary>
    public sealed class PileView : MonoBehaviour
    {
        [SerializeField] private string _title;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private GameObject _popup;
        [SerializeField] private RectTransform _popupContent;
        [SerializeField] private CardView _cardPrefab;
        [SerializeField] private Button _button;
        [SerializeField] private Button _closeButton;

        private Func<IReadOnlyList<CardPresentation>> _cards;
        private readonly List<CardView> _spawned = new List<CardView>();

        private void Awake()
        {
            _button.onClick.AddListener(Open);
            _closeButton.onClick.AddListener(Close);
        }

        public void Bind(Func<IReadOnlyList<CardPresentation>> cards)
        {
            _cards = cards;
        }

        public void SetCount(int count)
        {
            _labelText.text = _title + "\n" + count;
        }

        private void Open()
        {
            if (_cards == null)
            {
                return;
            }

            Clear();
            foreach (var data in _cards())
            {
                var view = Instantiate(_cardPrefab, _popupContent);
                view.Bind(data, null);
                _spawned.Add(view);
            }

            _popup.SetActive(true);
        }

        public void Close()
        {
            Clear();
            _popup.SetActive(false);
        }

        private void Clear()
        {
            foreach (var view in _spawned)
            {
                Destroy(view.gameObject);
            }

            _spawned.Clear();
        }

        /// <summary>Editor-time construction: the pile button under <paramref name="parent"/> and its
        /// popup under <paramref name="popupLayer"/> (a full-screen overlay above everything).</summary>
        public static PileView Create(
            RectTransform parent, RectTransform popupLayer, string title, CardView cardPrefab, Vector2 buttonSize)
        {
            var root = BattleUiKit.Rect(parent, "Pile_" + title);
            root.sizeDelta = buttonSize;

            var view = root.gameObject.AddComponent<PileView>();

            var background = BattleUiKit.Image(root, "Background", new Color(0.16f, 0.2f, 0.3f, 0.92f));
            BattleUiKit.Stretch(background.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            var label = BattleUiKit.Text(root, "Label", 16f, TextAlignmentOptions.Center);
            BattleUiKit.Stretch(label.rectTransform);
            label.text = title;

            var popup = BattleUiKit.Rect(popupLayer, "Popup_" + title);
            BattleUiKit.Stretch(popup);

            var dim = BattleUiKit.Image(popup, "Dim", new Color(0f, 0f, 0f, 0.75f));
            BattleUiKit.Stretch(dim.rectTransform);
            var closeButton = dim.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = dim;

            var titleText = BattleUiKit.Text(popup, "Title", 28f, TextAlignmentOptions.Center);
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(400f, 40f);
            titleText.text = title;

            var scrollArea = BattleUiKit.Rect(popup, "Scroll");
            BattleUiKit.Anchor(scrollArea, 0.08f, 0.08f, 0.92f, 0.88f);
            var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();

            var viewport = BattleUiKit.Rect(scrollArea, "Viewport");
            BattleUiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            var content = BattleUiKit.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170f, 238f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            popup.gameObject.SetActive(false);

            view._title = title;
            view._labelText = label;
            view._popup = popup.gameObject;
            view._popupContent = content;
            view._cardPrefab = cardPrefab;
            view._button = button;
            view._closeButton = closeButton;
            return view;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/PileView.cs
git commit -m "feat(ui): deck pile button with scrollable card-list popup"
```

---

### Task 9: BattleScreenController — 세션 배선 + 클릭 입력 이식

**Files:**
- Create: `Assets/Unity/BattleScreenController.cs`

**Interfaces:**
- Consumes: `DeckCombatSession` (Task 2의 `DrawPile`/`DiscardPile`/`AllDeckCards` 포함), `HandFanView`/`ExecutionRailView`/`UnitView`/`PileView` (Task 4–8), `GoblinDeck`, `DeckAsset`/`CardAsset`, `PlaytestKoreanText`
- Produces: `BattleScreenController` — 다음 [SerializeField] 필드명을 갖는다 (Task 10 빌더가 `SerializedObject.FindProperty`로 이름 일치 배선): `_deck`, `_enemyArtCards`, `_hand`, `_rail`, `_playerUnitsRow`, `_enemyUnitsRow`, `_drawPile`, `_discardPile`, `_fullDeck`, `_energyText`, `_messageText`, `_turnButton`, `_turnButtonLabel`, `_resetButton`, `_cancelButton`, `_dimLayer`

선택 모드(스펙 §6): 개입 카드가 무장된 동안(`_armedInterventionHandIndex >= 0`) `_dimLayer`를 켠다. 딤 레이어는 레일 아래·나머지 UI 위에 깔려(씬 계층은 Task 10이 보장) 레일 카드만 클릭 가능하게 만들고, 그 안의 실행 취소 버튼이 유일한 탈출구다.

클릭 입력 로직은 `DeckPlaytestController`(Assets/Unity/DeckPlaytestController.cs:95-148)의 armed-intervention 흐름을 그대로 이식한다. 기존 컨트롤러/씬은 삭제하지 않는다(디버그용 유지, 스펙 §7).

- [ ] **Step 1: BattleScreenController 작성**

`Assets/Unity/BattleScreenController.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Intervention;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Battle screen over DeckCombatSession (visual revamp phase 1): stage units with per-unit
    /// HP bars, the scrollable execution rail, a curved hand fan, three pile viewers, and a single
    /// resolve/next turn button. Input is still the 2-step click flow (drag arrives in phase 2), but the
    /// selection-mode UX is final: while an intervention card is armed, everything except the rail dims
    /// and the left-side cancel button is the only way out. UI only — logic stays in the session.</summary>
    public sealed class BattleScreenController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private DeckAsset _deck;
        [Tooltip("Enemy cards' art source (rules live in the goblin deck).")]
        [SerializeField] private CardAsset[] _enemyArtCards = Array.Empty<CardAsset>();

        [Header("Views")]
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
        [SerializeField] private RectTransform _playerUnitsRow;
        [SerializeField] private RectTransform _enemyUnitsRow;
        [SerializeField] private PileView _drawPile;
        [SerializeField] private PileView _discardPile;
        [SerializeField] private PileView _fullDeck;
        [SerializeField] private TMP_Text _energyText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _turnButton;
        [SerializeField] private TMP_Text _turnButtonLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private GameObject _dimLayer;

        private const int PlayerHp = 30;
        private const int FateEnergyPerTurn = 3;
        private const int HandSize = 5;
        private const int Seed = 1;

        private static readonly Color PlayerUnitTint = new Color(0.25f, 0.4f, 0.55f, 1f);
        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);

        private DeckCombatSession _session;
        private int _armedInterventionHandIndex = -1;
        private int _firstSwapZoneIndex = -1;
        private UnitView _playerUnit;
        private readonly List<UnitView> _enemyUnits = new List<UnitView>();
        private readonly List<int> _enemyMaxHp = new List<int>();
        private readonly Dictionary<string, Sprite> _artById = new Dictionary<string, Sprite>();

        private void Start()
        {
            _turnButton.onClick.AddListener(OnTurnButton);
            _resetButton.onClick.AddListener(StartSession);
            _cancelButton.onClick.AddListener(OnCancelSelection);
            StartSession();
        }

        private void StartSession()
        {
            var specs = _deck != null ? _deck.ToSpecs() : StarterDeckSpecs.Build();
            var deckDefs = specs.Select(CardSpecMapper.ToDefinition).ToList();
            var enemies = new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) };
            _session = new DeckCombatSession(
                deckDefs, PlayerHp, enemies, GoblinDeck.Policy(Seed), FateEnergyPerTurn, HandSize, Seed);
            BuildArtLookup();
            SpawnUnits();
            BindPiles();
            ClearArmed();
            SetMessage(_deck != null ? "전투 시작." : "전투 시작 (코드 시작덱 폴백 — DeckAsset 미연결).");
            RefreshAll();
        }

        private void SpawnUnits()
        {
            foreach (Transform child in _playerUnitsRow) Destroy(child.gameObject);
            foreach (Transform child in _enemyUnitsRow) Destroy(child.gameObject);
            _enemyUnits.Clear();
            _enemyMaxHp.Clear();

            _playerUnit = UnitView.Create(_playerUnitsRow, new Vector2(180f, 250f));
            _playerUnit.Bind("플레이어", PlayerUnitTint);

            foreach (var enemy in _session.State.Enemies)
            {
                var view = UnitView.Create(_enemyUnitsRow, new Vector2(200f, 270f));
                view.Bind(PlaytestKoreanText.EnemyName(enemy.Id, enemy.Id), EnemyUnitTint);
                _enemyUnits.Add(view);
                _enemyMaxHp.Add(enemy.Hp);
            }
        }

        private void BindPiles()
        {
            // 뽑을 덱은 실제 순서가 스포일러라 이름순으로 보여준다 (Task 2 규약).
            _drawPile.Bind(() => Presentations(_session.DrawPile)
                .OrderBy(p => p.DisplayName, StringComparer.Ordinal).ToList());
            _discardPile.Bind(() => Presentations(_session.DiscardPile));
            _fullDeck.Bind(() => Presentations(_session.AllDeckCards));
        }

        private IReadOnlyList<CardPresentation> Presentations(IReadOnlyList<CardDefinition> cards)
            => cards.Select(c => CardPresentation.FromDefinition(c, ArtFor)).ToList();

        // --- input (2-step click flow ported from DeckPlaytestController; drag replaces it in phase 2) ---

        private void OnHandClicked(int handIndex)
        {
            if (_session == null) return;
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
            SetMessage(PlaytestKoreanText.CardName(def.Id, def.Name) + " — 레일에서 대상을 선택하세요.");
            RefreshSelections();
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null || _armedInterventionHandIndex < 0) return;

            var def = _session.Hand[_armedInterventionHandIndex];
            var needsTwo = def.InterventionAction != null
                && def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder;

            if (needsTwo && _firstSwapZoneIndex < 0)
            {
                _firstSwapZoneIndex = zoneIndex;
                SetMessage("교환할 두 번째 카드를 선택하세요.");
                RefreshSelections();
                return;
            }

            bool ok = needsTwo
                ? _session.PlayInterventionCard(_armedInterventionHandIndex, _firstSwapZoneIndex, zoneIndex)
                : _session.PlayInterventionCard(_armedInterventionHandIndex, zoneIndex);

            SetMessage(ok ? "개입 카드 적용." : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            ClearArmed();
            RefreshAll();
        }

        private void OnTurnButton()
        {
            if (_session == null || _session.IsComplete) return;

            if (!_session.CurrentTurnResolved)
            {
                _session.ResolveTurn();
                ClearArmed();
                SetMessage(_session.IsComplete
                    ? "전투 결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome)
                    : "턴 해석 완료.");
            }
            else if (_session.BeginNextTurn())
            {
                ClearArmed();
                SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            }

            RefreshAll();
        }

        private void OnCancelSelection()
        {
            SetMessage("실행 취소.");
            ClearArmed();
            RefreshAll();
        }

        private void ClearArmed()
        {
            _armedInterventionHandIndex = -1;
            _firstSwapZoneIndex = -1;
        }

        // --- art lookup (same GUID-backed pattern as DeckPlaytestController) ---

        private void BuildArtLookup()
        {
            _artById.Clear();
            if (_deck != null)
            {
                foreach (var entry in _deck.Entries) AddArt(entry.Card);
            }

            foreach (var card in _enemyArtCards) AddArt(card);
        }

        private void AddArt(CardAsset card)
        {
            if (card != null && !string.IsNullOrEmpty(card.Id) && card.Art != null)
            {
                _artById[card.Id] = card.Art;
            }
        }

        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : PlaytestCardArt.Sprite(id);

        // --- render ---

        private void RefreshAll()
        {
            _hand.SetCards(
                _session.Hand.Select(c => CardPresentation.FromDefinition(c, ArtFor)).ToList(), OnHandClicked);
            _rail.SetCards(
                _session.CurrentOrder.Select(c => CardPresentation.From(c, ArtFor)).ToList(), OnZoneClicked);
            RefreshSelections();
            RefreshUnits();
            RefreshHudTexts();
        }

        private void RefreshSelections()
        {
            // Selection mode (spec §6): dim everything but the rail while an intervention wants targets.
            _dimLayer.SetActive(_armedInterventionHandIndex >= 0);
            _hand.SetSelection(_armedInterventionHandIndex, CardView.SelectionKind.Primary);
            _rail.SetSelection(_firstSwapZoneIndex, CardView.SelectionKind.Secondary);
        }

        private void RefreshUnits()
        {
            _playerUnit.SetHp(_session.State.PlayerHp, PlayerHp);
            for (int i = 0; i < _enemyUnits.Count && i < _session.State.Enemies.Count; i++)
            {
                _enemyUnits[i].SetHp(_session.State.Enemies[i].Hp, _enemyMaxHp[i]);
            }
        }

        private void RefreshHudTexts()
        {
            _energyText.text = "운명력 " + _session.FateEnergy;
            _drawPile.SetCount(_session.DrawCount);
            _discardPile.SetCount(_session.DiscardCount);
            _fullDeck.SetCount(_session.AllDeckCards.Count);
            _turnButtonLabel.text = _session.CurrentTurnResolved ? "다음 턴" : "턴 실행";
            _turnButton.interactable = !_session.IsComplete;
        }

        private void SetMessage(string message)
        {
            if (_messageText != null) _messageText.text = message;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/BattleScreenController.cs
git commit -m "feat(ui): battle screen controller wiring session to the new layout"
```

---

### Task 10: BattleSceneBuilder — 씬 코드 생성 (에디터)

**Files:**
- Create: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Consumes: Task 4–9의 `EditorBuild`/`Create` 및 `BattleScreenController` 필드명; 애셋 경로 `Assets/Unity/Prefabs/CardView.prefab`, `Assets/Unity/CardSO/Player/StarterDeck.asset`, `Assets/Unity/CardSO/Enemies/Goblin/{goblin_jab,goblin_sly_jab,goblin_crude_guard}.asset`, `Assets/Unity/Resources/UIInputActions.inputactions`
- Produces: 메뉴 `Fate Weaver ▸ Build Battle Scene` → `Assets/Scenes/FateWeaverBattle.unity` 저장 (재실행 시 덮어씀)

- [ ] **Step 1: BattleSceneBuilder 작성**

`Assets/Unity/Editor/BattleSceneBuilder.cs`:

```csharp
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
        private const string DeckAssetPath = "Assets/Unity/CardSO/Player/StarterDeck.asset";
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
            var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(CardPrefabPath);
            var deck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckAssetPath);
            if (cardPrefab == null || deck == null)
            {
                Debug.LogError("BattleSceneBuilder: missing CardView prefab or StarterDeck asset.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- canvas + event system ---
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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

            var background = BattleUiKit.Image(canvasRect, "Background", new Color(0.08f, 0.1f, 0.16f, 1f));
            BattleUiKit.Stretch(background.rectTransform);
            background.raycastTarget = false;

            // --- stage: player row left, enemy row right, each unit carries its own HP bar ---
            var stage = BattleUiKit.Rect(canvasRect, "Stage");
            BattleUiKit.Anchor(stage, 0.03f, 0.52f, 0.97f, 0.94f);
            var playerRow = UnitRow(stage, "PlayerUnits", 0f, 0.45f, TextAnchor.LowerLeft);
            var enemyRow = UnitRow(stage, "EnemyUnits", 0.55f, 1f, TextAnchor.LowerRight);

            // --- overlay (popups + hover preview; forced last sibling below) ---
            var overlay = BattleUiKit.Rect(canvasRect, "Overlay");
            BattleUiKit.Stretch(overlay);

            // --- execution rail ---
            var railRect = BattleUiKit.Rect(canvasRect, "ExecutionRail");
            BattleUiKit.Anchor(railRect, 0.03f, 0.30f, 0.97f, 0.51f);
            var rail = railRect.gameObject.AddComponent<ExecutionRailView>();
            rail.EditorBuild(cardPrefab, overlay);

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

            // --- selection-mode dim + left-side cancel button (spec §6; hidden until targets are wanted) ---
            var dimLayer = BattleUiKit.Rect(canvasRect, "SelectionDim");
            BattleUiKit.Stretch(dimLayer);
            var dimImage = BattleUiKit.Image(dimLayer, "Dim", new Color(0f, 0f, 0f, 0.6f));
            BattleUiKit.Stretch(dimImage.rectTransform);
            var cancelButton = MakeButton(dimLayer, "CancelButton", "실행 취소", 20f, out _);
            Place((RectTransform)cancelButton.transform, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(150f, 48f));
            dimLayer.gameObject.SetActive(false);

            // Z-order: the dim covers everything except the rail (the selection candidates) and the
            // message line; popups/hover preview stay on the very top.
            dimLayer.SetAsLastSibling();
            railRect.SetAsLastSibling();
            ((RectTransform)message.transform).SetAsLastSibling();
            overlay.SetAsLastSibling();

            // --- controller wiring (field names must match BattleScreenController) ---
            var controllerGo = new GameObject("BattleScreenController");
            var controller = controllerGo.AddComponent<BattleScreenController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_deck").objectReferenceValue = deck;
            var arts = so.FindProperty("_enemyArtCards");
            arts.arraySize = EnemyArtCardPaths.Length;
            for (int i = 0; i < EnemyArtCardPaths.Length; i++)
            {
                arts.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<CardAsset>(EnemyArtCardPaths[i]);
            }

            so.FindProperty("_hand").objectReferenceValue = hand;
            so.FindProperty("_rail").objectReferenceValue = rail;
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
            so.FindProperty("_cancelButton").objectReferenceValue = cancelButton;
            so.FindProperty("_dimLayer").objectReferenceValue = dimLayer.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("BattleSceneBuilder: saved " + ScenePath);
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
```

- [ ] **Step 2: 컴파일 확인**

Unity Console 에러 0 (또는 batchmode 명령).
Expected: 컴파일 에러 없음. (에디터 asmdef는 이미 `Unity.InputSystem`을 참조한다 — 수정 불필요.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Unity/Editor/BattleSceneBuilder.cs
git commit -m "feat(editor): code-generated battle scene builder menu"
```

---

### Task 11: 씬 생성 + Unity Play 수동 검증 + 문서/메타 커밋

이 Task는 Unity 에디터가 필요하다 (사람 또는 에디터를 열 수 있는 환경).

**Files:**
- Create (생성물): `Assets/Scenes/FateWeaverBattle.unity` + 신규 `.cs`들의 `.meta`
- Modify: `Assets/Unity/PLAYTEST.md`

- [ ] **Step 1: 씬 생성**

Unity 에디터에서 `Fate Weaver ▸ Build Battle Scene` 실행.
Expected: Console에 `BattleSceneBuilder: saved Assets/Scenes/FateWeaverBattle.unity`, 에러 0.

- [ ] **Step 2: Play 수동 체크리스트**

`Assets/Scenes/FateWeaverBattle.unity`를 열고 Play:

1. 무대: 좌측에 플레이어 유닛(파랑 초상), 우측에 고블린 유닛(빨강 초상), **각 유닛 아래 HP 바** (30/30, 28/28)
2. 레일: 적 카드가 미니 카드(프레임+일러스트/틴트+상단 실행력 배지)로 표시, 카드가 많으면 좌우 드래그 스크롤
3. 레일 카드에 마우스 호버 → 전체 카드 프리뷰가 위에 표시, 벗어나면 사라짐
4. 손패: 5장이 곡선(가장자리 카드가 기울고 가라앉음)으로 배열
5. 손패 실행 카드 클릭 → 운명력 차감 + 레일에 배치; 개입 카드 클릭 → 레일 대상 클릭으로 적용(교환은 2회 클릭)
6. 선택 모드: 개입 카드 클릭 시 레일·메시지를 제외한 화면 전체가 딤 처리되고 좌측에 `실행 취소` 버튼 표시. 딤 상태에서 손패/턴 버튼 클릭이 막히는지, `실행 취소` 클릭 시 딤 해제 + 카드 미사용(운명력 그대로)인지, 대상 선택을 마치면 딤이 자동 해제되는지 확인
7. 덱 버튼: 좌하 뽑을 덱(이름순 목록), 우하 버린 덱, 우상 전체 덱 — 각각 팝업 열림/배경 클릭으로 닫힘, 개수 표기 갱신
8. `턴 실행` → HP 갱신, 라벨이 `다음 턴`으로; `다음 턴` → 새 손패/레일; `초기화` → 처음 상태
9. 승패 도달 시 메시지에 결과 표기, 턴 버튼 비활성
10. Console 에러 0

문제 발견 시 해당 Task로 돌아가 수정 후 재실행.

- [ ] **Step 3: PLAYTEST.md 갱신**

`Assets/Unity/PLAYTEST.md`의 `## 실행` 섹션 마지막에 추가:

```markdown
### 전투 화면 (시각 개편 1단계)

1. `Fate Weaver ▸ Build Battle Scene`으로 `Assets/Scenes/FateWeaverBattle.unity`를 생성(재실행 시 덮어씀)하고 Play.
2. 구도: 유닛 무대(유닛별 HP 바) / 스크롤 실행 레일(미니 카드, 호버 시 전체 카드) / 곡선 손패 /
   덱 버튼 3종(좌하 뽑을 덱 · 우하 버린 덱 · 우상 전체 덱) / 좌측 운명력 / 우측 턴 버튼.
3. 개입 카드의 대상 선택 중에는 레일을 제외한 화면이 딤 처리되고 좌측 `실행 취소` 버튼으로 취소한다.
4. 입력은 아직 클릭 2단계(1단계 범위) — 드래그(카드 내기)+클릭(대상 선택)은 2단계에서 교체 예정.
   구현 계획: `docs/superpowers/plans/2026-07-10-battle-screen-skeleton.md`.
```

- [ ] **Step 4: 헤드리스 회귀 최종 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj`
Expected: 전부 통과 (Failed 0).

- [ ] **Step 5: 씬/메타/문서 커밋**

```bash
git add Assets/Scenes/FateWeaverBattle.unity Assets/Scenes/FateWeaverBattle.unity.meta Assets/Unity/PLAYTEST.md
git add Assets/Unity/*.cs.meta Assets/Unity/Editor/BattleSceneBuilder.cs.meta Assets/Core/Simulation/Presentation Assets/Core/Tests/EditMode/HandFanLayoutTests.cs.meta Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs.meta
git commit -m "chore(unity): generate battle scene, metas, and playtest docs for phase 1"
```

(`Assets/Core/Simulation/Presentation`은 폴더 `.meta` 포함을 위해 디렉터리째 추가. `git status`로 누락 `.meta`가 없는지 확인 후 커밋.)
