# Execution Placement Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 대상 없는 실행 카드를 잡고 실행 영역에 올렸을 때 실제 최종 위치에 반투명한 푸른색 레일 카드 프리뷰를 표시하고, 대상 선택의 중앙 카드 강조 연출은 제거한다.

**Architecture:** 순수 코어의 `DeckCombatSession`이 상태 효과와 `FutureZone` 정렬 규칙을 공유해 최종 실행 순서와 삽입 인덱스를 무변이로 계산한다. Unity의 `ExecutionRailView`는 기존 `RailCardView` 프리팹을 런타임에 재사용해 레이아웃 안에 프리뷰를 넣고, `CardSelectionController`와 `BattleScreenController`가 배치 시작·종료를 연결한다.

**Tech Stack:** C#, .NET/NUnit headless tests, Unity 6000.5.2f1, uGUI, Unity Input System, Unity EditMode tests

## Global Constraints

- 마우스를 따라다니는 잡은 전체 카드는 유지한다.
- 대상 클릭 뒤 화면 중앙에 나타나는 별도 강조 카드와 코루틴은 완전히 제거한다.
- 프리뷰 위치는 마우스 좌표가 아니라 실제 실행 순서, 상태 효과, 플레이어 우선, 안정 동률 정렬로 계산한다.
- 프리뷰 계산은 운명력, 손패, 버린 카드, 미래 영역, 인스턴스 ID, RNG 상태를 변경하지 않는다.
- 실행 영역 밖에서는 레일 프리뷰를 숨기고, 영역 안에서는 실제 최종 sibling index에 표시한다.
- 레일 프리뷰는 `CanvasGroup.alpha = 0.5`, `CardView.SelectionKind.Secondary`, 입력 비활성 상태다.
- 프리뷰가 활성화되면 가로 레이아웃에 참여해 뒤 카드를 실제 배치처럼 민다.
- 실행 영역 이탈, 성공, 취소, 다른 선택 시작, 카드 목록 재구성에서 프리뷰를 숨긴다.
- 배치 프리뷰 중 기존 레일 카드의 전체 카드 호버 프리뷰를 표시하지 않는다.
- 새 프리팹, 새 직렬화 필드, 외부 에셋 또는 외부 패키지를 추가하지 않는다.
- `Assets/Core`와 `Assets/Core/Simulation`은 `UnityEngine`을 참조하지 않는다.
- 기존 씬, 프리팹, 타겟 화살표와 `KoreanTMP.asset` 변경은 사용자 생성물로 간주해 커밋하지 않는다.

## File Map

- Modify `Assets/Core/Combat/FutureZone.cs`: 실제 정렬을 공유하는 가상 후보 삽입 인덱스 계산.
- Modify `Assets/Core/Tests/EditMode/FutureZoneTests.cs`: 빠름·느림·동률·무변이 테스트.
- Modify `Assets/Core/Simulation/DeckCombatSession.cs`: `ExecutionPlacementPreview`, 상태 적용 순서 공유, 읽기 전용 프리뷰 API.
- Modify `Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs`: 상태 효과, 실제 배치 일치, 거부·무변이 테스트.
- Modify `Assets/Unity/ExecutionRailView.cs`: 포인터 진입/이탈과 레이아웃 프리뷰 생명주기.
- Modify `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`: 프리뷰 위치·알파·테두리·입력·정리 테스트.
- Modify `Assets/Unity/CardPresentation.cs`: 최종 실행 순서만 바꾼 UI 스냅샷 복사.
- Modify `Assets/Unity/CardSelectionController.cs`: 프리뷰 등록/정리와 중앙 강조 제거.
- Modify `Assets/Unity/BattleScreenController.cs`: 코어 위치 프리뷰를 배치 시작에 연결.
- Modify `Assets/Tests/UnityEditMode/CardPresentationTests.cs`: 실행 순서 복사 보존 테스트.
- Modify `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`: 새 API 및 중앙 강조 제거 테스트.
- Modify `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`: `OnTargetClicked` API 호출 갱신.
- Modify `Assets/Unity/PLAYTEST.md`: 자동 위치 프리뷰 수동 검증.

---

### Task 1: Compute the real placement preview without mutation

**Files:**
- Modify: `Assets/Core/Combat/FutureZone.cs`
- Test: `Assets/Core/Tests/EditMode/FutureZoneTests.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Test: `Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs`

**Interfaces:**
- Produces: `int FutureZone.PreviewInsertionIndex(ExecutionCardInstance candidate)`.
- Produces: `ExecutionPlacementPreview(int executionOrder, int insertionIndex)` in namespace `FateWeaver.Simulation`.
- Produces: `bool DeckCombatSession.TryPreviewExecutionPlacement(int handIndex, out ExecutionPlacementPreview preview)`.
- Preserves: `bool DeckCombatSession.PlayExecutionCard(int handIndex, string targetId = null)` behavior.

- [ ] **Step 1: Write failing FutureZone ordering tests**

Add to `FutureZoneTests`:

```csharp
[TestCase(1, 0)]
[TestCase(3, 1)]
[TestCase(6, 2)]
public void Preview_insertion_uses_execution_order_without_mutating_zone(
    int candidateOrder, int expectedIndex)
{
    var zone = new FutureZone();
    zone.Add(Card("fast", 2));
    zone.Add(Card("slow", 5));
    var before = zone.Cards.ToArray();

    int index = zone.PreviewInsertionIndex(Card("candidate", candidateOrder));

    Assert.AreEqual(expectedIndex, index);
    CollectionAssert.AreEqual(before, zone.Cards);
}

[Test]
public void Preview_insertion_puts_new_player_after_player_ties_and_before_enemy_ties()
{
    var zone = new FutureZone();
    zone.Add(Card("enemy", 5, Side.Enemy));
    zone.Add(Card("player", 5, Side.Player));

    int index = zone.PreviewInsertionIndex(Card("candidate", 5, Side.Player));

    Assert.AreEqual(1, index);
    CollectionAssert.AreEqual(new[] { "player", "enemy" },
        zone.ResolutionOrder().Select(card => card.Def.Id).ToArray());
}
```

- [ ] **Step 2: Write failing session preview tests**

Add to `PartyDeckCombatSessionTests`:

```csharp
[Test]
public void Placement_preview_applies_owner_status_and_matches_real_position_without_mutation()
{
    var session = Session(
        new[] { Loadout("a", new[] { Execution("preview", order: 5) }) },
        new[] { EnemyStrike(order: 4, damage: 0) });
    session.State.Party.Single().Statuses.Add(
        StatusKeys.Haste, StatusLifetime.Turns(2), magnitude: 3);
    int energyBefore = session.FateEnergy;
    var handBefore = session.Hand.ToArray();
    var orderBefore = session.CurrentOrder.ToArray();
    int highestInstanceId = orderBefore.Max(card => card.InstanceId);

    Assert.IsTrue(session.TryPreviewExecutionPlacement(0, out var preview));

    Assert.AreEqual(2, preview.ExecutionOrder);
    Assert.AreEqual(0, preview.InsertionIndex);
    Assert.AreEqual(energyBefore, session.FateEnergy);
    CollectionAssert.AreEqual(handBefore, session.Hand);
    CollectionAssert.AreEqual(orderBefore, session.CurrentOrder);

    Assert.IsTrue(session.PlayExecutionCard(0));
    var placed = session.CurrentOrder[preview.InsertionIndex];
    Assert.AreEqual("preview", placed.Def.Id);
    Assert.AreEqual(preview.ExecutionOrder, placed.ExecutionOrder);
    Assert.AreEqual(highestInstanceId + 1, placed.InstanceId);
}

[Test]
public void Placement_preview_rejects_invalid_unaffordable_nonexecution_and_resolved_turn()
{
    var unaffordable = Session(new[]
    {
        Loadout("a", new[] { Execution("costly", cost: 4) })
    }, fateEnergyPerTurn: 3);
    Assert.IsFalse(unaffordable.TryPreviewExecutionPlacement(-1, out _));
    Assert.IsFalse(unaffordable.TryPreviewExecutionPlacement(0, out _));

    var intervention = Execution("intervention");
    intervention.Category = CardCategory.Intervention;
    var wrongCategory = Session(new[] { Loadout("a", new[] { intervention }) });
    Assert.IsFalse(wrongCategory.TryPreviewExecutionPlacement(0, out _));

    var resolved = Session(new[] { Loadout("a", new[] { Execution("late") }) });
    resolved.ResolveTurn();
    Assert.IsFalse(resolved.TryPreviewExecutionPlacement(0, out _));
}

[Test]
public void Placement_preview_does_not_advance_future_draw_rng()
{
    var cards = Enumerable.Range(0, 8)
        .Select(index => Execution("card_" + index))
        .ToArray();
    var previewed = Session(
        new[] { Loadout("a", cards) },
        new[] { EnemyStrike(damage: 0) }, seed: 17);
    var control = Session(
        new[] { Loadout("a", cards) },
        new[] { EnemyStrike(damage: 0) }, seed: 17);

    Assert.IsTrue(previewed.TryPreviewExecutionPlacement(0, out _));
    previewed.ResolveTurn();
    control.ResolveTurn();
    Assert.IsTrue(previewed.BeginNextTurn());
    Assert.IsTrue(control.BeginNextTurn());

    CollectionAssert.AreEqual(
        control.Hand.Select(card => card.Def.Id).ToArray(),
        previewed.Hand.Select(card => card.Def.Id).ToArray());
}
```

- [ ] **Step 3: Run focused tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore --filter "FullyQualifiedName~FutureZoneTests|FullyQualifiedName~PartyDeckCombatSessionTests"
```

Expected: compilation fails because `PreviewInsertionIndex`, `ExecutionPlacementPreview`, and `TryPreviewExecutionPlacement` do not exist.

- [ ] **Step 4: Share FutureZone ordering and implement insertion preview**

Add `using System;` and replace `ResolutionOrder` with the shared helper:

```csharp
public IReadOnlyList<ExecutionCardInstance> ResolutionOrder()
    => Ordered(_cards).ToList();

public int PreviewInsertionIndex(ExecutionCardInstance candidate)
{
    if (candidate == null)
    {
        throw new ArgumentNullException(nameof(candidate));
    }

    return Ordered(_cards.Concat(new[] { candidate })).ToList().IndexOf(candidate);
}

private static IOrderedEnumerable<ExecutionCardInstance> Ordered(
    IEnumerable<ExecutionCardInstance> cards)
    => cards
        .OrderBy(card => card.ExecutionOrder)
        .ThenBy(card => card.Def.Side == Side.Player ? 0 : 1);
```

- [ ] **Step 5: Add the preview value and session API**

Before `DeckCombatSession`, add:

```csharp
public readonly struct ExecutionPlacementPreview
{
    public int ExecutionOrder { get; }
    public int InsertionIndex { get; }

    public ExecutionPlacementPreview(int executionOrder, int insertionIndex)
    {
        ExecutionOrder = executionOrder;
        InsertionIndex = insertionIndex;
    }
}
```

Add to `DeckCombatSession`:

```csharp
public bool TryPreviewExecutionPlacement(
    int handIndex, out ExecutionPlacementPreview preview)
{
    preview = default;
    if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
    {
        return false;
    }

    var card = _deck.Hand[handIndex];
    if (card.Def.Category != CardCategory.Execution
        || _state.FateEnergy < card.Def.EnergyCost)
    {
        return false;
    }

    int executionOrder = EffectiveExecutionOrderFor(card);
    var candidate = new ExecutionCardInstance(card.Def)
    {
        OwnerId = card.OwnerId,
        ExecutionOrder = executionOrder
    };
    preview = new ExecutionPlacementPreview(
        executionOrder, _state.Zone.PreviewInsertionIndex(candidate));
    return true;
}

private int EffectiveExecutionOrderFor(OwnedCard card)
    => StatusExecutionOrder.ExecutionOrderFor(
        card.Def.BaseExecutionOrder, OwnerStatusesFor(card), _statuses);

private StatusBag OwnerStatusesFor(OwnedCard card)
{
    if (!_isPartyMode)
    {
        return _state.PlayerStatuses;
    }

    foreach (var member in _state.Party)
    {
        if (member.IsAlive && member.Id == card.OwnerId)
        {
            return member.Statuses;
        }
    }

    return null;
}
```

In `PlayExecutionCard`, replace the duplicated owner-status search and `StatusExecutionOrder` call with:

```csharp
placed.ExecutionOrder = EffectiveExecutionOrderFor(card);
```

- [ ] **Step 6: Run focused and full headless GREEN verification**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore --filter "FullyQualifiedName~FutureZoneTests|FullyQualifiedName~PartyDeckCombatSessionTests"
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore
```

Expected: focused and full suites pass with 0 failures; existing execution placement tests remain green.

- [ ] **Step 7: Commit the pure-core preview**

```bash
git add Assets/Core/Combat/FutureZone.cs Assets/Core/Tests/EditMode/FutureZoneTests.cs Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs
git commit -m "feat(core): preview execution placement"
```

---

### Task 2: Render a translucent blue card inside the execution rail

**Files:**
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Test: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`

**Interfaces:**
- Produces: `void ExecutionRailView.SetPlacementPreview(CardPresentation card, int insertionIndex)`.
- Produces: `void ExecutionRailView.ClearPlacementPreview()`.
- Produces: `IPointerEnterHandler.OnPointerEnter(PointerEventData)` and `IPointerExitHandler.OnPointerExit(PointerEventData)`.
- Consumes: existing serialized `_cardPrefab`, `_content`, and `RailCardView.SetSelection/SetInteractable`.

- [ ] **Step 1: Write the failing rail preview test**

Add `using UnityEngine.EventSystems;` and add to `ExecutionRailInputTests`:

```csharp
private static readonly Color BlueOutline =
    new Color(0.35f, 0.75f, 0.95f, 1f);

[Test]
public void Placement_preview_appears_at_slot_with_blue_translucent_noninteractive_style()
{
    var root = new GameObject("Root", typeof(RectTransform));
    var overlay = ChildRect(root.transform, "Overlay");
    var prefabRoot = ChildRect(root.transform, "PrefabRoot");
    try
    {
        var prefab = RailCardView.EditorCreate(prefabRoot, new Vector2(96f, 132f));
        var railObject = ChildRect(root.transform, "Rail");
        var rail = railObject.gameObject.AddComponent<ExecutionRailView>();
        rail.EditorBuild(null, prefab, overlay);
        var existing = new CardPresentation(
            "existing", "existing", 4, 0, Side.Enemy, string.Empty, null, false);
        var candidate = new CardPresentation(
            "candidate", "candidate", 3, 1, Side.Player, string.Empty, null, false);
        rail.SetCards(new[] { existing, existing }, _ => { });

        rail.SetPlacementPreview(candidate, 1);
        var preview = Field<RailCardView>(rail, "_placementPreview");
        Assert.IsFalse(preview.gameObject.activeSelf);

        rail.OnPointerEnter((PointerEventData)null);

        Assert.IsTrue(preview.gameObject.activeSelf);
        Assert.AreEqual(1, preview.transform.GetSiblingIndex());
        Assert.AreEqual(2, Field<List<RailCardView>>(rail, "_views")[1]
            .transform.GetSiblingIndex());
        Assert.AreEqual(0.5f, preview.GetComponent<CanvasGroup>().alpha);
        Assert.IsFalse(Field<Button>(preview, "_button").interactable);
        Assert.AreEqual(BlueOutline, Field<Image>(preview, "_selectionOutline").color);

        rail.OnPointerExit((PointerEventData)null);
        Assert.IsFalse(preview.gameObject.activeSelf);
        rail.OnPointerEnter((PointerEventData)null);
        Assert.IsTrue(preview.gameObject.activeSelf);

        rail.ClearPlacementPreview();
        Assert.IsFalse(preview.gameObject.activeSelf);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

Add the generic reflection helper:

```csharp
private static T Field<T>(object target, string name)
    => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(target);
```

- [ ] **Step 2: Write the failing cards-refresh cleanup test**

Add:

```csharp
[Test]
public void Rebuilding_cards_clears_active_placement_preview()
{
    var root = new GameObject("Root", typeof(RectTransform));
    var overlay = ChildRect(root.transform, "Overlay");
    try
    {
        var prefab = RailCardView.EditorCreate(
            ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
        var rail = Child<ExecutionRailView>(root.transform, "Rail");
        rail.EditorBuild(null, prefab, overlay);
        var candidate = new CardPresentation(
            "candidate", "candidate", 3, 1, Side.Player, string.Empty, null, false);
        rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
        rail.SetPlacementPreview(candidate, 0);
        rail.OnPointerEnter(null);
        var preview = Field<RailCardView>(rail, "_placementPreview");
        Assert.IsTrue(preview.gameObject.activeSelf);

        rail.SetCards(Array.Empty<CardPresentation>(), _ => { });

        Assert.IsFalse(preview.gameObject.activeSelf);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

- [ ] **Step 3: Run the focused Unity test and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests -testResults /private/tmp/execution-placement-rail-red.xml -quit
```

Expected: compilation fails because the placement preview and pointer-handler APIs do not exist. If the project is open in the user's Unity editor, record the occupied-project failure and use the existing test-source compile fallback for RED/GREEN compilation evidence.

- [ ] **Step 4: Implement the rail preview state and pointer handlers**

Add `using UnityEngine.EventSystems;`, implement both pointer interfaces, and add:

```csharp
private const float PlacementPreviewAlpha = 0.5f;
private RailCardView _placementPreview;
private CardPresentation? _placementPreviewCard;
private int _placementPreviewIndex = -1;

public void SetPlacementPreview(CardPresentation card, int insertionIndex)
{
    if (insertionIndex < 0 || insertionIndex > _views.Count)
    {
        throw new ArgumentOutOfRangeException(nameof(insertionIndex));
    }

    _placementPreviewCard = card;
    _placementPreviewIndex = insertionIndex;
    EnsurePlacementPreview();
    HidePlacementPreview();
    HidePreview();
}

public void ClearPlacementPreview()
{
    _placementPreviewCard = null;
    _placementPreviewIndex = -1;
    HidePlacementPreview();
}

public void OnPointerEnter(PointerEventData eventData)
{
    ShowPlacementPreview();
}

public void OnPointerExit(PointerEventData eventData)
{
    HidePlacementPreview();
}
```

Add the helpers:

```csharp
private void EnsurePlacementPreview()
{
    if (_placementPreview != null)
    {
        return;
    }

    _placementPreview = Instantiate(_cardPrefab, _content);
    ((RectTransform)_placementPreview.transform).sizeDelta = CardSize;
    var group = _placementPreview.gameObject.AddComponent<CanvasGroup>();
    group.alpha = PlacementPreviewAlpha;
    group.interactable = false;
    group.blocksRaycasts = false;
    _placementPreview.SetInteractable(false);
    _placementPreview.gameObject.SetActive(false);
}

private void ShowPlacementPreview()
{
    if (!_placementPreviewCard.HasValue || _placementPreview == null)
    {
        return;
    }

    _placementPreview.Bind(_placementPreviewCard.Value, null, null);
    _placementPreview.SetSelection(CardView.SelectionKind.Secondary);
    _placementPreview.SetInteractable(false);
    _placementPreview.transform.SetSiblingIndex(_placementPreviewIndex);
    _placementPreview.gameObject.SetActive(true);
    LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
}

private void HidePlacementPreview()
{
    if (_placementPreview != null)
    {
        _placementPreview.gameObject.SetActive(false);
    }
}
```

At the start of `SetCards`, call `ClearPlacementPreview()` before destroying `_views`. In `OnHover`, add this first guard so the full hover preview does not compete with placement feedback:

```csharp
if (_placementPreviewCard.HasValue)
{
    HidePreview();
    return;
}
```

- [ ] **Step 5: Run focused compile and Unity GREEN verification**

```bash
dotnet build /private/tmp/CodexUnityCompile/Task4UnityCompile.csproj --no-restore
dotnet build /private/tmp/CodexUnityCompile/Task4TestCompile.csproj --no-restore
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests -testResults /private/tmp/execution-placement-rail-green.xml -quit
```

Expected: fallback builds have 0 warnings and 0 errors; focused Unity tests have 0 failures when the editor is available. Do not claim Test Runner execution if another Unity instance owns the project.

- [ ] **Step 6: Commit the rail preview**

```bash
git add Assets/Unity/ExecutionRailView.cs Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs
git commit -m "feat(ui): preview execution rail placement"
```

---

### Task 3: Integrate placement preview and remove center emphasis

**Files:**
- Modify: `Assets/Unity/CardPresentation.cs`
- Modify: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/BattleScreenController.cs`
- Test: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`
- Modify: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`
- Modify: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`
- Modify: `Assets/Unity/PLAYTEST.md`

**Interfaces:**
- Consumes: Task 1 `TryPreviewExecutionPlacement` and `ExecutionPlacementPreview`.
- Consumes: Task 2 `SetPlacementPreview` and `ClearPlacementPreview`.
- Produces: `CardPresentation CardPresentation.WithExecutionOrder(int executionOrder)`.
- Replaces: `BeginPlacement(int handIndex, CardPresentation card)` with `BeginPlacement(int handIndex, CardPresentation card, int insertionIndex)`.
- Replaces: `OnTargetClicked(SelectionTargetRef target, CardPresentation? card)` with `OnTargetClicked(SelectionTargetRef target)`.

- [ ] **Step 1: Write the failing presentation-copy test**

Add to `CardPresentationTests`:

```csharp
[Test]
public void With_execution_order_changes_only_order()
{
    var original = new CardPresentation(
        "id", "name", 5, 2, Side.Player, "description", null, false,
        new[] { CardStatusIcon.Lock }, CardCategory.Execution,
        "owner", Color.cyan, true);

    var changed = original.WithExecutionOrder(2);

    Assert.AreEqual(2, changed.ExecutionOrder);
    Assert.AreEqual(original.Id, changed.Id);
    Assert.AreEqual(original.DisplayName, changed.DisplayName);
    Assert.AreEqual(original.EnergyCost, changed.EnergyCost);
    Assert.AreEqual(original.Side, changed.Side);
    Assert.AreEqual(original.Description, changed.Description);
    Assert.AreEqual(original.StatusIcons, changed.StatusIcons);
    Assert.AreEqual(original.Category, changed.Category);
    Assert.AreEqual(original.OwnerDisplayName, changed.OwnerDisplayName);
    Assert.AreEqual(original.OwnerColor, changed.OwnerColor);
    Assert.AreEqual(original.IsPartyOwned, changed.IsPartyOwned);
}
```

- [ ] **Step 2: Write the failing controller API and no-center-card test**

Update all `CardSelectionControllerTests` calls from `OnTargetClicked(target, null)` to `OnTargetClicked(target)`. Add:

```csharp
[Test]
public void Target_click_does_not_create_center_emphasis_card()
{
    var target = SelectionTargetRef.PartyMember("member-a");
    var overlay = Field<RectTransform>(_controller, "_overlay");
    int childCountBefore = overlay.childCount;
    _controller.BeginTargetSelection(
        0, SelectionTargetKind.PartyMember, 1, new[] { target });

    _controller.OnTargetClicked(target);

    Assert.AreEqual(childCountBefore, overlay.childCount);
}
```

In `ExecutionRailInputTests`, replace:

```csharp
index => selection.OnTargetClicked(
    SelectionTargetRef.ExecutionCard(index), card)
```

with:

```csharp
index => selection.OnTargetClicked(SelectionTargetRef.ExecutionCard(index))
```

- [ ] **Step 3: Run changed Unity test sources and verify RED**

```bash
dotnet build /private/tmp/CodexUnityCompile/CardSelectionControllerTestCompile.csproj --no-restore
dotnet build /private/tmp/CodexUnityCompile/Task4TestCompile.csproj --no-restore
```

Expected: compilation fails because `WithExecutionOrder` and the one-argument `OnTargetClicked` API do not exist.

- [ ] **Step 4: Implement `WithExecutionOrder`**

Add to `CardPresentation`:

```csharp
public CardPresentation WithExecutionOrder(int executionOrder)
    => new CardPresentation(
        Id,
        DisplayName,
        executionOrder,
        EnergyCost,
        Side,
        Description,
        Art,
        IsLocked,
        StatusIcons,
        Category,
        OwnerDisplayName,
        OwnerColor,
        IsPartyOwned);
```

- [ ] **Step 5: Remove center emphasis and connect rail preview in the selection controller**

In `CardSelectionController`:

- Remove `using System.Collections;`.
- Remove `EmphasisHoldSeconds`, `EmphasisGrowSeconds`, `_emphasisCard`, and `_emphasis`.
- Change `BeginPlacement` to:

```csharp
public void BeginPlacement(
    int handIndex, CardPresentation card, int insertionIndex)
{
    EndSelectionVisuals();
    _machine.SelectCard(handIndex, SelectionTargetKind.None, 0);
    _visualHandIndex = handIndex;
    _hand.SetHoverSuppressed(true);
    _rail.SetDropHint(true);
    _rail.SetPlacementPreview(card, insertionIndex);
    _hand.SetGhost(handIndex, true);
    SpawnFloatingCard(card);
}
```

- Change the target click method to:

```csharp
public void OnTargetClicked(SelectionTargetRef target)
{
    if (!SelectionActive
        || target.Kind != _targetKind
        || !_validTargets.Contains(target))
    {
        return;
    }

    var result = _machine.ClickTarget(target);
    RefreshTargetVisuals();
    TryDispatch(result);
}
```

- Delete `PlayCenterEmphasis` and `CenterEmphasis` entirely.
- In `EndSelectionVisuals`, call `_rail.ClearPlacementPreview()` immediately after `_rail.SetDropHint(false)`.
- Delete the `_emphasis` coroutine stop and `_emphasisCard` hide blocks from `EndSelectionVisuals`.

- [ ] **Step 6: Connect actual preview data in BattleScreenController**

In the targetless execution-card branch, replace `BeginPlacement` with:

```csharp
if (!_session.TryPreviewExecutionPlacement(handIndex, out var placement))
{
    SetMessage("카드를 실행 순서에 배치할 수 없습니다.");
    return;
}

var presentation = PresentationFor(card)
    .WithExecutionOrder(placement.ExecutionOrder);
_selection.BeginPlacement(
    handIndex, presentation, placement.InsertionIndex);
SetMessage(name + " — 실행 순서를 클릭해 배치하세요.");
```

Update unit bindings and `OnZoneClicked` to the one-argument API:

```csharp
view.BindTarget(member.Id, id => _selection.OnTargetClicked(
    SelectionTargetRef.PartyMember(id)));
view.BindTarget(enemy.Id, id => _selection.OnTargetClicked(
    SelectionTargetRef.Enemy(id)));
_selection.OnTargetClicked(SelectionTargetRef.ExecutionCard(zoneIndex));
```

- [ ] **Step 7: Run Unity compile and headless regression verification**

```bash
dotnet build /private/tmp/CodexUnityCompile/IntegrationCompile.csproj --no-restore
dotnet build /private/tmp/CodexUnityCompile/Task4EditorCompile.csproj --no-restore
dotnet build /private/tmp/CodexUnityCompile/CardSelectionControllerTestCompile.csproj --no-restore
dotnet build /private/tmp/CodexUnityCompile/Task4TestCompile.csproj --no-restore
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore
rg -n "OnTargetClicked\([^)]*,|BeginPlacement\([^,]+,[^,]+\)" Assets -g '*.cs'
```

Expected: all compile fallbacks succeed with 0 warnings and 0 errors; all headless tests pass with 0 failures; the signature search returns no matches.

When Unity is available, run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration -runTests -testPlatform EditMode -testResults /private/tmp/execution-placement-final-editmode.xml -quit
```

Expected: all EditMode tests pass with 0 failures. If the editor is already open, record that limitation and do not claim Test Runner success.

- [ ] **Step 8: Update manual playtest coverage**

Replace unified-target checklist case 1 and add the center-emphasis check:

```text
1. 대상 없는 실행 카드는 마우스를 따라가며, 실행 영역 밖에는 레일 프리뷰가 없다. 실행 영역에 올리면
   상태 효과가 반영된 실제 최종 위치에 알파 0.5의 푸른색 레일 카드가 나타나고 기존 카드가 한 칸 밀린다.
   영역에서 벗어나면 프리뷰가 사라지며, 클릭 배치 후 실제 카드 위치는 프리뷰와 일치한다.
2. 명시적 대상을 클릭해도 화면 중앙에 별도 전체 카드 강조가 나타나지 않는다.
```

Renumber the remaining checklist items without changing their behavior requirements.

- [ ] **Step 9: Commit integration and documentation**

```bash
git diff --check -- Assets/Unity/CardPresentation.cs Assets/Unity/CardSelectionController.cs Assets/Unity/BattleScreenController.cs Assets/Tests/UnityEditMode/CardPresentationTests.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs Assets/Unity/PLAYTEST.md
git add Assets/Unity/CardPresentation.cs Assets/Unity/CardSelectionController.cs Assets/Unity/BattleScreenController.cs Assets/Tests/UnityEditMode/CardPresentationTests.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs Assets/Unity/PLAYTEST.md
git commit -m "refactor(ui): replace center emphasis with rail preview"
```

- [ ] **Step 10: Confirm the worktree boundary**

```bash
git status --short --branch
git log --oneline -7
```

Expected: the three implementation commits follow the design and plan commits; only the pre-existing scene, prefab, targeting-arrow, and `KoreanTMP.asset` changes remain unstaged. No scene or prefab regeneration is required because all new rail preview state is runtime-only and no serialized field or hierarchy changed.
