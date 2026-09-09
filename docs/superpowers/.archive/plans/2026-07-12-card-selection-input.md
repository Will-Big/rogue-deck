# 카드 선택 입력 개편 (시각 개편 2단계) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** [스펙 §6](../../specs/2026-07-10-battle-scene-visual-design.md) "카드 입력 규칙 (클릭 선택 흐름)" 구현 — 호버 = 확대 보기, 클릭 = 선택, 실행은 항상 두 번째 명시적 입력(재클릭/대상 클릭/확인 버튼)에서만. 기존 즉시 실행 클릭과 좌측 실행 취소 버튼을 제거하고, 1단계 최종 리뷰 Minor 후속(빌더 InputActions 경고, 덱 리스트 다운캐스트 봉인, null 가드 일관화, 문서 폴리시)을 흡수한다.

**Architecture:** 선택 흐름의 판정(대상 수 분기, 어떤 입력이 어떤 커밋 커맨드를 내는가)은 순수 C# `CardSelectionMachine` + `CardTargetRules`로 분리해 헤드리스 TDD한다. Unity 쪽은 `CardSelectionController`(MonoBehaviour)가 머신을 구동하며 시각 요소(마우스 추적 카드, 타겟팅 화살표, 딤, 가운데 강조, 확인 버튼)를 소유하고, `BattleScreenController`는 세션 API 호출과 렌더만 유지한다. 씬은 `BattleSceneBuilder` 수정 후 재생성한다.

**Tech Stack:** Unity 6 uGUI + TMP + Input System(`Mouse.current`), 순수 C# 상태 머신(헤드리스 NUnit).

## Global Constraints

- `Assets/Core/**`는 **UnityEngine 참조 금지**. C# **LangVersion 9** (C# 10+ 금지).
- 헤드리스 테스트: 위치 `Assets/Core/Tests/EditMode/`, 네임스페이스 `FateWeaver.Tests`, 실행 명령(이 머신은 .NET 5 SDK만 있음 — csproj 수정 금지):
  `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
- Unity 쪽 코드는 헤드리스로 컴파일되지 않는다. **개별 태스크의 Unity 컴파일 확인은 생략**(에디터가 프로젝트 잠금 중일 수 있음)하고 파일 정독 자기 검토로 대체 — 마지막 태스크에서 일괄 검증한다. **모든 using 지시문을 특히 주의해서 검토할 것** (1단계에서 계획 코드의 using 누락이 컴파일 실패를 냈다).
- **`Assets/Unity/CardView.cs`는 절대 수정 금지** — 사용자의 별도 실험(카드 프레임)이 워킹 트리에 진행 중이다.
- **선행 조건: 워킹 트리의 `BattleSceneBuilder.cs` 미커밋 변경(UI 카메라 추가)은 이 계획 실행 전에 커밋되어 있어야 한다** (Task 9가 같은 파일을 수정한다). 컨트롤러가 실행 시작 시 확인한다.
- 카드 이름/설명은 `PlaytestKoreanText`/`CardPresentation` 경유(하드코딩 금지). 새 프리팹 제작 금지(코드 빌드, 기존 `CardView.prefab` 재사용).
- 씬은 Task 9 이후 반드시 `Fate Weaver ▸ Build Battle Scene`으로 **재생성**해야 한다(새 직렬화 필드 배선).
- `.meta` 파일은 Unity가 생성 — 마지막 태스크에서 일괄 커밋.
- 커밋 prefix 관례: `feat(...)` / `test(...)` / `chore(...)` / `docs(...)`.
- 1단계 Minor #6(BattleUiKit 정적 폰트 캐시)은 **의도적으로 제외**(무해, 해당 파일을 이번에 건드리지 않음).

---

### Task 1: CardTargetRules — 카드별 요구 대상 수 (순수 C#, TDD)

**Files:**
- Create: `Assets/Core/Simulation/Presentation/CardTargetRules.cs`
- Test: `Assets/Core/Tests/EditMode/CardTargetRulesTests.cs`

**Interfaces:**
- Consumes: `CardDefinition` (`Category`, `InterventionAction`), `InterventionActionKeys.SwapExecutionOrder` (`FateWeaver.Core.Intervention`)
- Produces: `CardTargetRules.RequiredTargets(CardDefinition def)` → `int` (실행/널 = 0, 교환 개입 = 2, 그 외 개입 = 1). Task 2의 `SelectCard` 인자와 Task 8의 분기가 이 값을 쓴다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Core/Tests/EditMode/CardTargetRulesTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Presentation;

namespace FateWeaver.Tests
{
    public class CardTargetRulesTests
    {
        private static CardDefinition Card(string id)
        {
            var def = StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)
                .FirstOrDefault(c => c.Id == id);
            Assert.IsNotNull(def, "starter deck is missing card: " + id);
            return def;
        }

        [Test]
        public void Execution_card_needs_no_targets()
        {
            Assert.AreEqual(0, CardTargetRules.RequiredTargets(Card("slash")));
        }

        [Test]
        public void Single_target_intervention_needs_one()
        {
            Assert.AreEqual(1, CardTargetRules.RequiredTargets(Card("pull_forward")));
        }

        [Test]
        public void Swap_intervention_needs_two()
        {
            Assert.AreEqual(2, CardTargetRules.RequiredTargets(Card("swap_positions")));
        }

        [Test]
        public void Null_definition_needs_no_targets()
        {
            Assert.AreEqual(0, CardTargetRules.RequiredTargets(null));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CardTargetRulesTests`
Expected: 컴파일 에러 CS0103/CS0246 — `CardTargetRules` 미정의.

- [ ] **Step 3: 최소 구현**

`Assets/Core/Simulation/Presentation/CardTargetRules.cs`:

```csharp
using FateWeaver.Core.Cards;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Presentation
{
    /// <summary>How many rail targets a card must pick before it can execute (spec §6 selection
    /// flow). Pure C# so the selection state machine stays headless-testable.</summary>
    public static class CardTargetRules
    {
        public static int RequiredTargets(CardDefinition def)
        {
            if (def == null || def.Category != CardCategory.Intervention || def.InterventionAction == null)
            {
                return 0;
            }

            return def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder ? 2 : 1;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CardTargetRulesTests`
Expected: `Passed!` — 4 tests.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Presentation/CardTargetRules.cs Assets/Core/Tests/EditMode/CardTargetRulesTests.cs
git commit -m "feat(input-core): card target rules with headless tests"
```

---

### Task 2: CardSelectionMachine — 선택 흐름 상태 머신 (순수 C#, TDD)

**Files:**
- Create: `Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`
- Test: `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`

**Interfaces:**
- Consumes: 없음 (순수 상태 머신 — 카드 지식은 호출자가 `requiredTargets`로 공급)
- Produces (Task 7/8이 사용):
  - `enum SelectionPhase { Idle, ConfirmPlacement, PickSingleTarget, PickMultipleTargets, ReadyToConfirm }`
  - `struct SelectionCommand { bool PlayExecution; bool PlayIntervention; int HandIndex; int TargetA; int TargetB; }` + 팩토리 `None`/`Execution(hand)`/`Intervention(hand, a, b=-1)`
  - `CardSelectionMachine`: `Phase`, `SelectedHandIndex`, `RequiredTargets`, `PickedTargets`(IReadOnlyList&lt;int&gt;), `SelectCard(int handIndex, int requiredTargets)`, `ClickApplyArea()`, `ClickTarget(int zoneIndex)`, `Confirm()`, `Cancel()` — 커맨드를 반환하는 메서드는 커밋 시 내부 상태를 Idle로 리셋한다.
- 라우팅 규약: **ConfirmPlacement 중 레일 카드 클릭은 컨트롤러가 `ClickApplyArea()`로 라우팅**한다(머신의 `ClickTarget`은 그 페이즈에서 무시).

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Simulation.Presentation;

namespace FateWeaver.Tests
{
    public class CardSelectionMachineTests
    {
        [Test]
        public void Starts_idle()
        {
            Assert.AreEqual(SelectionPhase.Idle, new CardSelectionMachine().Phase);
        }

        [Test]
        public void Zero_target_card_waits_for_apply_area_click()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(2, 0);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);

            var command = machine.ClickApplyArea();

            Assert.IsTrue(command.PlayExecution);
            Assert.AreEqual(2, command.HandIndex);
            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
        }

        [Test]
        public void Apply_area_click_does_nothing_while_picking_targets()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 1);

            var command = machine.ClickApplyArea();

            Assert.IsFalse(command.PlayExecution || command.PlayIntervention);
            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
        }

        [Test]
        public void Single_target_commits_on_target_click()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(1, 1);

            var command = machine.ClickTarget(3);

            Assert.IsTrue(command.PlayIntervention);
            Assert.AreEqual(1, command.HandIndex);
            Assert.AreEqual(3, command.TargetA);
            Assert.AreEqual(-1, command.TargetB);
            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
        }

        [Test]
        public void Target_click_in_confirm_placement_is_ignored()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 0);

            var command = machine.ClickTarget(1);

            Assert.IsFalse(command.PlayExecution || command.PlayIntervention);
            Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);
        }

        [Test]
        public void Two_target_flow_requires_distinct_picks_then_confirm()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(4, 2);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);

            Assert.IsFalse(machine.ClickTarget(1).PlayIntervention);
            CollectionAssert.AreEqual(new[] { 1 }, machine.PickedTargets);

            Assert.IsFalse(machine.ClickTarget(1).PlayIntervention);
            CollectionAssert.AreEqual(new[] { 1 }, machine.PickedTargets);

            Assert.IsFalse(machine.ClickTarget(3).PlayIntervention);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

            var command = machine.Confirm();

            Assert.IsTrue(command.PlayIntervention);
            Assert.AreEqual(4, command.HandIndex);
            Assert.AreEqual(1, command.TargetA);
            Assert.AreEqual(3, command.TargetB);
            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
        }

        [Test]
        public void Confirm_before_requirement_met_does_nothing()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);

            var command = machine.Confirm();

            Assert.IsFalse(command.PlayIntervention);
            Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
        }

        [Test]
        public void Cancel_clears_everything_without_command()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);

            machine.Cancel();

            Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
            Assert.AreEqual(-1, machine.SelectedHandIndex);
        }

        [Test]
        public void Selecting_another_card_resets_previous_picks()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);

            machine.SelectCard(3, 1);

            Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
            Assert.AreEqual(0, machine.PickedTargets.Count);
            Assert.AreEqual(3, machine.SelectedHandIndex);
        }

        [Test]
        public void Extra_target_click_after_ready_is_ignored()
        {
            var machine = new CardSelectionMachine();
            machine.SelectCard(0, 2);
            machine.ClickTarget(1);
            machine.ClickTarget(2);
            Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

            Assert.IsFalse(machine.ClickTarget(4).PlayIntervention);
            CollectionAssert.AreEqual(new[] { 1, 2 }, machine.PickedTargets);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CardSelectionMachineTests`
Expected: 컴파일 에러 — `CardSelectionMachine`/`SelectionPhase` 미정의.

- [ ] **Step 3: 최소 구현**

`Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Simulation.Presentation
{
    public enum SelectionPhase
    {
        Idle,
        ConfirmPlacement,
        PickSingleTarget,
        PickMultipleTargets,
        ReadyToConfirm
    }

    /// <summary>Command the UI must run against the session after an input step.
    /// At most one of PlayExecution / PlayIntervention is set; neither means "keep going".</summary>
    public readonly struct SelectionCommand
    {
        public bool PlayExecution { get; }
        public bool PlayIntervention { get; }
        public int HandIndex { get; }
        public int TargetA { get; }
        public int TargetB { get; }

        private SelectionCommand(bool playExecution, bool playIntervention, int handIndex, int targetA, int targetB)
        {
            PlayExecution = playExecution;
            PlayIntervention = playIntervention;
            HandIndex = handIndex;
            TargetA = targetA;
            TargetB = targetB;
        }

        public static SelectionCommand None => new SelectionCommand(false, false, -1, -1, -1);

        public static SelectionCommand Execution(int handIndex)
            => new SelectionCommand(true, false, handIndex, -1, -1);

        public static SelectionCommand Intervention(int handIndex, int targetA, int targetB = -1)
            => new SelectionCommand(false, true, handIndex, targetA, targetB);
    }

    /// <summary>Selection-flow state machine for spec §6: click = select, execution only on the second
    /// explicit input (apply-area re-click / target click / confirm). Pure C# — the Unity controller
    /// feeds it clicks and runs the returned command; all presentation stays outside. The controller
    /// routes rail-card clicks during ConfirmPlacement to ClickApplyArea (they are inside the apply
    /// area); ClickTarget ignores that phase on purpose.</summary>
    public sealed class CardSelectionMachine
    {
        private readonly List<int> _picked = new List<int>();

        public SelectionPhase Phase { get; private set; } = SelectionPhase.Idle;
        public int SelectedHandIndex { get; private set; } = -1;
        public int RequiredTargets { get; private set; }
        public IReadOnlyList<int> PickedTargets => _picked;

        public void SelectCard(int handIndex, int requiredTargets)
        {
            Cancel();
            SelectedHandIndex = handIndex;
            RequiredTargets = requiredTargets;
            Phase = requiredTargets <= 0 ? SelectionPhase.ConfirmPlacement
                : requiredTargets == 1 ? SelectionPhase.PickSingleTarget
                : SelectionPhase.PickMultipleTargets;
        }

        /// <summary>Click anywhere on the apply area (the rail region) — confirms a pending placement.</summary>
        public SelectionCommand ClickApplyArea()
        {
            if (Phase != SelectionPhase.ConfirmPlacement)
            {
                return SelectionCommand.None;
            }

            var command = SelectionCommand.Execution(SelectedHandIndex);
            Cancel();
            return command;
        }

        /// <summary>Click on a rail card. Single-target commits immediately; multi-target accumulates
        /// distinct picks until the requirement is met, then waits for Confirm.</summary>
        public SelectionCommand ClickTarget(int zoneIndex)
        {
            if (Phase == SelectionPhase.PickSingleTarget)
            {
                var command = SelectionCommand.Intervention(SelectedHandIndex, zoneIndex);
                Cancel();
                return command;
            }

            if (Phase == SelectionPhase.PickMultipleTargets && !_picked.Contains(zoneIndex))
            {
                _picked.Add(zoneIndex);
                if (_picked.Count >= RequiredTargets)
                {
                    Phase = SelectionPhase.ReadyToConfirm;
                }
            }

            return SelectionCommand.None;
        }

        /// <summary>Commit a satisfied multi-pick. The session API takes two targets, which matches
        /// the current maximum (swap); a future 3+-target card needs a session API change first.</summary>
        public SelectionCommand Confirm()
        {
            if (Phase != SelectionPhase.ReadyToConfirm)
            {
                return SelectionCommand.None;
            }

            var command = SelectionCommand.Intervention(SelectedHandIndex, _picked[0], _picked[1]);
            Cancel();
            return command;
        }

        public void Cancel()
        {
            Phase = SelectionPhase.Idle;
            SelectedHandIndex = -1;
            RequiredTargets = 0;
            _picked.Clear();
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CardSelectionMachineTests`
Expected: `Passed!` — 10 tests.

- [ ] **Step 5: 전체 회귀 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 통과 (Failed 0).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Simulation/Presentation/CardSelectionMachine.cs Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs
git commit -m "feat(input-core): selection-flow state machine with headless tests"
```

---

### Task 3: 1단계 리뷰 후속 — 덱 리스트 다운캐스트 봉인 + 문서 폴리시 (TDD)

**Files:**
- Modify: `Assets/Core/Combat/Deck.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Modify: `Assets/Core/Simulation/Presentation/HandFanLayout.cs` (문서만)
- Test: `Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs` (테스트 1개 추가)

**Interfaces:**
- Consumes: 기존 `DrawPile`/`DiscardPile`/`AllDeckCards` 계약
- Produces: 같은 시그니처 유지(`IReadOnlyList<CardDefinition>`), 반환 인스턴스만 `ReadOnlyCollection` 래퍼로 교체 — 소비자 코드 변경 없음.

- [ ] **Step 1: 실패하는 테스트 추가**

`Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs` — using 블록에 `using System.Collections.Generic;` 추가하고, 클래스 마지막에 테스트 추가:

```csharp
        [Test]
        public void Piles_are_not_downcastable_to_mutable_lists()
        {
            var session = NewSession();

            Assert.IsNotInstanceOf<List<CardDefinition>>(session.DrawPile);
            Assert.IsNotInstanceOf<List<CardDefinition>>(session.DiscardPile);
            Assert.IsNotInstanceOf<List<CardDefinition>>(session.AllDeckCards);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter DeckPileVisibilityTests`
Expected: FAIL — `Piles_are_not_downcastable_to_mutable_lists` 1건 실패(현재는 live `List` 노출), 나머지 통과.

- [ ] **Step 3: 구현 — ReadOnlyCollection 래퍼**

`Assets/Core/Combat/Deck.cs`:

(a) using 블록에 추가: `using System.Collections.ObjectModel;`

(b) 필드 블록의 `private readonly Random _rng;` 아래에 추가:

```csharp
        private readonly ReadOnlyCollection<CardDefinition> _drawView;
        private readonly ReadOnlyCollection<CardDefinition> _discardView;
```

(c) 생성자의 `Shuffle(_draw);` 아래에 추가:

```csharp
            _drawView = _draw.AsReadOnly();
            _discardView = _discard.AsReadOnly();
```

(d) 기존 두 프로퍼티를 다음으로 교체:

```csharp
        /// <summary>Read-only pile views for deck-viewer UI. Draw order is real — UI must sort for display.</summary>
        public IReadOnlyList<CardDefinition> DrawPile => _drawView;
        public IReadOnlyList<CardDefinition> DiscardPile => _discardView;
```

`Assets/Core/Simulation/DeckCombatSession.cs`:

(a) using 블록에 추가: `using System.Collections.ObjectModel;`

(b) 필드 선언을 교체: `private readonly List<CardDefinition> _allCards;` → `private readonly ReadOnlyCollection<CardDefinition> _allCards;`

(c) 생성자 대입을 교체: `_allCards = new List<CardDefinition>(deckCards);` → `_allCards = new List<CardDefinition>(deckCards).AsReadOnly();`

(`AllDeckCards => _allCards` 프로퍼티는 그대로 컴파일된다.)

- [ ] **Step 4: HandFanLayout 문서 폴리시 (동작 변경 없음)**

`Assets/Core/Simulation/Presentation/HandFanLayout.cs`의 `FanPose` 프로퍼티 3개를 다음으로 교체:

```csharp
        /// <summary>Signed X offset from the fan center in abstract units (left cards negative).</summary>
        public float XOffset { get; }

        /// <summary>Vertical offset from the fan center (edge cards sink below 0; center card is 0).</summary>
        public float YOffset { get; }

        /// <summary>Z tilt in degrees, Unity CCW-positive (left cards tilt CCW, i.e. positive).</summary>
        public float AngleDegrees { get; }
```

그리고 `HandFanLayout` 클래스 doc comment 끝에 한 줄 추가 (기존 `<summary>` 안):

```csharp
    /// <summary>Curved-fan hand layout. Pure C# (no UnityEngine) so it stays headless-testable.
    /// Callers drive count from a non-empty hand list; no count guard is added on purpose (YAGNI).</summary>
```

- [ ] **Step 5: 통과 + 전체 회귀 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 통과 (새 테스트 포함, Failed 0).

- [ ] **Step 6: 커밋**

```bash
git add Assets/Core/Combat/Deck.cs Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Simulation/Presentation/HandFanLayout.cs Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs
git commit -m "chore(core): seal pile views against downcast and polish fan-layout docs"
```

---

### Task 4: HandCardHoverEffect + HandFanView 확장 — 호버 확대 / Held / Ghost

**Files:**
- Create: `Assets/Unity/HandCardHoverEffect.cs`
- Modify: `Assets/Unity/HandFanView.cs` (전체 교체 — 아래 코드)

**Interfaces:**
- Consumes: `HandFanLayout.PoseFor` (기존), `CardView` 프리팹
- Produces (Task 7/8이 사용):
  - `HandCardHoverEffect.Suppress(bool on)` (static — 선택 진행 중 호버 팝 억제), `Capture()`, `Hold(bool on)`
  - `HandFanView.SetHeld(int index, bool on)` — 단일 대상 선택 중 카드를 확대 상태로 고정
  - `HandFanView.SetGhost(int index, bool on)` — 0대상 흐름에서 원본 카드 반투명화
  - 기존 `SetCards`/`SetSelection`/`EditorBuild` 시그니처 유지 (`SetSelection`은 Task 8에서 호출자와 함께 제거)

- [ ] **Step 1: HandCardHoverEffect 작성**

`Assets/Unity/HandCardHoverEffect.cs`:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace FateWeaver.Unity
{
    /// <summary>Hover = enlarged reading view for a hand card (spec §6): straighten, lift, scale up,
    /// and draw above neighbors; restore the fan pose on exit. Hold() freezes the enlarged pose while
    /// the card is the current selection; Suppress() disables hover pops during selection flows.</summary>
    public sealed class HandCardHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HoverScale = 1.35f;
        private const float HoverLift = 46f;

        private static bool _suppressed;

        private RectTransform _rect;
        private Vector2 _basePosition;
        private Quaternion _baseRotation;
        private int _baseSiblingIndex;
        private bool _hovering;
        private bool _held;

        public static void Suppress(bool on)
        {
            _suppressed = on;
        }

        /// <summary>Cache the fan pose. Call after HandFanView positions the card.</summary>
        public void Capture()
        {
            _rect = (RectTransform)transform;
            _basePosition = _rect.anchoredPosition;
            _baseRotation = _rect.localRotation;
            _baseSiblingIndex = _rect.GetSiblingIndex();
        }

        public void Hold(bool on)
        {
            _held = on;
            if (on)
            {
                Enlarge();
            }
            else if (!_hovering)
            {
                Restore();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_suppressed || _held)
            {
                return;
            }

            _hovering = true;
            Enlarge();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            if (_held)
            {
                return;
            }

            Restore();
        }

        private void Enlarge()
        {
            if (_rect == null)
            {
                Capture();
            }

            _rect.SetAsLastSibling();
            _rect.localRotation = Quaternion.identity;
            _rect.anchoredPosition = _basePosition + new Vector2(0f, HoverLift);
            _rect.localScale = Vector3.one * HoverScale;
        }

        private void Restore()
        {
            if (_rect == null)
            {
                return;
            }

            _rect.SetSiblingIndex(_baseSiblingIndex);
            _rect.localRotation = _baseRotation;
            _rect.anchoredPosition = _basePosition;
            _rect.localScale = Vector3.one;
        }
    }
}
```

- [ ] **Step 2: HandFanView 전체 교체**

`Assets/Unity/HandFanView.cs` 전체를 다음으로 교체:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Presentation;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>The hand as a slight curved fan (spec §2): full CardViews positioned by HandFanLayout,
    /// no layout group — poses are absolute so cards can tilt. Hovering a card enlarges it for reading
    /// (spec §6); Held/Ghost states support the selection flow.
    /// Geometry (Spacing across the 900px root) assumes the session's hand size stays ≤ 5.</summary>
    public sealed class HandFanView : MonoBehaviour
    {
        [SerializeField] private CardView _cardPrefab;

        private const float Spacing = 150f;
        private const float AnglePerCard = 4f;
        private const float ArcDrop = 10f;
        private static readonly Vector2 CardSize = new Vector2(170f, 238f);

        private readonly List<CardView> _views = new List<CardView>();
        private readonly List<HandCardHoverEffect> _hoverEffects = new List<HandCardHoverEffect>();
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();

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
            _hoverEffects.Clear();
            _groups.Clear();
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
                var hover = view.gameObject.AddComponent<HandCardHoverEffect>();
                hover.Capture();
                _hoverEffects.Add(hover);
                _groups.Add(view.gameObject.AddComponent<CanvasGroup>());
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

        /// <summary>Freeze/release the enlarged reading pose (single-target selection keeps the card up).</summary>
        public void SetHeld(int index, bool on)
        {
            if (index >= 0 && index < _hoverEffects.Count)
            {
                _hoverEffects[index].Hold(on);
            }
        }

        /// <summary>Ghost the original while a floating copy tracks the mouse (0-target flow).</summary>
        public void SetGhost(int index, bool on)
        {
            if (index >= 0 && index < _groups.Count)
            {
                _groups[index].alpha = on ? 0.35f : 1f;
            }
        }
    }
}
```

- [ ] **Step 3: 자기 검토 (using/전사 오류) 후 커밋**

```bash
git add Assets/Unity/HandCardHoverEffect.cs Assets/Unity/HandFanView.cs
git commit -m "feat(ui): hand hover enlarge with held and ghost states"
```

---

### Task 5: TargetingArrowView — 클릭 지점→커서 타겟팅 화살표

**Files:**
- Create: `Assets/Unity/TargetingArrowView.cs`

**Interfaces:**
- Consumes: `BattleUiKit.Rect/Image/Stretch` (기존)
- Produces (Task 7이 사용): `TargetingArrowView.Create(RectTransform overlay)` → 인스턴스(비활성 상태로 생성); `Show(Vector2 startScreen)`; `Track(Vector2 currentScreen)`; `Hide()` — 스크린 좌표 입력(ScreenSpaceOverlay 캔버스, 카메라 null 변환).

- [ ] **Step 1: TargetingArrowView 작성**

`Assets/Unity/TargetingArrowView.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Targeting arrow for single-target selection (spec §6): anchored where the card was
    /// clicked, the shaft stretches and rotates so the diamond tip tracks the cursor. Code-built on
    /// the overlay layer; all screen-point math assumes a ScreenSpaceOverlay canvas (null camera).</summary>
    public sealed class TargetingArrowView : MonoBehaviour
    {
        private static readonly Color ArrowColor = new Color(0.95f, 0.72f, 0.25f, 0.9f);

        private RectTransform _root;
        private RectTransform _shaft;
        private RectTransform _head;
        private Vector2 _startLocal;

        public static TargetingArrowView Create(RectTransform overlay)
        {
            var root = BattleUiKit.Rect(overlay, "TargetingArrow");
            BattleUiKit.Stretch(root);
            var view = root.gameObject.AddComponent<TargetingArrowView>();
            view._root = root;

            var shaft = BattleUiKit.Image(root, "Shaft", ArrowColor);
            var shaftRect = shaft.rectTransform;
            shaftRect.anchorMin = shaftRect.anchorMax = new Vector2(0.5f, 0.5f);
            shaftRect.pivot = new Vector2(0f, 0.5f);
            shaftRect.sizeDelta = new Vector2(0f, 6f);
            shaft.raycastTarget = false;
            view._shaft = shaftRect;

            var head = BattleUiKit.Image(root, "Head", ArrowColor);
            var headRect = head.rectTransform;
            headRect.anchorMin = headRect.anchorMax = new Vector2(0.5f, 0.5f);
            headRect.sizeDelta = new Vector2(18f, 18f);
            head.raycastTarget = false;
            view._head = headRect;

            root.gameObject.SetActive(false);
            return view;
        }

        public void Show(Vector2 startScreen)
        {
            _startLocal = ToLocal(startScreen);
            gameObject.SetActive(true);
            Track(startScreen);
        }

        public void Track(Vector2 currentScreen)
        {
            var current = ToLocal(currentScreen);
            var delta = current - _startLocal;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _shaft.anchoredPosition = _startLocal;
            _shaft.sizeDelta = new Vector2(length, 6f);
            _shaft.localRotation = Quaternion.Euler(0f, 0f, angle);
            _head.anchoredPosition = current;
            _head.localRotation = Quaternion.Euler(0f, 0f, angle + 45f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private Vector2 ToLocal(Vector2 screen)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
            return local;
        }
    }
}
```

- [ ] **Step 2: 자기 검토 후 커밋**

```bash
git add Assets/Unity/TargetingArrowView.cs
git commit -m "feat(ui): cursor-tracking targeting arrow view"
```

---

### Task 6: ExecutionRailView 확장 — 드롭 힌트 / 레일 영역 클릭 / 다중 선택 하이라이트

**Files:**
- Modify: `Assets/Unity/ExecutionRailView.cs`

**Interfaces:**
- Consumes: 기존 레일 구조 (`EditorBuild`가 만든 viewport backdrop)
- Produces (Task 7/8이 사용):
  - `SetDropHint(bool on)` — 레일 배경을 매우 약한 호박색으로 (스펙 §6 "적용 예정 영역 하이라이트")
  - `SetRailClicked(Action onRailClicked)` — 레일의 빈 배경 클릭 콜백 (0대상 배치 확정용)
  - `SetPickedTargets(IReadOnlyList<int> picked)` — 선택된 대상 타일들에 Secondary 외곽선, `null`이면 전체 해제
  - **주의: `EditorBuild`에 새 직렬화 필드(`_backdrop`, `_railClickButton`) 배선이 추가되므로 Task 9 이후 씬 재생성 필수** (기존 씬에서는 두 필드가 null — `SetDropHint`/`SetRailClicked`는 null 가드로 무동작).

- [ ] **Step 1: 필드/상수 추가**

`Assets/Unity/ExecutionRailView.cs`에서:

(a) `[SerializeField] private RectTransform _previewLayer;` 아래에 추가:

```csharp
        [SerializeField] private Image _backdrop;
        [SerializeField] private Button _railClickButton;
```

(b) `private static readonly Vector2 PreviewSize ...` 아래에 추가:

```csharp
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color DropHintColor = new Color(0.95f, 0.72f, 0.25f, 0.14f);
```

(c) `private CardView _preview;` 아래에 추가:

```csharp
        private Action _onRailClicked;
```

- [ ] **Step 2: Awake + 공개 메서드 추가**

`EditorBuild` 메서드 바로 위에 추가:

```csharp
        private void Awake()
        {
            if (_railClickButton != null)
            {
                _railClickButton.onClick.AddListener(() => _onRailClicked?.Invoke());
            }
        }

        /// <summary>Runtime hook for clicks on the rail's empty background (confirms a pending placement).</summary>
        public void SetRailClicked(Action onRailClicked)
        {
            _onRailClicked = onRailClicked;
        }

        /// <summary>Very weak amber wash marking the rail as the apply area while a placement is pending.</summary>
        public void SetDropHint(bool on)
        {
            if (_backdrop != null)
            {
                _backdrop.color = on ? DropHintColor : BackdropColor;
            }
        }

        /// <summary>Secondary outlines on the picked target tiles; null clears all.</summary>
        public void SetPickedTargets(IReadOnlyList<int> picked)
        {
            for (int i = 0; i < _views.Count; i++)
            {
                bool isPicked = false;
                if (picked != null)
                {
                    for (int p = 0; p < picked.Count; p++)
                    {
                        if (picked[p] == i)
                        {
                            isPicked = true;
                            break;
                        }
                    }
                }

                _views[i].SetSelection(isPicked ? CardView.SelectionKind.Secondary : CardView.SelectionKind.None);
            }
        }
```

- [ ] **Step 3: EditorBuild 배선 추가**

`EditorBuild` 안에서 `backdrop.color = new Color(0f, 0f, 0f, 0.25f);` 줄을 다음으로 교체:

```csharp
            backdrop.color = BackdropColor;
            _backdrop = backdrop;
            var railButton = viewport.gameObject.AddComponent<Button>();
            railButton.targetGraphic = backdrop;
            railButton.transition = Selectable.Transition.None;
            _railClickButton = railButton;
```

- [ ] **Step 4: 자기 검토 후 커밋**

using 확인: 이 파일은 이미 `System`(Action), `System.Collections.Generic`, `UnityEngine`, `UnityEngine.UI`(Image/Button/Selectable)를 임포트하고 있다 — 추가 불필요.

```bash
git add Assets/Unity/ExecutionRailView.cs
git commit -m "feat(ui): rail drop hint, background click hook, and multi-pick highlights"
```

---

### Task 7: CardSelectionController — 선택 흐름 오케스트레이터 (+asmdef Input System)

**Files:**
- Create: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/FateWeaver.Unity.asmdef` (references에 `Unity.InputSystem` 추가 — `Mouse.current` 사용)

**Interfaces:**
- Consumes: `CardSelectionMachine`/`SelectionCommand`/`SelectionPhase` (Task 2), `HandFanView.SetHeld/SetGhost` (Task 4), `TargetingArrowView` (Task 5), `ExecutionRailView.SetDropHint/SetPickedTargets` (Task 6), `CardView`/`CardPresentation`
- Produces (Task 8/9가 사용):
  - [SerializeField] 필드명 (Task 9 빌더가 FindProperty로 배선): `_hand`, `_rail`, `_dimLayer`, `_confirmButton`, `_overlay`, `_cardPrefab`
  - `Initialize(Action<SelectionCommand> onCommand)`; `bool SelectionActive`; `BeginSelection(int handIndex, int requiredTargets, CardPresentation card)`; `bool OnZoneClicked(int zoneIndex, CardPresentation zoneCard)`; `OnRailAreaClicked()`; `CancelSelection()` (빈 곳 클릭 취소는 Task 8의 컨트롤러가 `SelectionActive` 확인 후 `CancelSelection()` 호출로 처리)

- [ ] **Step 1: asmdef에 Input System 참조 추가**

`Assets/Unity/FateWeaver.Unity.asmdef`의 references 배열을 다음으로 교체 (전체 파일):

```json
{
    "name": "FateWeaver.Unity",
    "rootNamespace": "FateWeaver.Unity",
    "references": [
        "FateWeaver.Core",
        "FateWeaver.Simulation",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: CardSelectionController 작성**

`Assets/Unity/CardSelectionController.cs`:

```csharp
using System;
using System.Collections;
using FateWeaver.Simulation.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Runs spec §6's selection flow on top of the pure CardSelectionMachine. Click = select,
    /// then per target count — 0: an enlarged copy tracks the mouse and the rail shows a weak drop
    /// hint until the rail is clicked; 1: the card stays enlarged in the hand while a targeting arrow
    /// tracks the cursor until a rail card is clicked; 2+: everything but the rail dims, each pick
    /// plays a brief center emphasis, then the bottom-right confirm button commits. Owns all selection
    /// visuals; commits by handing the SelectionCommand to BattleScreenController. Presentation only —
    /// no session access.</summary>
    public sealed class CardSelectionController : MonoBehaviour
    {
        [SerializeField] private HandFanView _hand;
        [SerializeField] private ExecutionRailView _rail;
        [SerializeField] private GameObject _dimLayer;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private RectTransform _overlay;
        [SerializeField] private CardView _cardPrefab;

        private const float FloatingScale = 1.25f;
        private const float FloatingLift = 30f;
        private const float EmphasisHoldSeconds = 0.55f;
        private const float EmphasisGrowSeconds = 0.12f;

        private readonly CardSelectionMachine _machine = new CardSelectionMachine();
        private Action<SelectionCommand> _onCommand;
        private TargetingArrowView _arrow;
        private CardView _floatingCard;
        private CardView _emphasisCard;
        private Coroutine _emphasis;
        private int _visualHandIndex = -1;

        public bool SelectionActive => _machine.Phase != SelectionPhase.Idle;

        private void Awake()
        {
            _arrow = TargetingArrowView.Create(_overlay);
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        public void Initialize(Action<SelectionCommand> onCommand)
        {
            _onCommand = onCommand;
        }

        public void BeginSelection(int handIndex, int requiredTargets, CardPresentation card)
        {
            EndSelectionVisuals();
            _machine.SelectCard(handIndex, requiredTargets);
            _visualHandIndex = handIndex;
            HandCardHoverEffect.Suppress(true);

            if (_machine.Phase == SelectionPhase.ConfirmPlacement)
            {
                _rail.SetDropHint(true);
                _hand.SetGhost(handIndex, true);
                SpawnFloatingCard(card);
            }
            else if (_machine.Phase == SelectionPhase.PickSingleTarget)
            {
                _hand.SetHeld(handIndex, true);
                _arrow.Show(MouseScreen());
            }
            else
            {
                _dimLayer.SetActive(true);
            }
        }

        /// <summary>Rail-card click. Returns true when consumed by the selection flow.</summary>
        public bool OnZoneClicked(int zoneIndex, CardPresentation zoneCard)
        {
            if (!SelectionActive)
            {
                return false;
            }

            if (_machine.Phase == SelectionPhase.ConfirmPlacement)
            {
                Dispatch(_machine.ClickApplyArea());
                return true;
            }

            int before = _machine.PickedTargets.Count;
            var command = _machine.ClickTarget(zoneIndex);
            if (_machine.Phase == SelectionPhase.PickMultipleTargets || _machine.Phase == SelectionPhase.ReadyToConfirm)
            {
                _rail.SetPickedTargets(_machine.PickedTargets);
                if (_machine.PickedTargets.Count > before)
                {
                    PlayCenterEmphasis(zoneCard);
                }

                _confirmButton.gameObject.SetActive(_machine.Phase == SelectionPhase.ReadyToConfirm);
            }

            Dispatch(command);
            return true;
        }

        public void OnRailAreaClicked()
        {
            if (SelectionActive)
            {
                Dispatch(_machine.ClickApplyArea());
            }
        }

        public void CancelSelection()
        {
            _machine.Cancel();
            EndSelectionVisuals();
        }

        private void OnConfirmClicked()
        {
            Dispatch(_machine.Confirm());
        }

        private void Dispatch(SelectionCommand command)
        {
            if (!command.PlayExecution && !command.PlayIntervention)
            {
                if (!SelectionActive)
                {
                    EndSelectionVisuals();
                }

                return;
            }

            EndSelectionVisuals();
            _onCommand?.Invoke(command);
        }

        private void Update()
        {
            if (_machine.Phase == SelectionPhase.ConfirmPlacement && _floatingCard != null)
            {
                MoveToScreen((RectTransform)_floatingCard.transform, MouseScreen());
            }
            else if (_machine.Phase == SelectionPhase.PickSingleTarget)
            {
                _arrow.Track(MouseScreen());
            }
        }

        private void SpawnFloatingCard(CardPresentation card)
        {
            if (_floatingCard == null)
            {
                _floatingCard = Instantiate(_cardPrefab, _overlay);
                var rect = (RectTransform)_floatingCard.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(170f, 238f);
                rect.localScale = Vector3.one * FloatingScale;
                rect.localRotation = Quaternion.identity;
                DisableRaycasts(_floatingCard);
            }

            _floatingCard.gameObject.SetActive(true);
            _floatingCard.Bind(card, null);
            MoveToScreen((RectTransform)_floatingCard.transform, MouseScreen());
        }

        private void PlayCenterEmphasis(CardPresentation card)
        {
            if (_emphasis != null)
            {
                StopCoroutine(_emphasis);
            }

            _emphasis = StartCoroutine(CenterEmphasis(card));
        }

        private IEnumerator CenterEmphasis(CardPresentation card)
        {
            if (_emphasisCard == null)
            {
                _emphasisCard = Instantiate(_cardPrefab, _overlay);
                var rect = (RectTransform)_emphasisCard.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(200f, 280f);
                DisableRaycasts(_emphasisCard);
            }

            _emphasisCard.gameObject.SetActive(true);
            _emphasisCard.Bind(card, null);
            var rectT = (RectTransform)_emphasisCard.transform;
            float t = 0f;
            while (t < EmphasisGrowSeconds)
            {
                t += Time.deltaTime;
                rectT.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t / EmphasisGrowSeconds);
                yield return null;
            }

            rectT.localScale = Vector3.one;
            yield return new WaitForSeconds(EmphasisHoldSeconds);
            _emphasisCard.gameObject.SetActive(false);
            _emphasis = null;
        }

        private void EndSelectionVisuals()
        {
            HandCardHoverEffect.Suppress(false);
            _rail.SetDropHint(false);
            _rail.SetPickedTargets(null);
            _dimLayer.SetActive(false);
            _confirmButton.gameObject.SetActive(false);
            _arrow.Hide();
            _hand.SetGhost(_visualHandIndex, false);
            _hand.SetHeld(_visualHandIndex, false);
            _visualHandIndex = -1;
            if (_floatingCard != null)
            {
                _floatingCard.gameObject.SetActive(false);
            }

            if (_emphasis != null)
            {
                StopCoroutine(_emphasis);
                _emphasis = null;
            }

            if (_emphasisCard != null)
            {
                _emphasisCard.gameObject.SetActive(false);
            }
        }

        private static void DisableRaycasts(CardView card)
        {
            foreach (var graphic in card.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static Vector2 MouseScreen()
        {
            var mouse = Mouse.current;
            return mouse != null ? (Vector2)mouse.position.ReadValue() : Vector2.zero;
        }

        private void MoveToScreen(RectTransform rect, Vector2 screen)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, null, out local);
            rect.anchoredPosition = local + new Vector2(0f, FloatingLift);
        }
    }
}
```

- [ ] **Step 3: 자기 검토 후 커밋**

```bash
git add Assets/Unity/CardSelectionController.cs Assets/Unity/FateWeaver.Unity.asmdef
git commit -m "feat(ui): card selection controller orchestrating the spec 6 flow"
```

---

### Task 8: BattleScreenController 재배선 — 선택 흐름 경유 + 구 입력 제거

**Files:**
- Modify: `Assets/Unity/BattleScreenController.cs` (전체 교체 — 아래 코드)
- Modify: `Assets/Unity/HandFanView.cs` (`SetSelection` 메서드 삭제 — 마지막 호출자가 사라짐)
- Modify: `Assets/Unity/ExecutionRailView.cs` (`SetSelection(int, CardView.SelectionKind)` 메서드 삭제 — `SetPickedTargets`가 대체)

**Interfaces:**
- Consumes: Task 1/2/7의 산출물 전부, 세션 API (`PlayExecutionCard(int)`, `PlayInterventionCard(int, int, int = -1)` 등)
- Produces (Task 9 빌더가 FindProperty로 배선하는 [SerializeField] 필드명 — **17개 전부**): `_deck`, `_enemyArtCards`, `_hand`, `_rail`, `_playerUnitsRow`, `_enemyUnitsRow`, `_drawPile`, `_discardPile`, `_fullDeck`, `_energyText`, `_messageText`, `_turnButton`, `_turnButtonLabel`, `_resetButton`, `_selection`, `_emptyClickCatcher`, `_dimClickCatcher` (기존 `_cancelButton`/`_dimLayer`는 삭제 — 딤은 이제 `CardSelectionController` 소유)

- [ ] **Step 1: BattleScreenController 전체 교체**

`Assets/Unity/BattleScreenController.cs` 전체를 다음으로 교체:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;
using FateWeaver.Simulation.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>Battle screen over DeckCombatSession (visual revamp phase 2): stage units with per-unit
    /// HP bars, the scrollable execution rail, a curved hand fan, three pile viewers, and a single
    /// resolve/next turn button. Input follows spec §6's selection flow — hover to read, click to
    /// select, execution only on the second explicit input — with the visuals orchestrated by
    /// CardSelectionController. UI only — logic stays in the session.</summary>
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
        [SerializeField] private CardSelectionController _selection;
        [SerializeField] private Button _emptyClickCatcher;
        [SerializeField] private Button _dimClickCatcher;

        private const int PlayerHp = 30;
        private const int FateEnergyPerTurn = 3;
        private const int HandSize = 5;
        private const int Seed = 1;

        private static readonly Color PlayerUnitTint = new Color(0.25f, 0.4f, 0.55f, 1f);
        private static readonly Color EnemyUnitTint = new Color(0.55f, 0.25f, 0.25f, 1f);

        private DeckCombatSession _session;
        private UnitView _playerUnit;
        private readonly List<UnitView> _enemyUnits = new List<UnitView>();
        private readonly List<int> _enemyMaxHp = new List<int>();
        private readonly Dictionary<string, Sprite> _artById = new Dictionary<string, Sprite>();

        private void Start()
        {
            _turnButton.onClick.AddListener(OnTurnButton);
            _resetButton.onClick.AddListener(StartSession);
            _emptyClickCatcher.onClick.AddListener(OnEmptyClicked);
            _dimClickCatcher.onClick.AddListener(OnEmptyClicked);
            _selection.Initialize(ApplyCommand);
            _rail.SetRailClicked(() => _selection.OnRailAreaClicked());
            StartSession();
        }

        private void StartSession()
        {
            _selection.CancelSelection();
            var specs = _deck != null ? _deck.ToSpecs() : StarterDeckSpecs.Build();
            var deckDefs = specs.Select(CardSpecMapper.ToDefinition).ToList();
            var enemies = new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) };
            _session = new DeckCombatSession(
                deckDefs, PlayerHp, enemies, GoblinDeck.Policy(Seed), FateEnergyPerTurn, HandSize, Seed);
            BuildArtLookup();
            SpawnUnits();
            BindPiles();
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
            // 뽑을 덱은 실제 순서가 스포일러라 이름순으로 보여준다.
            _drawPile.Bind(() => Presentations(_session.DrawPile)
                .OrderBy(p => p.DisplayName, StringComparer.Ordinal).ToList());
            _discardPile.Bind(() => Presentations(_session.DiscardPile));
            _fullDeck.Bind(() => Presentations(_session.AllDeckCards));
        }

        private IReadOnlyList<CardPresentation> Presentations(IReadOnlyList<CardDefinition> cards)
            => cards.Select(c => CardPresentation.FromDefinition(c, ArtFor)).ToList();

        // --- input (spec §6 selection flow — CardSelectionController drives the visuals) ---

        private void OnHandClicked(int handIndex)
        {
            if (_session == null) return;
            if (_session.CurrentTurnResolved)
            {
                SetMessage("이미 턴을 해석했습니다. '다음 턴'을 누르세요.");
                return;
            }

            var def = _session.Hand[handIndex];
            if (def.EnergyCost > _session.FateEnergy)
            {
                SetMessage("운명력이 부족합니다.");
                return;
            }

            int required = CardTargetRules.RequiredTargets(def);
            if (_session.CurrentOrder.Count < required)
            {
                SetMessage("대상으로 삼을 카드가 레일에 부족합니다.");
                return;
            }

            _selection.BeginSelection(handIndex, required, CardPresentation.FromDefinition(def, ArtFor));
            var name = PlaytestKoreanText.CardName(def.Id, def.Name);
            SetMessage(required == 0 ? name + " — 레일을 클릭해 배치하세요."
                : required == 1 ? name + " — 대상을 클릭하세요."
                : name + " — 대상 " + required + "개를 클릭하세요.");
        }

        private void OnZoneClicked(int zoneIndex)
        {
            if (_session == null || _session.CurrentTurnResolved) return;
            var order = _session.CurrentOrder;
            if (zoneIndex < 0 || zoneIndex >= order.Count) return;
            _selection.OnZoneClicked(zoneIndex, CardPresentation.From(order[zoneIndex], ArtFor));
        }

        private void OnEmptyClicked()
        {
            if (_selection.SelectionActive)
            {
                _selection.CancelSelection();
                SetMessage("선택 취소.");
            }
        }

        private void ApplyCommand(SelectionCommand command)
        {
            if (command.PlayExecution)
            {
                var def = _session.Hand[command.HandIndex];
                SetMessage(_session.PlayExecutionCard(command.HandIndex)
                    ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
                    : "운명력이 부족하거나 낼 수 없습니다.");
            }
            else if (command.PlayIntervention)
            {
                bool ok = _session.PlayInterventionCard(command.HandIndex, command.TargetA, command.TargetB);
                SetMessage(ok ? "개입 카드 적용." : "대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
            }

            RefreshAll();
        }

        private void OnTurnButton()
        {
            if (_session == null || _session.IsComplete) return;
            _selection.CancelSelection();

            if (!_session.CurrentTurnResolved)
            {
                _session.ResolveTurn();
                SetMessage(_session.IsComplete
                    ? "전투 결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome)
                    : "턴 해석 완료.");
            }
            else if (_session.BeginNextTurn())
            {
                SetMessage((_session.TurnIndex + 1) + "턴 준비 완료.");
            }

            RefreshAll();
        }

        // --- art lookup (GUID-backed CardAsset.Art first, Resources fallback) ---

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
            RefreshUnits();
            RefreshHudTexts();
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
            _messageText.text = message;
        }
    }
}
```

- [ ] **Step 2: 죽은 메서드 제거**

(a) `Assets/Unity/HandFanView.cs`에서 `SetSelection(int index, CardView.SelectionKind kind)` 메서드 전체(주석 포함) 삭제.
(b) `Assets/Unity/ExecutionRailView.cs`에서 `SetSelection(int index, CardView.SelectionKind kind)` 메서드 전체 삭제 (`RailCardView.SetSelection`은 유지 — `SetPickedTargets`가 사용).

- [ ] **Step 3: 자기 검토 후 커밋**

리포에서 `SetSelection(` 잔여 호출자가 없는지 grep으로 확인 (`RailCardView.SetSelection`과 `CardView.SetSelection` 정의/내부 사용만 남아야 함):

Run: `grep -rn "\.SetSelection(" Assets/Unity --include="*.cs"`
Expected: `ExecutionRailView.cs`의 `_views[i].SetSelection(...)` (SetPickedTargets 내부)와 `RailCardView.cs`/`CardView.cs` 내부 사용만.

```bash
git add Assets/Unity/BattleScreenController.cs Assets/Unity/HandFanView.cs Assets/Unity/ExecutionRailView.cs
git commit -m "feat(ui): route battle screen input through the selection flow"
```

---

### Task 9: BattleSceneBuilder 갱신 — 확인 버튼 / 클릭 캐처 / 선택 컨트롤러 배선

**선행 조건: 워킹 트리의 `CreateUiCamera` 미커밋 변경이 커밋되어 있어야 한다.** `git status`로 `Assets/Unity/Editor/BattleSceneBuilder.cs`가 깨끗한지 확인하고, 아니면 컨트롤러에게 보고(BLOCKED)한다.

**Files:**
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`

**Interfaces:**
- Consumes: Task 7/8의 [SerializeField] 필드명 계약 (BattleScreenController 17개, CardSelectionController 6개)
- Produces: `Fate Weaver ▸ Build Battle Scene` 재실행 시 새 배선의 씬. 1단계 Minor #1(InputActions 누락 시 경고)도 이 태스크가 흡수.

- [ ] **Step 1: 구 실행 취소 버튼 제거 + 딤 클릭 캐처 추가**

`Build()` 안에서 다음 두 줄을 삭제:

```csharp
            var cancelButton = MakeButton(dimLayer, "CancelButton", "실행 취소", 20f, out _);
            Place((RectTransform)cancelButton.transform, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(150f, 48f));
```

그리고 `BattleUiKit.Stretch(dimImage.rectTransform);` 바로 아래에 추가:

```csharp
            var dimClickCatcher = dimImage.gameObject.AddComponent<Button>();
            dimClickCatcher.transition = Selectable.Transition.None;
```

- [ ] **Step 2: 빈 곳 클릭 캐처 추가**

`background.raycastTarget = false;` 바로 아래에 추가:

```csharp
            // Full-screen invisible catcher: clicks that hit nothing interactive land here (spec §6 cancel).
            var clickCatcher = BattleUiKit.Image(canvasRect, "ClickCatcher", new Color(0f, 0f, 0f, 0f));
            BattleUiKit.Stretch(clickCatcher.rectTransform);
            var emptyClickCatcher = clickCatcher.gameObject.AddComponent<Button>();
            emptyClickCatcher.transition = Selectable.Transition.None;
```

- [ ] **Step 3: 확인 버튼 추가**

`resetButton` 배치 블록 바로 아래에 추가:

```csharp
            // Bottom-right confirm for satisfied multi-target picks (spec §6); hidden until then.
            var confirmButton = MakeButton(canvasRect, "ConfirmButton", "확인", 22f, out _);
            Place((RectTransform)confirmButton.transform, new Vector2(1f, 0f), new Vector2(-120f, 150f), new Vector2(160f, 52f));
            confirmButton.gameObject.SetActive(false);
```

- [ ] **Step 4: Z-순서에 확인 버튼 삽입**

기존 Z-순서 블록을 다음으로 교체 (확인 버튼이 딤 위에서 클릭 가능해야 한다):

```csharp
            // Z-order: the dim covers everything except the rail (selection candidates), the confirm
            // button, and the message line; popups/hover preview stay on the very top.
            dimLayer.SetAsLastSibling();
            railRect.SetAsLastSibling();
            ((RectTransform)confirmButton.transform).SetAsLastSibling();
            ((RectTransform)message.transform).SetAsLastSibling();
            overlay.SetAsLastSibling();
```

- [ ] **Step 5: InputActions 누락 경고 (1단계 Minor #1)**

`if (actions != null) { uiModule.actionsAsset = actions; }` 블록을 다음으로 교체:

```csharp
            if (actions != null)
            {
                uiModule.actionsAsset = actions;
            }
            else
            {
                Debug.LogWarning("BattleSceneBuilder: UIInputActions asset missing at " + InputActionsPath
                    + " — UI input will be dead until an actions asset is assigned.");
            }
```

- [ ] **Step 6: CardSelectionController 생성 + 배선, 컨트롤러 배선 갱신**

`var controllerGo = new GameObject("BattleScreenController");` 바로 위에 추가:

```csharp
            var selectionGo = new GameObject("CardSelectionController");
            var selection = selectionGo.AddComponent<CardSelectionController>();
            var selectionSo = new SerializedObject(selection);
            selectionSo.FindProperty("_hand").objectReferenceValue = hand;
            selectionSo.FindProperty("_rail").objectReferenceValue = rail;
            selectionSo.FindProperty("_dimLayer").objectReferenceValue = dimLayer.gameObject;
            selectionSo.FindProperty("_confirmButton").objectReferenceValue = confirmButton;
            selectionSo.FindProperty("_overlay").objectReferenceValue = overlay;
            selectionSo.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
            selectionSo.ApplyModifiedPropertiesWithoutUndo();
```

그리고 컨트롤러 배선 블록에서 다음 두 줄을 삭제:

```csharp
            so.FindProperty("_cancelButton").objectReferenceValue = cancelButton;
            so.FindProperty("_dimLayer").objectReferenceValue = dimLayer.gameObject;
```

그 자리에 추가:

```csharp
            so.FindProperty("_selection").objectReferenceValue = selection;
            so.FindProperty("_emptyClickCatcher").objectReferenceValue = emptyClickCatcher;
            so.FindProperty("_dimClickCatcher").objectReferenceValue = dimClickCatcher;
```

- [ ] **Step 7: 자기 검토 후 커밋**

FindProperty 문자열 23개(컨트롤러 17 + 선택 6)가 Task 7/8의 필드명과 정확히 일치하는지 대조. `CreateUiCamera` 호출(사용자 커밋분)이 보존됐는지 확인.

```bash
git add Assets/Unity/Editor/BattleSceneBuilder.cs
git commit -m "feat(editor): rebuild battle scene wiring for selection input"
```

---

### Task 10: 씬 재생성 + Unity Play 검증 + 문서/메타 커밋

이 태스크는 Unity 에디터가 필요하다 (사용자 수행 단계 포함).

**Files:**
- 재생성: `Assets/Scenes/FateWeaverBattle.unity` (+ 신규 `.cs` `.meta`)
- Modify: `Assets/Unity/PLAYTEST.md`

- [ ] **Step 1: 컴파일 + 씬 재생성**

Unity 에디터 포커스 → Console 에러 0 확인 → `Fate Weaver ▸ Build Battle Scene` 실행.
Expected: `BattleSceneBuilder: saved Assets/Scenes/FateWeaverBattle.unity`, 에러 0.

- [ ] **Step 2: Play 수동 체크리스트**

1. **호버**: 손패 카드에 호버 → 직립·확대·이웃 위로; 이탈 → 원래 포즈 복귀
2. **실행 카드**: 클릭 → 즉시 실행되지 않음. 원본 반투명 + 확대 사본이 마우스 추적(회전 없음) + 레일에 약한 호박색 하이라이트 → 레일(배경 또는 카드) 클릭 = 배치·운명력 차감 → 사본/하이라이트 해제. **빈 곳 클릭 = 취소, 운명력 그대로**
3. **단일 대상 개입**: 클릭 → 카드 확대 고정 + 클릭 지점에서 화살표가 커서 추적 → 레일 카드 클릭 = 즉시 적용 / 빈 곳 = 취소
4. **교환 개입**: 클릭 → 레일 제외 딤 → 대상 1 클릭(화면 가운데 잠시 강조 + 타일 외곽선) → 같은 대상 재클릭 무시 → 대상 2 클릭 → 우측 하단 `확인` 버튼 등장 → 확인 = 적용. 딤 영역 클릭 = 취소(선택 초기화)
5. **좌측 실행 취소 버튼이 화면에 없음**
6. 운명력 부족 카드 클릭 → 메시지만, 선택 진입 없음; 레일이 빈 턴에 개입 클릭 → "대상 부족" 메시지
7. 선택 중 `턴 실행` 클릭 → 선택 자동 취소 후 해석; `초기화`/덱 팝업/라벨 전환 회귀 정상
8. Console 에러 0

문제 발견 시 해당 태스크로 돌아가 수정 후 재실행.

- [ ] **Step 3: PLAYTEST.md 갱신**

`Assets/Unity/PLAYTEST.md`의 "### 전투 화면 (시각 개편 1단계)" 섹션에서 3–4번 항목을 다음으로 교체:

```markdown
3. 카드 입력(§6): 호버 = 확대 보기, 클릭 = 선택. 실행 카드는 레일 클릭으로 배치,
   단일 대상 개입은 화살표로 대상 클릭, 교환은 대상 2개 선택 후 우하단 `확인`.
   빈 곳(또는 딤) 클릭 = 취소. 어떤 카드도 첫 클릭으로 실행되지 않는다.
```

(섹션 제목의 "1단계"는 "1–2단계"로 수정.)

- [ ] **Step 4: 헤드리스 최종 회귀**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 통과 (Failed 0).

- [ ] **Step 5: 씬/메타/문서 커밋**

```bash
git add Assets/Scenes/FateWeaverBattle.unity Assets/Unity/PLAYTEST.md
git add Assets/Unity/CardSelectionController.cs.meta Assets/Unity/HandCardHoverEffect.cs.meta Assets/Unity/TargetingArrowView.cs.meta Assets/Core/Simulation/Presentation/CardTargetRules.cs.meta Assets/Core/Simulation/Presentation/CardSelectionMachine.cs.meta Assets/Core/Tests/EditMode/CardTargetRulesTests.cs.meta Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs.meta
git commit -m "chore(unity): regenerate battle scene and playtest docs for selection input"
```

(`git status`로 누락 `.meta`가 없는지 확인 후 커밋. `CardView.cs` 등 사용자 실험 파일은 절대 포함하지 말 것.)
