# Unified Explicit Target Selection UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 모든 명시적 대상 카드가 선택한 손패 카드에서 시작하는 화살표, 공통 후보 강조, 단일 대상 즉시 확정, 다중 대상 확인 규칙을 사용하게 한다.

**Architecture:** `FateWeaver.Simulation.Presentation`의 순수 선택 상태가 타입 안전 대상 식별자와 선택 목록을 관리한다. Unity의 `CardSelectionController`가 아군·적·실행 순서 대상의 표현과 완료 흐름을 통합하고, `BattleScreenController`는 카드별 대상 요구사항과 세션 API 변환만 담당한다.

**Tech Stack:** C#, .NET/NUnit headless tests, Unity 6000.5.2f1, uGUI, Unity Input System, Unity EditMode tests

## Global Constraints

- `Assets/Core`와 `Assets/Core/Simulation`은 `UnityEngine`을 참조하지 않는다.
- 새 외부 패키지나 에셋을 추가하지 않는다.
- 모든 무작위 규칙과 기존 전투 결정론을 변경하지 않는다.
- 대상이 없는 카드는 기존 마우스 추적 카드와 실행 순서 영역 클릭 방식을 유지한다.
- 대상이 1개면 확인 버튼을 표시하지 않고 대상 클릭 즉시 완료한다.
- 대상이 2개 이상이면 필요한 대상을 모두 고른 뒤에만 확인 버튼을 표시한다.
- 다중 선택 중 화살표 시작점은 항상 선택한 손패 카드의 화면상 중심이다.
- 구현 전부터 존재하는 `KoreanTMP.asset` 변경은 사용자 변경으로 간주해 커밋하지 않는다.
- 현재 미커밋된 씬과 타겟 화살표 프리팹은 마지막 태스크에서 빌더를 재실행한 결과만 검증 후 커밋한다.

## File Map

- Create `Assets/Core/Simulation/Presentation/SelectionTargetRef.cs`: 순수 대상 식별자.
- Modify `Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`: 대상 종류·개수·검증 실패 복구.
- Modify `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`: 순수 상태 전이 테스트.
- Modify `Assets/Unity/TargetingArrowView.cs`, `HandFanView.cs`: 동적 화살표 시작점.
- Modify `Assets/Unity/UnitView.cs`, `RailCardView.cs`, `ExecutionRailView.cs`: 후보 딤과 번호 배지.
- Create `Assets/Tests/UnityEditMode/TargetSelectionVisualTests.cs`: 공통 표현 테스트.
- Modify `Assets/Unity/CardSelectionController.cs`: 모든 명시적 대상의 단일 조정기.
- Create `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`: 확인 규칙 테스트.
- Modify `Assets/Unity/BattleScreenController.cs`: 별도 아군 모드 제거와 결과 변환.
- Modify `Assets/Unity/Editor/BattleSceneBuilder.cs`, `Assets/Unity/PLAYTEST.md`.
- Regenerate `UnitView.prefab`, `RailCardView.prefab`, `TargetingArrowView.prefab`, `FateWeaverBattle.unity`.

---

### Task 1: Generalize the pure selection state

**Files:**
- Create: `Assets/Core/Simulation/Presentation/SelectionTargetRef.cs`
- Modify: `Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`
- Test: `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`

**Interfaces:**
- Produces: `SelectionTargetKind`, `SelectionTargetRef`, `SelectionResult`.
- Produces: `SelectCard(int, SelectionTargetKind, int)`, `ClickTarget(SelectionTargetRef)`, `CommitSucceeded()`, `RejectCompletion(IReadOnlyCollection<SelectionTargetRef>)`.
- Consumed by: Tasks 2–4.

- [ ] **Step 1: Write typed-target tests**

Replace rail-index assertions with these cases while retaining zero-target, cancel, wrong-phase, and reselect coverage:

```csharp
[Test]
public void Single_party_target_completes_without_confirmation()
{
    var machine = new CardSelectionMachine();
    machine.SelectCard(1, SelectionTargetKind.PartyMember, 1);
    var target = SelectionTargetRef.PartyMember("member-b");

    var result = machine.ClickTarget(target);

    Assert.IsTrue(result.IsComplete);
    Assert.AreEqual(1, result.HandIndex);
    CollectionAssert.AreEqual(new[] { target }, result.Targets);
    Assert.AreEqual(SelectionPhase.PickSingleTarget, machine.Phase);
}

[Test]
public void Target_from_wrong_domain_is_ignored()
{
    var machine = new CardSelectionMachine();
    machine.SelectCard(1, SelectionTargetKind.PartyMember, 1);

    var result = machine.ClickTarget(SelectionTargetRef.ExecutionCard(0));

    Assert.IsFalse(result.IsComplete);
    Assert.AreEqual(0, machine.PickedTargets.Count);
}

[Test]
public void Multiple_targets_require_explicit_confirmation()
{
    var machine = new CardSelectionMachine();
    var first = SelectionTargetRef.ExecutionCard(1);
    var second = SelectionTargetRef.ExecutionCard(3);
    machine.SelectCard(4, SelectionTargetKind.ExecutionCard, 2);

    Assert.IsFalse(machine.ClickTarget(first).IsComplete);
    Assert.IsFalse(machine.ClickTarget(first).IsComplete);
    Assert.IsFalse(machine.ClickTarget(second).IsComplete);
    Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);
    CollectionAssert.AreEqual(new[] { first, second }, machine.PickedTargets);

    var result = machine.Confirm();
    Assert.IsTrue(result.IsComplete);
    CollectionAssert.AreEqual(new[] { first, second }, result.Targets);
}

[Test]
public void Rejected_completion_removes_invalid_picks_and_resumes_selection()
{
    var machine = new CardSelectionMachine();
    var first = SelectionTargetRef.ExecutionCard(1);
    var second = SelectionTargetRef.ExecutionCard(3);
    machine.SelectCard(4, SelectionTargetKind.ExecutionCard, 2);
    machine.ClickTarget(first);
    machine.ClickTarget(second);
    machine.Confirm();

    machine.RejectCompletion(new[] { second, SelectionTargetRef.ExecutionCard(5) });

    Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
    CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);
}

[Test]
public void Successful_completion_is_the_only_operation_that_returns_to_idle()
{
    var machine = new CardSelectionMachine();
    machine.SelectCard(2, SelectionTargetKind.PartyMember, 1);
    machine.ClickTarget(SelectionTargetRef.PartyMember("member-a"));

    machine.CommitSucceeded();

    Assert.AreEqual(SelectionPhase.Idle, machine.Phase);
    Assert.AreEqual(0, machine.PickedTargets.Count);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~CardSelectionMachineTests
```

Expected: build fails because the new target/result types and signatures do not exist.

- [ ] **Step 3: Add the typed target reference**

Create `SelectionTargetRef.cs`:

```csharp
using System;

namespace FateWeaver.Simulation.Presentation
{
    public enum SelectionTargetKind { None, ExecutionCard, PartyMember, Enemy }

    public readonly struct SelectionTargetRef : IEquatable<SelectionTargetRef>
    {
        public SelectionTargetKind Kind { get; }
        public int Index { get; }
        public string EntityId { get; }

        private SelectionTargetRef(SelectionTargetKind kind, int index, string entityId)
        {
            Kind = kind;
            Index = index;
            EntityId = entityId;
        }

        public static SelectionTargetRef ExecutionCard(int index)
            => new SelectionTargetRef(SelectionTargetKind.ExecutionCard, index, null);
        public static SelectionTargetRef PartyMember(string id)
            => new SelectionTargetRef(SelectionTargetKind.PartyMember, -1, id);
        public static SelectionTargetRef Enemy(string id)
            => new SelectionTargetRef(SelectionTargetKind.Enemy, -1, id);

        public bool Equals(SelectionTargetRef other)
            => Kind == other.Kind && Index == other.Index
                && string.Equals(EntityId, other.EntityId, StringComparison.Ordinal);
        public override bool Equals(object obj)
            => obj is SelectionTargetRef other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((int)Kind * 397) ^ Index;
                return (hash * 397) ^ (EntityId == null ? 0 : EntityId.GetHashCode());
            }
        }
    }
}
```

- [ ] **Step 4: Generalize `CardSelectionMachine`**

Replace `SelectionCommand` with this immutable copied result:

```csharp
public readonly struct SelectionResult
{
    private readonly SelectionTargetRef[] _targets;

    public bool IsComplete { get; }
    public int HandIndex { get; }
    public IReadOnlyList<SelectionTargetRef> Targets
        => _targets ?? Array.Empty<SelectionTargetRef>();

    private SelectionResult(
        bool isComplete, int handIndex, SelectionTargetRef[] targets)
    {
        IsComplete = isComplete;
        HandIndex = handIndex;
        _targets = targets;
    }

    public static SelectionResult None
        => new SelectionResult(false, -1, Array.Empty<SelectionTargetRef>());

    internal static SelectionResult Complete(
        int handIndex, IReadOnlyCollection<SelectionTargetRef> targets)
    {
        var copy = new SelectionTargetRef[targets.Count];
        int index = 0;
        foreach (var target in targets)
        {
            copy[index++] = target;
        }
        return new SelectionResult(true, handIndex, copy);
    }
}
```

Implement these machine methods:

```csharp
public void SelectCard(int handIndex, SelectionTargetKind targetKind, int requiredTargets);
public SelectionResult ClickApplyArea();
public SelectionResult ClickTarget(SelectionTargetRef target);
public SelectionResult Confirm();
public void CommitSucceeded();
public void RejectCompletion(IReadOnlyCollection<SelectionTargetRef> validTargets);
public void Cancel();
```

Rules: zero targets require `None`; one target enters `PickSingleTarget`; two or more enter `PickMultipleTargets`; mismatched kinds and duplicates do nothing; single clicks return a result immediately; multiple clicks require `Confirm`; results copy `_picked` into a new array; only `CommitSucceeded` returns to idle. A rejected single-target completion clears its pending pick so the player can click again. A rejected multi-target completion retains only valid picks and restores `PickMultipleTargets` unless the requirement is still satisfied.

- [ ] **Step 5: Run focused and full headless tests**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter FullyQualifiedName~CardSelectionMachineTests
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
```

Expected: both commands pass with zero failed tests.

- [ ] **Step 6: Commit**

```bash
git add Assets/Core/Simulation/Presentation/SelectionTargetRef.cs Assets/Core/Simulation/Presentation/CardSelectionMachine.cs Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs
git commit -m "refactor(input-core): generalize explicit target selection"
```

---

### Task 2: Build common target-selection visuals

**Files:**
- Modify: `Assets/Unity/TargetingArrowView.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Modify: `Assets/Unity/UnitView.cs`
- Modify: `Assets/Unity/RailCardView.cs`
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Test: `Assets/Tests/UnityEditMode/TargetSelectionVisualTests.cs`

**Interfaces:**
- Consumes: `SelectionTargetRef` from Task 1.
- Produces: `TargetingArrowView.Show(Vector2, Vector2)` and `Track(Vector2, Vector2)`.
- Produces: `HandFanView.TryGetCardScreenPoint(int, out Vector2)` and `SetTargetSelection(int, bool)`.
- Produces: `UnitView.SetTargetSelection(bool, bool, int)`.
- Produces: `ExecutionRailView.SetTargetSelection(bool, IReadOnlyCollection<SelectionTargetRef>, IReadOnlyList<SelectionTargetRef>)`.
- Consumed by: Task 3.

- [ ] **Step 1: Write visual-state tests**

Create `TargetSelectionVisualTests.cs`. Include `System.Reflection`, `TMPro`, NUnit, and Unity namespaces, then add:

```csharp
[Test]
public void Arrow_tracks_a_new_start_point_each_frame()
{
    var overlay = new GameObject("Overlay", typeof(RectTransform));
    try
    {
        ((RectTransform)overlay.transform).sizeDelta = new Vector2(1280f, 720f);
        var arrow = TargetingArrowView.EditorCreate((RectTransform)overlay.transform);
        arrow.Show(new Vector2(100f, 100f), new Vector2(300f, 100f));
        arrow.Track(new Vector2(150f, 100f), new Vector2(350f, 100f));

        var shaft = (RectTransform)typeof(TargetingArrowView)
            .GetField("_shaft", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(arrow);
        Assert.AreEqual(200f, shaft.sizeDelta.x, 0.01f);
    }
    finally
    {
        Object.DestroyImmediate(overlay);
    }
}

[Test]
public void Unit_target_state_shows_candidate_and_selection_order()
{
    var root = new GameObject("Root", typeof(RectTransform));
    try
    {
        var view = UnitView.EditorCreate(
            (RectTransform)root.transform, new Vector2(180f, 250f));
        view.SetTargetSelection(true, true, 2);

        var badge = (GameObject)typeof(UnitView)
            .GetField("_targetOrderBadge", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(view);
        Assert.IsTrue(badge.activeSelf);
        Assert.AreEqual("2", badge.GetComponentInChildren<TMP_Text>().text);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

Add a rail test that creates two `RailCardView` instances, supplies two `ExecutionCard` candidates, picks the first, and verifies badge `1` on the first while a noncandidate third card has `_targetDim` active.

- [ ] **Step 2: Run Unity tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.TargetSelectionVisualTests -testResults /private/tmp/target-selection-visual-tests.xml -quit
```

Expected: compilation fails because the new methods and badge fields do not exist. If licensing blocks batch mode, run the same class in Unity Test Runner and record the result.

- [ ] **Step 3: Make the arrow origin dynamic**

Replace the stored start-point flow with:

```csharp
public void Show(Vector2 startScreen, Vector2 currentScreen)
{
    gameObject.SetActive(true);
    Track(startScreen, currentScreen);
}

public void Track(Vector2 startScreen, Vector2 currentScreen)
{
    var start = ToLocal(startScreen);
    var current = ToLocal(currentScreen);
    var delta = current - start;
    float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
    _shaft.anchoredPosition = start;
    _shaft.sizeDelta = new Vector2(delta.magnitude, 6f);
    _shaft.localRotation = Quaternion.Euler(0f, 0f, angle);
    _head.anchoredPosition = current;
    _head.localRotation = Quaternion.Euler(0f, 0f, angle + 45f);
}
```

- [ ] **Step 4: Expose the hand-card origin and hand selection dimming**

Add to `HandFanView`:

```csharp
public bool TryGetCardScreenPoint(int index, out Vector2 screenPoint)
{
    if (index < 0 || index >= _views.Count)
    {
        screenPoint = Vector2.zero;
        return false;
    }

    screenPoint = RectTransformUtility.WorldToScreenPoint(
        null, _views[index].transform.position);
    return true;
}

public void SetTargetSelection(int selectedIndex, bool active)
{
    for (int i = 0; i < _views.Count; i++)
    {
        _groups[i].alpha = !active || i == selectedIndex ? 1f : 0.35f;
        _views[i].SetInteractable(!active);
        _views[i].SetSelection(active && i == selectedIndex
            ? CardView.SelectionKind.Primary
            : CardView.SelectionKind.None);
    }
}
```

Keep `SetGhost` for targetless placement.

- [ ] **Step 5: Add target state to unit and rail-card views**

Add serialized `_targetDim`, `_targetOrderBadge`, and `_targetOrderText` fields to `UnitView` and `RailCardView`. Each `EditorCreate` creates a non-raycast black dim image and a top-right amber number badge with centered TMP text. Implement on `UnitView`:

```csharp
public void SetTargetSelection(bool active, bool candidate, int selectionOrder)
{
    _targetDim.SetActive(active && !candidate);
    _targetHighlight.SetActive(active && candidate);
    _targetButton.interactable = active && candidate;
    _targetOrderBadge.SetActive(selectionOrder > 0);
    _targetOrderText.text = selectionOrder > 0
        ? selectionOrder.ToString()
        : string.Empty;
}
```

The `RailCardView` method uses `_selectionOutline` instead of `_targetHighlight` and leaves button interaction to `ExecutionRailView`. Preserve `UnitView.SetTargetable(bool)` as a compatibility wrapper with selection order zero.

- [ ] **Step 6: Project candidates and picks onto rail cards**

Implement `ExecutionRailView.SetTargetSelection`. For each view, create `SelectionTargetRef.ExecutionCard(i)`, compute candidate membership, compute the one-based position in `pickedTargets`, call the rail card's target-state method, and enable only candidate buttons while selection is active. Passing `active: false` clears dim, outline, badge, and restores normal input behavior.

- [ ] **Step 7: Run Unity tests**

Run the Task 2 Unity command again.

Expected: `TargetSelectionVisualTests` passes with zero failures.

- [ ] **Step 8: Commit**

```bash
git add Assets/Core/Simulation/Presentation/SelectionTargetRef.cs.meta Assets/Unity/TargetingArrowView.cs Assets/Unity/HandFanView.cs Assets/Unity/UnitView.cs Assets/Unity/RailCardView.cs Assets/Unity/ExecutionRailView.cs Assets/Tests/UnityEditMode/TargetSelectionVisualTests.cs Assets/Tests/UnityEditMode/TargetSelectionVisualTests.cs.meta
git commit -m "feat(ui): unify explicit target visuals"
```

---

### Task 3: Make `CardSelectionController` the single selection coordinator

**Files:**
- Modify: `Assets/Unity/CardSelectionController.cs`
- Test: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 interfaces.
- Produces: `Initialize(Func<SelectionResult, bool>, Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>>, Action)`.
- Produces: `RegisterUnitTarget(SelectionTargetRef, UnitView)`, `ClearUnitTargets()`.
- Produces: `BeginPlacement(int, CardPresentation)`, `BeginTargetSelection(int, SelectionTargetKind, int, IReadOnlyList<SelectionTargetRef>)`, `OnTargetClicked(SelectionTargetRef, CardPresentation)`.
- Consumed by: Task 4.

- [ ] **Step 1: Write controller behavior tests**

Create an inactive test root, add a minimal hand, rail, dim object, confirm button, overlay, and `TargetingArrowView`, assign private fields with reflection, then activate the root so `Awake` sees valid references. Use a callback that appends results and returns `true`:

```csharp
[Test]
public void Single_target_shows_arrow_never_shows_confirm_and_dispatches_on_click()
{
    var target = SelectionTargetRef.PartyMember("member-a");
    _controller.BeginTargetSelection(
        0, SelectionTargetKind.PartyMember, 1, new[] { target });

    Assert.IsTrue(_arrow.gameObject.activeSelf);
    Assert.IsFalse(_confirmButton.gameObject.activeSelf);

    _controller.OnTargetClicked(target, null);

    Assert.AreEqual(1, _appliedResults.Count);
    Assert.IsFalse(_confirmButton.gameObject.activeSelf);
}

[Test]
public void Multiple_targets_show_confirm_only_after_requirement_is_met()
{
    var first = SelectionTargetRef.ExecutionCard(0);
    var second = SelectionTargetRef.ExecutionCard(1);
    _controller.BeginTargetSelection(
        0, SelectionTargetKind.ExecutionCard, 2, new[] { first, second });

    _controller.OnTargetClicked(first, null);
    Assert.IsFalse(_confirmButton.gameObject.activeSelf);

    _controller.OnTargetClicked(second, null);
    Assert.IsTrue(_confirmButton.gameObject.activeSelf);
    Assert.AreEqual(0, _appliedResults.Count);

    _confirmButton.onClick.Invoke();
    Assert.AreEqual(1, _appliedResults.Count);
}
```

Add a rejection test: the apply callback returns `false`, the current-target provider removes one selected target, and the controller remains active with that target removed from its numbered picks.

- [ ] **Step 2: Run controller tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.CardSelectionControllerTests -testResults /private/tmp/card-selection-controller-tests.xml -quit
```

Expected: compilation fails on the new controller API.

- [ ] **Step 3: Add collaborators and target registries**

Replace `Action<SelectionCommand>` with:

```csharp
private Func<SelectionResult, bool> _tryApply;
private Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>> _currentTargets;
private Action _onApplied;
private readonly HashSet<SelectionTargetRef> _validTargets =
    new HashSet<SelectionTargetRef>();
private readonly Dictionary<SelectionTargetRef, UnitView> _unitTargets =
    new Dictionary<SelectionTargetRef, UnitView>();

public void Initialize(
    Func<SelectionResult, bool> tryApply,
    Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>> currentTargets,
    Action onApplied)
{
    _tryApply = tryApply;
    _currentTargets = currentTargets;
    _onApplied = onApplied;
}

public void RegisterUnitTarget(SelectionTargetRef target, UnitView view)
{
    _unitTargets[target] = view;
}

public void ClearUnitTargets()
{
    _unitTargets.Clear();
}
```

- [ ] **Step 4: Implement unified begin and target-click flows**

Implement the public API with these exact state rules:

- `BeginPlacement` selects `SelectionTargetKind.None`, shows the floating card, ghosts the chosen hand card, and leaves arrow/dim/confirm hidden.
- `BeginTargetSelection` throws `ArgumentException` for `None` or `requiredTargets < 1`, copies candidates into `_validTargets`, holds the chosen card, enables the dim, refreshes hand/rail/unit target states, and shows the arrow from the selected hand center to the mouse.
- `OnTargetClicked` ignores idle, wrong-kind, and noncandidate targets. Otherwise it calls the pure machine, refreshes numbered badges, plays rail emphasis when a `CardPresentation` is supplied, and dispatches only a complete result.
- The confirm button is active only when `RequiredTargets >= 2` and `Phase == ReadyToConfirm`.
- `OnRailAreaClicked` commits only targetless placement.

Use these helper signatures so the implementation stays bounded:

```csharp
private void TryDispatch(SelectionResult result);
private void ReloadValidTargetsAfterRejection();
private void RefreshTargetVisuals();
private int SelectionOrder(SelectionTargetRef target);
private Vector2 SelectedCardScreen();
private void EndSelectionVisuals();
```

- [ ] **Step 5: Keep the arrow anchored to the selected card in every target phase**

Replace the target branch in `Update` with:

```csharp
if (_machine.Phase == SelectionPhase.PickSingleTarget
    || _machine.Phase == SelectionPhase.PickMultipleTargets
    || _machine.Phase == SelectionPhase.ReadyToConfirm)
{
    _arrow.Track(SelectedCardScreen(), MouseScreen());
}
```

`TryDispatch` calls `_tryApply`. On success it calls `CommitSucceeded`, then `EndSelectionVisuals`, then `_onApplied`, in that order. On failure it reloads candidates from `_currentTargets`, calls `RejectCompletion`, refreshes target visuals, and cancels when the remaining candidates are fewer than `RequiredTargets`.

- [ ] **Step 6: Unify cleanup**

`CancelSelection`, successful completion, and forced cancellation all use `EndSelectionVisuals`. It must reset hand target mode/held/ghost state, rail candidate state, every registered unit target state, dim, confirm, arrow, floating card, emphasis card/coroutine, `_validTargets`, and `_visualHandIndex`.

- [ ] **Step 7: Run all Unity EditMode tests**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testResults /private/tmp/unified-target-editmode-tests.xml -quit
```

Expected: all Unity EditMode tests pass with zero failures.

- [ ] **Step 8: Commit**

```bash
git add Assets/Unity/CardSelectionController.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs.meta
git commit -m "refactor(ui): centralize explicit target selection"
```

---

### Task 4: Integrate battle input, regenerate prefabs, and verify the scene

**Files:**
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/Unity/PLAYTEST.md`
- Test: `Assets/Tests/UnityEditMode/BattleScreenUnitIdentityTests.cs`
- Test: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`
- Regenerate: `Assets/Unity/Prefabs/UnitView.prefab`
- Regenerate: `Assets/Unity/Prefabs/RailCardView.prefab`
- Regenerate: `Assets/Unity/Prefabs/TargetingArrowView.prefab`
- Regenerate: `Assets/Scenes/FateWeaverBattle.unity`

**Interfaces:**
- Consumes: all previous tasks.
- Produces: one battle input flow for placement, party-member targets, enemy targets, and execution-card targets.

- [ ] **Step 1: Add integration coverage before removing the old ally mode**

Extend `BattleScreenUnitIdentityTests.cs` with a test that registers party unit views through `CardSelectionController`, begins a one-target `PartyMember` selection, invokes the target button, and asserts that member id reaches a completed `SelectionResult`. Extend `ExecutionRailInputTests.cs` with the same assertion for an `ExecutionCard` target. Both tests assert the confirm button stays inactive for one target.

- [ ] **Step 2: Run focused integration tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.BattleScreenUnitIdentityTests -testResults /private/tmp/battle-target-integration-tests.xml -quit
```

Expected: the new test fails while `BattleScreenController` still owns `AllyTargeting` separately.

- [ ] **Step 3: Remove the separate ally input mode**

Delete `InputMode`, `_inputMode`, `_armedAllyTargetHandIndex`, `OnAllyUnitClicked`, and `ClearAllyTargeting`. Initialize:

```csharp
_selection.Initialize(TryApplySelection, CurrentValidTargets, RefreshAll);
```

Before rebuilding units, call `_selection.ClearUnitTargets()`. Register party members:

```csharp
var target = SelectionTargetRef.PartyMember(member.Id);
view.BindTarget(member.Id, id => _selection.OnTargetClicked(
    SelectionTargetRef.PartyMember(id), null));
_selection.RegisterUnitTarget(target, view);
```

Register enemies with `SelectionTargetRef.Enemy(enemy.Id)` and the same callback shape so future explicit enemy cards use the common path without another input mode.

Remove all remaining mode checks: `OnEmptyClicked` only cancels `_selection`; `OnTurnButton` returns while `_selection.SelectionActive`; `RefreshHudTexts` makes the turn button interactable only when no selection is active and the session is incomplete. `StartSession` cancels the common selection and no longer calls ally cleanup.

- [ ] **Step 4: Route every hand card through one requirement calculation**

After existing turn and energy guards, use:

```csharp
var name = PlaytestKoreanText.CardName(def.Id, def.Name);
if (def.Category == CardCategory.Execution
    && PartyTargetRules.RequiresExplicitAllyTarget(def))
{
    var targets = CurrentValidTargets(SelectionTargetKind.PartyMember);
    _selection.BeginTargetSelection(
        handIndex, SelectionTargetKind.PartyMember, 1, targets);
    SetMessage(name + " — 살아 있는 아군을 선택하세요.");
}
else
{
    int requiredTargets = CardTargetRules.RequiredRailTargets(def);
    if (requiredTargets == 0)
    {
        _selection.BeginPlacement(handIndex, PresentationFor(card));
        SetMessage(name + " — 실행 순서를 클릭해 배치하세요.");
    }
    else
    {
        var targets = CurrentValidTargets(SelectionTargetKind.ExecutionCard);
        if (targets.Count < requiredTargets)
        {
            SetMessage("대상으로 삼을 카드가 실행 순서에 부족합니다.");
            return;
        }

        _selection.BeginTargetSelection(
            handIndex, SelectionTargetKind.ExecutionCard, requiredTargets, targets);
        SetMessage(name + " — 대상 " + requiredTargets + "개를 선택하세요.");
    }
}
```

`CurrentValidTargets` returns living party ids, living enemy ids, or current execution-card indexes according to `SelectionTargetKind`. `OnZoneClicked` forwards `SelectionTargetRef.ExecutionCard(zoneIndex)` and its `CardPresentation` to `_selection.OnTargetClicked`.

- [ ] **Step 5: Translate one selection result into session calls**

Implement:

```csharp
private bool TryApplySelection(SelectionResult result)
```

Re-read the current hand card. For an execution card, pass `null` for zero targets or the single party/enemy target's `EntityId` to `PlayExecutionCard`. For an intervention, require one or two `ExecutionCard` targets and pass their `Index` values to `PlayInterventionCard`. Set the existing Korean success/failure message and return the session call's result without rebuilding views; `CardSelectionController` invokes `RefreshAll` only after cleanup.

`RefreshSelections` uses only `_selection.SelectionActive` to disable piles, reset, and turn input. Hand, rail, unit candidates, arrow, dim, and confirm remain owned by `CardSelectionController`.

- [ ] **Step 6: Update scene-builder ordering and serialized references**

Put the global dim below stage, execution order, hand, message, confirm, and overlay:

```csharp
dimLayer.SetAsLastSibling();
stage.SetAsLastSibling();
railRect.SetAsLastSibling();
handRect.SetAsLastSibling();
((RectTransform)confirmButton.transform).SetAsLastSibling();
((RectTransform)message.transform).SetAsLastSibling();
overlay.SetAsLastSibling();
```

Views above the global dim use their own `_targetDim` to darken noncandidates. Keep the dim click catcher wired to `OnEmptyClicked`. Ensure regenerated prefabs serialize every new badge/dim field and the selection controller retains non-null `_arrow`, `_hand`, `_rail`, `_dimLayer`, and `_confirmButton` references.

- [ ] **Step 7: Update the playtest checklist**

Add these exact cases to `Assets/Unity/PLAYTEST.md`:

1. 대상 없는 실행 카드는 마우스를 따라가고 실행 순서 영역 클릭으로 배치된다.
2. 선택 방어는 손패 카드 중심에서 아군까지 화살표가 표시되며 확인 버튼 없이 즉시 배치된다.
3. 앞당김/미룸은 같은 화살표 규칙으로 실행 순서 카드 하나를 즉시 선택한다.
4. 자리 교환은 화살표 시작점이 손패 카드에 고정되고 두 대상에 1·2 번호가 표시된 뒤 확인 버튼이 나타난다.
5. 빈 영역 취소는 카드와 운명력을 소비하지 않고 모든 선택 표현을 지운다.

- [ ] **Step 8: Run tests and rebuild the scene**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testResults /private/tmp/unified-target-final-editmode-tests.xml -quit
```

Open the worktree in Unity, run `Fate Weaver ▸ Build Battle Scene`, save, and perform all five PLAYTEST cases. Expected: headless and EditMode failures are zero; Console has no compile/runtime errors; no Missing Script or missing serialized field appears.

- [ ] **Step 9: Review generated diffs and commit intended assets only**

```bash
git diff --check
git status --short
git diff -- Assets/Unity/Resources/Fonts/KoreanTMP.asset
```

Keep `KoreanTMP.asset` unstaged. Stage source, tests, PLAYTEST, regenerated scene, and regenerated prefabs:

```bash
git add Assets/Unity/BattleScreenController.cs Assets/Unity/Editor/BattleSceneBuilder.cs Assets/Unity/PLAYTEST.md Assets/Tests/UnityEditMode/BattleScreenUnitIdentityTests.cs Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs Assets/Unity/Prefabs/UnitView.prefab Assets/Unity/Prefabs/RailCardView.prefab Assets/Unity/Prefabs/TargetingArrowView.prefab Assets/Unity/Prefabs/TargetingArrowView.prefab.meta Assets/Scenes/FateWeaverBattle.unity
git commit -m "feat(battle): unify explicit target selection UX"
```

- [ ] **Step 10: Final verification**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
git status --short --branch
git log --oneline -6
```

Expected: all headless tests pass; only the pre-existing `Assets/Unity/Resources/Fonts/KoreanTMP.asset` user change remains unstaged; the four implementation commits appear above the design and plan commits.
