# Card Selection Placement Motion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 선택한 손패 카드를 호버 자세와 푸른 테두리로 고정하고, 실행 카드 실루엣 확정 시 큰 카드가 DOTween으로 레일 위치까지 회전·이동·축소되게 한다.

**Architecture:** `HandFanView`가 기존 `CardView` 프리팹으로 입력 없는 비행 복제본과 원본 투명도를 관리하고, `ExecutionRailView`가 배치 실루엣의 목표 변환 및 DOTween sequence를 소유한다. `CardSelectionController`는 비행 준비, 규칙 적용, 연출 완료, 기존 `RefreshAll` 콜백의 순서를 조정하며 코어 세션 API는 변경하지 않는다.

**Tech Stack:** Unity 6000.5.2f1, C# 9, uGUI, TextMeshPro, DOTween, NUnit Unity EditMode tests, .NET headless tests

## Global Constraints

- 작업 브랜치는 `card-selection-placement-motion`이며 git worktree를 사용하지 않는다.
- `FateWeaver.Core`와 `FateWeaver.Simulation`의 규칙·결정론·배치 API는 변경하지 않는다.
- 런타임 `new GameObject`, 문자열 탐색, 경로 하드코딩과 새 외부 패키지를 추가하지 않는다.
- 비행 카드는 `HandFanView`에 이미 직렬화된 `CardView` 프리팹을 재사용한다.
- Unity 참조는 기존 `[SerializeField] private` 참조 또는 컴포넌트 API로 전달한다.
- DOTween 전역 kill을 사용하지 않고 각 뷰가 소유한 tween만 종료한다.
- 조작 카드 대상 선택 규칙과 실행 카드의 두 단계 실루엣 확정 흐름은 유지한다.
- 구현 전에 대응하는 Unity EditMode 테스트를 추가하고 의도한 실패를 확인한다.

## File Map

- Modify `Assets/Unity/HandFanView.cs`: 푸른 선택 테두리, 비행 카드 준비·표시·정리, 원본 알파 복원.
- Modify `Assets/Unity/CardSelectionController.cs`: hold 순서, 배치 적용과 비행 완료 사이 상태, 중복 입력 차단.
- Modify `Assets/Unity/ExecutionRailView.cs`: 실루엣 목표, 펄스 종료, 이동·축소·회전 sequence와 튜닝값.
- Modify `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`: 호버 자세 고정, 푸른 테두리, 비행 복제본 계약.
- Modify `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`: 비행 목표·sequence·실루엣 생명주기.
- Modify `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`: 성공 지연, 중복 클릭, 실패 무연출.
- Modify `Assets/Unity/PLAYTEST.md`: 수동 확인 절차.

---

### Task 1: Hold selected hand cards in the blue hover pose

**Files:**
- Modify: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`
- Modify: `Assets/Unity/HandFanView.cs:84-94`
- Modify: `Assets/Unity/CardSelectionController.cs:59-129`

**Interfaces:**
- Consumes: `HandCardHoverEffect.Hold(bool)`, `CardView.SelectionKind.Secondary`.
- Produces: placement and explicit-target selection call `SetHeld(handIndex, true)` before `SetHoverSuppressed(true)`.
- Produces: `HandFanView.SetTargetSelection` uses `SelectionKind.Secondary` for the selected hand card.

- [ ] **Step 1: Write failing pose and outline tests**

Add `using System.Reflection;` and `using UnityEngine.UI;` to `HandFanHoverTests.cs`, then add:

```csharp
private static readonly Color BlueOutline =
    new Color(0.35f, 0.75f, 0.95f, 1f);

[Test]
public void Held_card_keeps_the_exact_hover_pose_after_pointer_exit()
{
    var root = new GameObject("Hand", typeof(RectTransform));
    try
    {
        var hand = BuildHand(root, ThreeCards());
        var view = root.GetComponentsInChildren<CardView>()[0];
        var hover = view.GetComponent<HandCardHoverEffect>();
        var rect = (RectTransform)view.transform;
        hover.OnPointerEnter(null);
        Vector2 hoverPosition = rect.anchoredPosition;
        Quaternion hoverRotation = rect.localRotation;
        Vector3 hoverScale = rect.localScale;
        hover.OnPointerExit(null);

        hand.SetHeld(0, true);
        hover.OnPointerExit(null);

        Assert.AreEqual(hoverPosition, rect.anchoredPosition);
        Assert.AreEqual(hoverRotation, rect.localRotation);
        Assert.AreEqual(Quaternion.identity, rect.localRotation);
        Assert.AreEqual(hoverScale, rect.localScale);
    }
    finally { Object.DestroyImmediate(root); }
}

[Test]
public void Target_selected_hand_card_uses_blue_outline()
{
    var root = new GameObject("Hand", typeof(RectTransform));
    try
    {
        var hand = BuildHand(root, ThreeCards());
        var selected = root.GetComponentsInChildren<CardView>()[0];
        hand.SetTargetSelection(0, true);
        Assert.AreEqual(
            BlueOutline,
            Field<Image>(selected, "_selectionOutline").color);
    }
    finally { Object.DestroyImmediate(root); }
}

private static HandFanView BuildHand(
    GameObject root, IReadOnlyList<CardPresentation> cards)
{
    var prefab = AssetDatabase.LoadAssetAtPath<CardView>(
        "Assets/Unity/Prefabs/CardView.prefab");
    Assert.IsNotNull(prefab);
    var hand = root.AddComponent<HandFanView>();
    hand.EditorBuild(prefab);
    hand.SetCards(cards, _ => { }, (_, __) => { });
    return hand;
}

private static CardPresentation[] ThreeCards()
    => Enumerable.Range(0, 3)
        .Select(index => new CardPresentation(
            "execution-" + index, "execution", 3, 1, Side.Player,
            string.Empty, null, false))
        .ToArray();

private static T Field<T>(object target, string name)
    => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(target);
```

- [ ] **Step 2: Run the focused test and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.HandFanHoverTests -testResults /private/tmp/card-selection-hold-red.xml -logFile /private/tmp/card-selection-hold-red.log -quit
```

Expected: `Target_selected_hand_card_uses_blue_outline` fails because the hand still uses gold `Primary`. If Unity is open, a `dotnet build` is only a compile fallback and does not replace this RED assertion.

- [ ] **Step 3: Implement the minimal selection presentation**

Change `HandFanView.SetTargetSelection`:

```csharp
_views[i].SetSelection(active && i == selectedIndex
    ? CardView.SelectionKind.Secondary
    : CardView.SelectionKind.None);
```

In `CardSelectionController.BeginPlacement`, use this order:

```csharp
_visualHandIndex = handIndex;
_hoverHandIndex = -1;
_hand.SetHeld(handIndex, true);
_hand.SetHoverSuppressed(true);
_hand.SetSelection(handIndex, CardView.SelectionKind.Secondary);
```

In `BeginTargetSelection`, move hold before suppression:

```csharp
_hand.SetHeld(handIndex, true);
_hand.SetHoverSuppressed(true);
```

- [ ] **Step 4: Verify GREEN and commit**

Run the Step 2 command, then:

```bash
dotnet build FateWeaver.Tests.UnityEditMode.csproj --no-restore
git diff --check -- Assets/Unity/HandFanView.cs Assets/Unity/CardSelectionController.cs Assets/Tests/UnityEditMode/HandFanHoverTests.cs
git add Assets/Unity/HandFanView.cs Assets/Unity/CardSelectionController.cs Assets/Tests/UnityEditMode/HandFanHoverTests.cs
git commit -m "fix(ui): hold selected cards in blue hover pose"
```

Expected: focused tests pass and build has zero errors.

---

### Task 2: Add reusable hand-to-rail flight views

**Files:**
- Modify: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`
- Modify: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Modify: `Assets/Unity/ExecutionRailView.cs`

**Interfaces:**
- Produces: `HandFanView.PlacementFlightVisual` with `CardView Card`, `RectTransform Rect`.
- Produces: `TryPreparePlacementFlight`, `ShowPlacementFlight`, `ClearPlacementFlight`.
- Produces: `ExecutionRailView.TryGetPlacementFlightLayer(out RectTransform)`.
- Produces: `ExecutionRailView.StartPlacementFlight(RectTransform, Action)`.

- [ ] **Step 1: Write failing hand flight preparation test**

Add to `HandFanHoverTests.cs`:

```csharp
[Test]
public void Prepared_flight_reuses_card_prefab_and_stays_hidden_until_shown()
{
    var root = new GameObject("Root", typeof(RectTransform));
    var overlay = new GameObject("Overlay", typeof(RectTransform));
    try
    {
        overlay.transform.SetParent(root.transform, false);
        var cards = ThreeCards();
        var hand = BuildHand(root, cards);
        Assert.IsTrue(hand.TryPreparePlacementFlight(
            0, cards[0], (RectTransform)overlay.transform, out var visual));
        Assert.IsFalse(visual.Card.gameObject.activeSelf);
        Assert.IsFalse(visual.Card.GetComponent<Button>().interactable);
        Assert.IsTrue(visual.Card.GetComponentsInChildren<Graphic>(true)
            .All(graphic => !graphic.raycastTarget));

        hand.ShowPlacementFlight(visual);
        Assert.IsTrue(visual.Card.gameObject.activeSelf);
        Assert.AreEqual(0f,
            root.GetComponentsInChildren<CardView>()[0]
                .GetComponent<CanvasGroup>().alpha);

        hand.ClearPlacementFlight(visual);
        Assert.AreEqual(1f,
            root.GetComponentsInChildren<CardView>()[0]
                .GetComponent<CanvasGroup>().alpha);
    }
    finally { Object.DestroyImmediate(root); }
}
```

- [ ] **Step 2: Write failing rail flight test**

Add to `ExecutionRailInputTests.cs`:

```csharp
[Test]
public void Placement_flight_hides_silhouette_and_settles_at_its_pose()
{
    SimulateDotweenRuntimeInitializationForEditMode();
    var root = new GameObject("Root", typeof(RectTransform));
    var overlay = ChildRect(root.transform, "Overlay");
    try
    {
        var prefab = RailCardView.EditorCreate(
            ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
        var rail = Child<ExecutionRailView>(root.transform, "Rail");
        rail.EditorBuild(null, prefab, overlay);
        rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
        rail.ShowPlacementHover(Card("candidate", 3, Side.Player), 0);
        rail.ArmPlacementPreview(() => { });
        var flight = ChildRect(overlay, "Flight");
        flight.sizeDelta = new Vector2(170f, 238f);
        bool completed = false;

        Assert.IsTrue(rail.StartPlacementFlight(flight, () => completed = true));
        var preview = Field<RailCardView>(rail, "_placementPreview");
        var sequence = Field<Sequence>(rail, "_placementFlightSequence");
        Assert.AreEqual(0f, preview.GetComponent<CanvasGroup>().alpha);
        Assert.IsFalse(Field<Button>(preview, "_button").interactable);
        Assert.IsTrue(sequence.IsActive());

        sequence.Complete();
        Assert.IsTrue(completed);
        Assert.That(Vector3.Distance(
            flight.position, preview.transform.position), Is.LessThan(0.01f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(
            flight.eulerAngles.z, preview.transform.eulerAngles.z)), Is.LessThan(0.01f));
        Assert.IsNull(Field<Sequence>(rail, "_placementFlightSequence"));
    }
    finally { Object.DestroyImmediate(root); }
}
```

- [ ] **Step 3: Run focused tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck -runTests -testPlatform EditMode -testFilter "FateWeaver.Tests.UnityEditMode.HandFanHoverTests|FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests" -testResults /private/tmp/card-placement-flight-red.xml -logFile /private/tmp/card-placement-flight-red.log -quit
```

Expected: compile failure because the new flight APIs and `_placementFlightSequence` do not exist.

- [ ] **Step 4: Implement hand flight preparation and cleanup**

Add `using UnityEngine.UI;` to `HandFanView.cs`, then add:

```csharp
public sealed class PlacementFlightVisual
{
    internal PlacementFlightVisual(CardView card, CanvasGroup sourceGroup)
    {
        Card = card;
        SourceGroup = sourceGroup;
    }

    public CardView Card { get; }
    public RectTransform Rect => (RectTransform)Card.transform;
    internal CanvasGroup SourceGroup { get; }
}

public bool TryPreparePlacementFlight(
    int index, CardPresentation card, RectTransform layer,
    out PlacementFlightVisual visual)
{
    visual = null;
    if (index < 0 || index >= _views.Count || _cardPrefab == null || layer == null)
    {
        return false;
    }

    var source = (RectTransform)_views[index].transform;
    var copy = Instantiate(_cardPrefab, layer);
    copy.Bind(card, null);
    copy.SetInteractable(false);
    foreach (var graphic in copy.GetComponentsInChildren<Graphic>(true))
    {
        graphic.raycastTarget = false;
    }

    var rect = (RectTransform)copy.transform;
    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
    rect.sizeDelta = source.rect.size;
    rect.SetPositionAndRotation(source.position, source.rotation);
    rect.localScale = RelativeScale(source.lossyScale, layer.lossyScale);
    copy.gameObject.SetActive(false);
    visual = new PlacementFlightVisual(copy, _groups[index]);
    return true;
}

public void ShowPlacementFlight(PlacementFlightVisual visual)
{
    if (visual == null || visual.Card == null) return;
    visual.SourceGroup.alpha = 0f;
    visual.Card.gameObject.SetActive(true);
}

public void ClearPlacementFlight(PlacementFlightVisual visual)
{
    if (visual == null) return;
    if (visual.SourceGroup != null) visual.SourceGroup.alpha = 1f;
    if (visual.Card == null) return;
    if (Application.isPlaying) Destroy(visual.Card.gameObject);
    else DestroyImmediate(visual.Card.gameObject);
}

private static Vector3 RelativeScale(Vector3 worldScale, Vector3 parentScale)
    => new Vector3(
        parentScale.x == 0f ? worldScale.x : worldScale.x / parentScale.x,
        parentScale.y == 0f ? worldScale.y : worldScale.y / parentScale.y,
        parentScale.z == 0f ? worldScale.z : worldScale.z / parentScale.z);
```

- [ ] **Step 5: Implement the rail-owned sequence**

Add to `ExecutionRailView`:

```csharp
[SerializeField] private float _placementFlightDuration = 0.45f;
[SerializeField] private float _placementFlightTiltDegrees = 12f;
[SerializeField, Range(0.1f, 0.9f)]
private float _placementFlightTiltRatio = 0.35f;
private Sequence _placementFlightSequence;

public bool TryGetPlacementFlightLayer(out RectTransform layer)
{
    layer = _previewLayer;
    return layer != null && _placementPreview != null
        && _placementPreview.gameObject.activeSelf;
}

public bool StartPlacementFlight(RectTransform flight, Action onComplete)
{
    if (flight == null || _placementPreview == null
        || !_placementPreview.gameObject.activeSelf || _previewLayer == null)
    {
        return false;
    }

    StopPlacementPulse();
    StopPlacementFlight();
    _placementPreview.SetInteractable(false);
    _placementPreviewGroup.interactable = false;
    _placementPreviewGroup.blocksRaycasts = false;
    _placementPreviewGroup.alpha = 0f;
    var target = (RectTransform)_placementPreview.transform;
    float tiltTime = _placementFlightDuration * _placementFlightTiltRatio;
    Vector3 targetEuler = target.eulerAngles;
    Vector3 endScale = ScaleForTarget(flight, target, _previewLayer.lossyScale);

    bool completionSent = false;
    Action finish = () =>
    {
        if (completionSent) return;
        completionSent = true;
        _placementFlightSequence = null;
        onComplete?.Invoke();
    };
    _placementFlightSequence = DOTween.Sequence()
        .Append(flight.DOMove(target.position, _placementFlightDuration)
            .SetEase(Ease.InOutCubic))
        .Join(flight.DOScale(endScale, _placementFlightDuration)
            .SetEase(Ease.InOutCubic))
        .Insert(0f, flight.DORotate(
            targetEuler + new Vector3(0f, 0f, _placementFlightTiltDegrees), tiltTime)
            .SetEase(Ease.OutSine))
        .Insert(tiltTime, flight.DORotate(
            targetEuler, _placementFlightDuration - tiltTime)
            .SetEase(Ease.InOutSine))
        .SetUpdate(true)
        .SetLink(flight.gameObject, LinkBehaviour.KillOnDestroy)
        .OnComplete(() => finish())
        .OnKill(() => finish());
    return true;
}

private void StopPlacementFlight()
{
    if (_placementFlightSequence == null) return;
    _placementFlightSequence.Kill();
    _placementFlightSequence = null;
}

private static Vector3 ScaleForTarget(
    RectTransform flight, RectTransform target, Vector3 parentScale)
    => new Vector3(
        target.rect.width * target.lossyScale.x /
            (flight.rect.width * parentScale.x),
        target.rect.height * target.lossyScale.y /
            (flight.rect.height * parentScale.y),
        1f);
```

Call `StopPlacementFlight()` at the start of `ClearPlacementPreview()`. Before deactivating `_placementPreview`, restore:

```csharp
_placementPreviewGroup.alpha = PlacementPreviewAlpha;
_placementPreviewGroup.interactable = false;
_placementPreviewGroup.blocksRaycasts = false;
```

- [ ] **Step 6: Verify GREEN and commit**

Run the Step 3 command, then:

```bash
dotnet build FateWeaver.Tests.UnityEditMode.csproj --no-restore
git diff --check -- Assets/Unity/HandFanView.cs Assets/Unity/ExecutionRailView.cs Assets/Tests/UnityEditMode/HandFanHoverTests.cs Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs
git add Assets/Unity/HandFanView.cs Assets/Unity/ExecutionRailView.cs Assets/Tests/UnityEditMode/HandFanHoverTests.cs Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs
git commit -m "feat(ui): animate card flight into execution rail"
```

Expected: focused tests pass and build has zero errors.

---

### Task 3: Delay placement refresh until the flight lands

**Files:**
- Modify: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`
- Modify: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/PLAYTEST.md`

**Interfaces:**
- Consumes: all Task 2 flight APIs.
- Preserves: `Initialize(Func<SelectionResult, bool>, Func<SelectionTargetKind, IReadOnlyList<SelectionTargetRef>>, Action)`.
- Produces: `_tryApply` executes once at click; `_machine.CommitSucceeded`, `EndSelectionVisuals`, `_onApplied` wait for flight completion.

- [ ] **Step 1: Upgrade controller fixture and write delayed-completion test**

Add `using UnityEditor;`. Store `_hand`, `_rail`, `_onAppliedCalls`. In `SetUp`, replace the empty hand with:

```csharp
var cardPrefab = AssetDatabase.LoadAssetAtPath<CardView>(
    "Assets/Unity/Prefabs/CardView.prefab");
Assert.IsNotNull(cardPrefab);
_hand = Child("Hand", typeof(RectTransform)).AddComponent<HandFanView>();
_hand.EditorBuild(cardPrefab);
_hand.SetCards(new[] { ExecutionPresentation() }, _ => { }, (_, __) => { });
_rail = Child("Rail", typeof(RectTransform)).AddComponent<ExecutionRailView>();
```

Use these fields in controller injection and set `() => _onAppliedCalls++` as the initialized completion callback. Reset the counter in `TearDown`.

Replace `Armed_silhouette_click_dispatches_targetless_placement_once`:

```csharp
[Test]
public void Armed_silhouette_applies_once_and_refreshes_only_after_flight_lands()
{
    var card = ExecutionPresentation();
    _controller.ShowPlacementHover(0, card, 0);
    _controller.BeginPlacement(0, card, 0);
    var preview = Field<RailCardView>(_rail, "_placementPreview");
    var button = Field<Button>(preview, "_button");
    button.onClick.Invoke();
    button.onClick.Invoke();

    Assert.AreEqual(1, _appliedResults.Count);
    Assert.IsTrue(_controller.SelectionActive);
    Assert.AreEqual(0, _onAppliedCalls);
    Assert.IsFalse(button.interactable);
    var sequence = Field<Sequence>(_rail, "_placementFlightSequence");
    Assert.IsTrue(sequence.IsActive());

    sequence.Complete();

    Assert.IsFalse(_controller.SelectionActive);
    Assert.AreEqual(1, _onAppliedCalls);
    Assert.IsNull(Field<Sequence>(_rail, "_placementFlightSequence"));
}
```

Add a controller-level selection pose regression test so the test fails if `BeginPlacement` omits hold or uses gold:

```csharp
[Test]
public void Begin_placement_holds_the_hand_card_in_blue_hover_pose()
{
    var cards = new[]
    {
        ExecutionPresentation(), ExecutionPresentation(), ExecutionPresentation()
    };
    _hand.SetCards(cards, _ => { }, (_, __) => { });
    var source = _hand.GetComponentsInChildren<CardView>()[0];

    _controller.ShowPlacementHover(0, cards[0], 0);
    _controller.BeginPlacement(0, cards[0], 0);

    Assert.AreEqual(Quaternion.identity, source.transform.localRotation);
    Assert.AreEqual(Vector3.one * 1.35f, source.transform.localScale);
    Assert.AreEqual(
        SelectedOutline,
        Field<Image>(source, "_selectionOutline").color);
}
```

Extend the rejected-placement test:

```csharp
Assert.IsNull(Field<Sequence>(_rail, "_placementFlightSequence"));
Assert.AreEqual(0, _onAppliedCalls);
Assert.AreEqual(0, _overlay.GetComponentsInChildren<CardView>(true).Length);
```

- [ ] **Step 2: Run controller tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck -runTests -testPlatform EditMode -testFilter FateWeaver.Tests.UnityEditMode.CardSelectionControllerTests -testResults /private/tmp/card-placement-controller-red.xml -logFile /private/tmp/card-placement-controller-red.log -quit
```

Expected: current placement ends immediately, so selection and callback timing assertions fail.

- [ ] **Step 3: Implement controller orchestration**

Add fields:

```csharp
private CardPresentation? _placementCard;
private HandFanView.PlacementFlightVisual _placementFlight;
private bool _placementCompleting;
```

Save `_placementCard = card;` in `BeginPlacement`. Replace `OnPlacementPreviewClicked` and add helpers:

```csharp
private void OnPlacementPreviewClicked()
{
    if (_placementCompleting || !_placementCard.HasValue) return;
    if (!_rail.TryGetPlacementFlightLayer(out var layer)
        || !_hand.TryPreparePlacementFlight(
            _visualHandIndex, _placementCard.Value, layer, out _placementFlight))
    {
        CancelSelection();
        return;
    }

    _placementCompleting = true;
    var result = _machine.ClickApplyArea();
    bool applied = result.IsComplete && _tryApply != null && _tryApply(result);
    if (!applied)
    {
        AbortPlacementCompletion();
        _machine.Cancel();
        EndSelectionVisuals();
        return;
    }

    _hand.SetInputEnabled(false);
    _hand.ShowPlacementFlight(_placementFlight);
    if (!_rail.StartPlacementFlight(_placementFlight.Rect, CompletePlacementFlight))
    {
        CompletePlacementFlight();
    }
}

private void AbortPlacementCompletion()
{
    _hand.ClearPlacementFlight(_placementFlight);
    _placementFlight = null;
    _placementCompleting = false;
}

private void CompletePlacementFlight()
{
    if (!_placementCompleting) return;
    _machine.CommitSucceeded();
    AbortPlacementCompletion();
    EndSelectionVisuals();
    _onApplied?.Invoke();
}
```

Guard `BeginPlacement`, `BeginTargetSelection`, `OnTargetClicked`, `CancelSelection`, and `OnConfirmClicked` with `if (_placementCompleting) return;`.

At the end of `EndSelectionVisuals`, restore placement-only state:

```csharp
_hand.ClearPlacementFlight(_placementFlight);
_placementFlight = null;
_placementCard = null;
_placementCompleting = false;
_hand.SetInputEnabled(true);
```

Keep `TryDispatch` unchanged for explicit target selections.

- [ ] **Step 4: Update manual playtest instructions**

Update the execution placement paragraph in `Assets/Unity/PLAYTEST.md`:

```markdown
손패 카드를 클릭하면 팬 회전이 풀린 호버 자세로 고정되고 푸른 테두리가 표시된다. 실행 카드의 고정
실루엣을 클릭하면 큰 손패 카드가 부드럽게 기울고 이동·축소되어 실루엣 자리에 안착한 뒤 실제 레일
카드로 교체된다. 빠르게 연속 클릭해도 카드와 운명력은 한 번만 소비되어야 한다.
```

- [ ] **Step 5: Run focused and full verification**

Run the Step 2 command, then:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ish/Git/rogue-deck -runTests -testPlatform EditMode -testResults /private/tmp/card-placement-motion-editmode.xml -logFile /private/tmp/card-placement-motion-editmode.log -quit
dotnet build FateWeaver.Tests.UnityEditMode.csproj --no-restore
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore
```

Expected: all Unity EditMode tests and headless tests pass with zero errors/failures. If Unity has the project open, run the EditMode tests in that Test Runner; a `.csproj` build alone is not sufficient to claim Unity tests passed.

- [ ] **Step 6: Commit Task 3**

```bash
git diff --check -- Assets/Unity/CardSelectionController.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Unity/PLAYTEST.md
git add Assets/Unity/CardSelectionController.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs Assets/Unity/PLAYTEST.md
git commit -m "feat(ui): land execution cards with placement motion"
```

## Final Review

- [ ] `git status --short`에 의도하지 않은 scene, prefab, font, 생성된 project 또는 test-result 변경이 없다.
- [ ] 실행 카드와 조작 카드가 모두 호버 자세와 푸른 테두리를 사용한다.
- [ ] `_tryApply`는 실루엣 클릭에 한 번, `_onApplied`는 비행 완료 뒤 한 번 호출된다.
- [ ] 거부된 배치에는 활성 sequence, 오버레이 카드, 투명 원본 카드가 남지 않는다.
- [ ] `DOTween.KillAll()` 없이 소유한 tween만 정리한다.
- [ ] Core 및 Simulation production 파일은 변경되지 않는다.
