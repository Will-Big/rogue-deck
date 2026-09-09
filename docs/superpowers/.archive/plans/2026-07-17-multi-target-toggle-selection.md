# Multi-Target Toggle Selection Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 다중 대상 선택에서 이미 선택한 대상을 다시 클릭하면 선택을 해제하고, 확인 준비 상태와 Unity 피드백을 즉시 이전 단계로 되돌린다.

**Architecture:** `FateWeaver.Simulation.Presentation.CardSelectionMachine`이 다중 대상 클릭의 추가·해제 토글과 단계 전이를 소유한다. 기존 `CardSelectionController`는 모든 활성 선택 클릭을 코어에 전달하고 `PickedTargets`를 표현에 투영하므로 공개 API나 프리팹 구조를 바꾸지 않고 상태 갱신 결과만 사용한다.

**Tech Stack:** C#, .NET/NUnit headless tests, Unity 6000.5.2f1, uGUI, Unity EditMode tests

## Global Constraints

- 단일 대상은 첫 클릭에서 즉시 완료되는 기존 동작을 유지한다.
- 대상 없는 카드는 기존 마우스 추적 배치 방식을 유지한다.
- 다중 대상의 선택된 대상 재클릭만 선택 해제로 처리한다.
- `ReadyToConfirm`에서 선택된 대상을 해제하면 `PickMultipleTargets`로 돌아가고 확인 버튼을 숨긴다.
- 해제된 대상은 푸른색에서 황금색 후보 테두리로 돌아가며, 남은 선택 대상은 푸른색을 유지한다.
- 선택 해제는 카드, 운명력, 실행 순서 또는 세션 상태를 변경하지 않는다.
- 새 외부 패키지, 프리팹, 직렬화 필드 또는 UI 객체를 추가하지 않는다.
- `Assets/Core`와 `Assets/Core/Simulation`은 `UnityEngine`을 참조하지 않는다.
- 기존 `KoreanTMP.asset`, 씬, 프리팹, 타겟 화살표 생성물은 사용자 변경으로 간주해 커밋하지 않는다.

## File Map

- Modify `Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`: 다중 대상 클릭 토글과 단계 전이.
- Modify `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`: 선택 진행 중·확인 준비 상태의 해제/재선택 테스트.
- Modify `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`: 황금색/푸른색 테두리와 확인 버튼 토글 테스트, `GameObject` 직렬화 계약에 맞는 하이라이트 헬퍼.
- Modify `Assets/Unity/PLAYTEST.md`: 다중 대상 재클릭 취소 수동 검증.

---

### Task 1: Toggle selected multi-targets and refresh Unity feedback

**Files:**
- Modify: `Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`
- Test: `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`
- Test: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`
- Modify: `Assets/Unity/PLAYTEST.md`

**Interfaces:**
- Preserves: `SelectionResult CardSelectionMachine.ClickTarget(SelectionTargetRef target)`.
- Produces: selected-target removal in `PickMultipleTargets` and `ReadyToConfirm`.
- Preserves: `CardSelectionController.OnTargetClicked(SelectionTargetRef, CardPresentation?)` and all serialized fields.
- Preserves: single-target immediate completion, target-domain validation, and `Confirm()` behavior.

- [ ] **Step 1: Prepare the Unity test helper for the existing serialized contract**

In `CardSelectionControllerTests`, add the candidate color beside `SelectedOutline`:

```csharp
private static readonly Color CandidateOutline =
    new Color(0.95f, 0.72f, 0.25f, 1f);
```

Add this helper:

```csharp
private static Image Highlight(UnitView view)
    => Field<GameObject>(view, "_targetHighlight").GetComponent<Image>();
```

In `Rejected_result_removes_stale_pick_and_keeps_selection_active`, replace:

```csharp
var secondHighlight = Field<Image>(secondView, "_targetHighlight");
```

with:

```csharp
var secondHighlight = Highlight(secondView);
```

This matches the `GameObject` serialization contract restored by commit `8b267ec` and prevents the actual Unity test from casting the stored GameObject to `Image`.

- [ ] **Step 2: Write the failing pure-state tests**

In `Multiple_targets_require_explicit_confirmation`, remove the duplicate second click on `first`; retain one click on `first`, one on `second`, and the expected `[first, second]` order.

Add:

```csharp
[Test]
public void Selected_multiple_target_click_removes_pick_before_requirement_is_met()
{
    var machine = new CardSelectionMachine();
    var first = SelectionTargetRef.ExecutionCard(1);
    var second = SelectionTargetRef.ExecutionCard(2);
    machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 3);
    machine.ClickTarget(first);
    machine.ClickTarget(second);

    var result = machine.ClickTarget(first);

    Assert.IsFalse(result.IsComplete);
    Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
    CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);
}

[Test]
public void Ready_target_click_removes_pick_and_reselection_restores_ready()
{
    var machine = new CardSelectionMachine();
    var first = SelectionTargetRef.ExecutionCard(1);
    var second = SelectionTargetRef.ExecutionCard(2);
    machine.SelectCard(0, SelectionTargetKind.ExecutionCard, 2);
    machine.ClickTarget(first);
    machine.ClickTarget(second);
    Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);

    Assert.IsFalse(machine.ClickTarget(first).IsComplete);
    Assert.AreEqual(SelectionPhase.PickMultipleTargets, machine.Phase);
    CollectionAssert.AreEqual(new[] { second }, machine.PickedTargets);

    Assert.IsFalse(machine.ClickTarget(first).IsComplete);
    Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);
    CollectionAssert.AreEqual(new[] { second, first }, machine.PickedTargets);
}
```

Retain `Extra_target_click_after_ready_is_ignored` and all single-target tests unchanged. They prove that only already selected multi-targets toggle.

- [ ] **Step 3: Write the failing Unity feedback test**

Add to `CardSelectionControllerTests`:

```csharp
[Test]
public void Selected_target_click_restores_candidate_and_hides_confirm()
{
    var first = SelectionTargetRef.PartyMember("member-a");
    var second = SelectionTargetRef.PartyMember("member-b");
    var firstView = UnitView.EditorCreate(
        (RectTransform)_root.transform, new Vector2(180f, 250f));
    var secondView = UnitView.EditorCreate(
        (RectTransform)_root.transform, new Vector2(180f, 250f));
    _controller.RegisterUnitTarget(first, firstView);
    _controller.RegisterUnitTarget(second, secondView);
    _controller.BeginTargetSelection(
        0, SelectionTargetKind.PartyMember, 2, new[] { first, second });

    _controller.OnTargetClicked(first, null);
    _controller.OnTargetClicked(second, null);
    Assert.IsTrue(_confirmButton.gameObject.activeSelf);
    Assert.AreEqual(SelectedOutline, Highlight(firstView).color);
    Assert.AreEqual(SelectedOutline, Highlight(secondView).color);

    _controller.OnTargetClicked(first, null);
    Assert.IsFalse(_confirmButton.gameObject.activeSelf);
    Assert.AreEqual(CandidateOutline, Highlight(firstView).color);
    Assert.AreEqual(SelectedOutline, Highlight(secondView).color);

    _controller.OnTargetClicked(first, null);
    Assert.IsTrue(_confirmButton.gameObject.activeSelf);
    Assert.AreEqual(SelectedOutline, Highlight(firstView).color);
}
```

- [ ] **Step 4: Run focused tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore --filter FullyQualifiedName~CardSelectionMachineTests
```

Expected: the two new core tests fail because duplicate targets are ignored and `ReadyToConfirm` rejects all target clicks.

Run the Unity test from the licensed Test Runner or:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.CardSelectionControllerTests.Selected_target_click_restores_candidate_and_hides_confirm -testResults /private/tmp/multi-target-toggle-red.xml -quit
```

Expected: the confirm button remains visible and the first target remains blue after the repeated click. If the user's Unity editor keeps the project open, record the occupied-project result; the headless RED still proves the missing production behavior.

- [ ] **Step 5: Implement the minimal core toggle**

Replace the guard and duplicate handling at the start of `CardSelectionMachine.ClickTarget` with:

```csharp
bool isSingleTarget = Phase == SelectionPhase.PickSingleTarget;
bool isMultipleTarget = Phase == SelectionPhase.PickMultipleTargets
    || Phase == SelectionPhase.ReadyToConfirm;
if ((!isSingleTarget && !isMultipleTarget) || target.Kind != _targetKind)
{
    return SelectionResult.None;
}

int pickedIndex = _picked.IndexOf(target);
if (isMultipleTarget && pickedIndex >= 0)
{
    _picked.RemoveAt(pickedIndex);
    Phase = SelectionPhase.PickMultipleTargets;
    return SelectionResult.None;
}

if (Phase == SelectionPhase.ReadyToConfirm
    || pickedIndex >= 0
    || _picked.Count >= RequiredTargets)
{
    return SelectionResult.None;
}
```

Keep the existing `_picked.Add(target)`, single-target completion, and `ReadyToConfirm` transition below this block unchanged. Do not change `CardSelectionController`; it already forwards active target clicks, refreshes visuals, and only plays center emphasis when the picked count increases.

- [ ] **Step 6: Run GREEN verification**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore --filter FullyQualifiedName~CardSelectionMachineTests
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore
```

Expected: focused tests pass and the full suite reports 280 passed, 0 failed, 0 skipped.

Run the Unity test again. If the project remains open, compile the changed Unity test source without claiming Test Runner execution:

```bash
dotnet build /private/tmp/CodexUnityCompile/CardSelectionControllerTestCompile.csproj --no-restore
```

Expected fallback: build succeeds with 0 warnings and 0 errors. When Unity is available, the focused test and full EditMode suite pass with zero failures.

- [ ] **Step 7: Update manual playtest coverage**

Replace unified-target checklist case 4 in `Assets/Unity/PLAYTEST.md` with:

```text
4. 자리 교환은 화살표 시작점이 손패 카드에 고정되고, 선택 가능한 대상은 황금색 테두리,
   선택한 두 대상은 푸른색 테두리로 표시된 뒤 확인 버튼이 나타난다. 선택한 대상을 다시 클릭하면
   황금색 후보로 돌아가고 확인 버튼이 사라지며, 다시 선택하면 푸른색과 확인 버튼이 복구된다.
   번호 배지는 표시되지 않는다.
```

- [ ] **Step 8: Check scope and commit**

```bash
git diff --check -- Assets/Core/Simulation/Presentation/CardSelectionMachine.cs Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Unity/PLAYTEST.md
git add Assets/Core/Simulation/Presentation/CardSelectionMachine.cs Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Unity/PLAYTEST.md
git commit -m "feat(input): toggle selected multi-targets"
```

- [ ] **Step 9: Confirm the worktree boundary**

```bash
git status --short --branch
git log --oneline -5
```

Expected: the implementation commit follows the design and plan commits; only the pre-existing Unity scene, prefab, targeting-arrow, and `KoreanTMP.asset` changes remain unstaged. No scene or prefab regeneration is required because no serialized field or UI hierarchy changes.
