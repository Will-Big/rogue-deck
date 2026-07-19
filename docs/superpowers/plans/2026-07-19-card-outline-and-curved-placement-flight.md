# Card Outline and Curved Placement Flight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 선택한 전체 크기 카드에는 프레임 외곽선만 표시하고, 실행 카드는 손패에서 레일 실루엣까지 두 구간 Bézier와 접선 회전을 따라 이동하게 한다.

**Architecture:** `CardView`는 루트 프레임의 직렬화된 uGUI `Outline`만 제어해 중앙 채움을 제거한다. 새 `PlacementFlightPath`가 두 Bézier 구간과 접선을 계산하고, `ExecutionRailView`는 DOTween으로 정규화 진행률과 축소를 재생하며 계산 결과를 비행 카드에 적용한다.

**Tech Stack:** Unity 6000.5.2f1, C# 9, uGUI, DOTween, NUnit Unity EditMode tests, .NET 5 headless tests

## Global Constraints

- 현재 브랜치 `card-selection-placement-motion`에서 작업하며 git worktree를 사용하지 않는다.
- 사용자 작업 중인 카드 SO, `GeneratedCards.cs`, 폰트와 새 적 카드 에셋을 수정·스테이징하지 않는다.
- `.superpowers/` 시각 비교 세션 파일을 커밋하지 않는다.
- 런타임 `new GameObject`, 문자열 탐색, 하드코딩된 에셋 경로와 새 외부 패키지를 추가하지 않는다.
- 선택 효과는 `CardView.prefab`에 저장한 `UnityEngine.UI.Outline`을 `[SerializeField] private` 참조로 사용한다.
- 레일 미니 카드와 배치 실루엣의 기존 선택 표현은 변경하지 않는다.
- 비행 경로의 모양과 속도 수치는 `ExecutionRailView`의 `[SerializeField] private` 튜닝값으로 둔다.
- `DOTween.KillAll()` 없이 `ExecutionRailView`가 소유한 sequence만 종료한다.
- Core와 Simulation production 코드는 이 기능 구현에 포함하지 않는다.
- 각 production 변경 전에 대응 Unity EditMode 테스트를 추가하고 의도한 RED를 확인한다.

---

## File Map

- Modify `Assets/Unity/CardView.cs`: 선택 상태를 루트 프레임 `Outline`의 활성화와 색으로 표현한다.
- Modify `Assets/Unity/Prefabs/CardView.prefab`: 루트 프레임에 `Outline`을 저장하고 전체 Rect 채움 자식을 제거한다.
- Modify `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`: 푸른 외곽선, 원본 프레임 색 유지와 중앙 채움 제거를 검증한다.
- Modify `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`: 컨트롤러 선택 단언을 새 `Outline` 계약으로 갱신한다.
- Create `Assets/Unity/PlacementFlightPath.cs`: 두 Bézier 구간, 위치와 접선 샘플을 계산한다.
- Create `Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs`: 시계 방향 접선과 실루엣 하단 제한을 순수 좌표로 검증한다.
- Modify `Assets/Unity/ExecutionRailView.cs`: 직선 이동과 별도 기울기를 경로 진행률 tween으로 교체한다.
- Modify `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`: 실제 sequence가 직선을 벗어나고 목표 자세로 안착하는지 검증한다.
- Modify `Assets/Unity/PLAYTEST.md`: 외곽선과 최종 곡선 수동 확인 절차를 기록한다.

---

### Task 1: Replace the full-card fill with a frame Outline

**Files:**
- Modify: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`
- Modify: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`
- Modify: `Assets/Unity/CardView.cs`
- Modify: `Assets/Unity/Prefabs/CardView.prefab`

**Interfaces:**
- Consumes: `CardView.SetSelection(CardView.SelectionKind)` and the root frame `Image` already stored on `CardView.prefab`.
- Produces: `[SerializeField] private Outline _selectionOutline`; `None` disables it, `Primary` and `Secondary` enable it with their existing colors.
- Preserves: `HandFanView.SetSelection`, `HandFanView.SetTargetSelection`, and every caller of `CardView.SetSelection`.

- [ ] **Step 1: Write the failing prefab and selection tests**

In `HandFanHoverTests.cs`, replace `Target_selected_hand_card_uses_blue_outline` with the following test and add the reset test immediately after it:

```csharp
private static readonly Color GoldOutline =
    new Color(0.95f, 0.72f, 0.25f, 1f);

[Test]
public void Target_selected_hand_card_uses_only_the_blue_frame_outline()
{
    var root = new GameObject("Hand", typeof(RectTransform));
    try
    {
        var hand = BuildHand(root, ThreeCards());
        var selected = root.GetComponentsInChildren<CardView>()[0];
        var frame = selected.GetComponent<Image>();
        Color originalFrameColor = frame.color;

        hand.SetTargetSelection(0, true);

        var outline = Field<Outline>(selected, "_selectionOutline");
        Assert.AreSame(selected.gameObject, outline.gameObject);
        Assert.IsTrue(outline.enabled);
        Assert.AreEqual(BlueOutline, outline.effectColor);
        Assert.AreEqual(originalFrameColor, frame.color);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}

[Test]
public void Primary_selection_uses_the_gold_frame_outline()
{
    var root = new GameObject("Hand", typeof(RectTransform));
    try
    {
        var hand = BuildHand(root, ThreeCards());
        var selected = root.GetComponentsInChildren<CardView>()[0];

        hand.SetSelection(0, CardView.SelectionKind.Primary);

        var outline = Field<Outline>(selected, "_selectionOutline");
        Assert.IsTrue(outline.enabled);
        Assert.AreEqual(GoldOutline, outline.effectColor);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}

[Test]
public void Clearing_card_selection_disables_the_frame_outline()
{
    var root = new GameObject("Hand", typeof(RectTransform));
    try
    {
        var hand = BuildHand(root, ThreeCards());
        var selected = root.GetComponentsInChildren<CardView>()[0];

        hand.SetSelection(0, CardView.SelectionKind.Secondary);
        hand.SetSelection(-1, CardView.SelectionKind.None);

        Assert.IsFalse(Field<Outline>(selected, "_selectionOutline").enabled);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

In `CardSelectionControllerTests.cs`, change the outline assertion in
`Begin_placement_holds_the_hand_card_in_blue_hover_pose`:

```csharp
var outline = Field<Outline>(source, "_selectionOutline");
Assert.IsTrue(outline.enabled);
Assert.AreEqual(SelectedOutline, outline.effectColor);
```

The files already import `UnityEngine.UI`, which defines both `Image` and `Outline`.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.HandFanHoverTests \
  -testResults /private/tmp/card-outline-red.xml \
  -logFile /private/tmp/card-outline-red.log
```

Expected: `Target_selected_hand_card_uses_only_the_blue_frame_outline` fails because `_selectionOutline` is still a full-Rect `Image`, not a root `Outline`.

- [ ] **Step 3: Implement the `CardView` Outline state contract**

In `CardView.cs`, change the serialized field type:

```csharp
[SerializeField] private Outline _selectionOutline;
```

Remove `OutlineNone`, then replace `SetSelection` with:

```csharp
public void SetSelection(SelectionKind kind)
{
    if (kind == SelectionKind.None)
    {
        _selectionOutline.enabled = false;
        return;
    }

    _selectionOutline.effectColor = kind == SelectionKind.Primary
        ? OutlinePrimary
        : OutlineSecondary;
    _selectionOutline.enabled = true;
}
```

Delete `LayoutSelectionOutline(scale);` from `ApplyResponsiveLayout` and delete the entire
`LayoutSelectionOutline(float scale)` method. `Outline.effectDistance` belongs to the prefab and scales with the card.

- [ ] **Step 4: Replace the prefab fill object with a root Outline**

Edit `CardView.prefab` without rebuilding the scene:

1. Remove the `SelectionOutline` GameObject, RectTransform, CanvasRenderer and `Image` blocks with file IDs
   `4109107120531430584`, `5769019174760968668`, `4350190791041173590`, and `5255540204861092008`.
2. Remove `{fileID: 5769019174760968668}` from the root RectTransform child list.
3. Add a new root component file ID and serialize Unity uGUI `Outline` with package script GUID
   `e19747de3f5aca642ab2be37e372fb86`:

```yaml
--- !u!114 &9001000000000000001
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 9145218196501635394}
  m_Enabled: 0
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e19747de3f5aca642ab2be37e372fb86, type: 3}
  m_Name:
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Outline
  m_EffectColor: {r: 0.35, g: 0.75, b: 0.95, a: 1}
  m_EffectDistance: {x: 3, y: -3}
  m_UseGraphicAlpha: 1
```

4. Add `{fileID: 9001000000000000001}` to the root GameObject component list after the root `Image`.
5. Change the `CardView` serialized reference to:

```yaml
  _selectionOutline: {fileID: 9001000000000000001}
```

Do not touch `RailCardView.prefab`; its blue placement silhouette is intentionally unchanged.

- [ ] **Step 5: Run focused tests and the Unity C# build to verify GREEN**

Run the Step 2 Unity command again. Expected: all `HandFanHoverTests` pass.

Then run:

```bash
dotnet build FateWeaver.Tests.UnityEditMode.csproj --no-restore
```

Expected: 0 errors. Existing Unity analyzer/reference compatibility warnings may remain.

Run the controller fixture as a second focused check:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardSelectionControllerTests \
  -testResults /private/tmp/card-outline-controller-green.xml \
  -logFile /private/tmp/card-outline-controller-green.log
```

Expected: all controller tests pass, including the blue held-card assertion.

- [ ] **Step 6: Commit Task 1 only**

```bash
git diff --check -- \
  Assets/Unity/CardView.cs \
  Assets/Unity/Prefabs/CardView.prefab \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs \
  Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs
git add \
  Assets/Unity/CardView.cs \
  Assets/Unity/Prefabs/CardView.prefab \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs \
  Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs
git commit -m "fix(ui): render card selection as frame outline"
```

Expected: the commit contains only the four listed files. User-owned SO, generated code, font files and `.superpowers/` remain unstaged.

---

### Task 2: Animate placement along a two-segment Bézier path

**Files:**
- Create: `Assets/Unity/PlacementFlightPath.cs`
- Create: `Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs`
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Modify: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`
- Modify: `Assets/Unity/PLAYTEST.md`

**Interfaces:**
- Produces: `PlacementFlightPath.Settings`, `PlacementFlightPath.Geometry`, `PlacementFlightPath.Sample`.
- Produces: `PlacementFlightPath.Create(Vector2, Vector2, Vector2, Settings)` and
  `PlacementFlightPath.Evaluate(Geometry, float, float)`.
- Consumes: `ExecutionRailView.StartPlacementFlight(RectTransform, Action)` without changing its public signature.
- Preserves: placement input lock, silhouette alpha, flight scale, `SetUpdate(true)`, `SetLink`, and exactly-once completion.

- [ ] **Step 1: Write failing path geometry tests**

Create `PlacementFlightPathTests.cs`:

```csharp
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    public class PlacementFlightPathTests
    {
        private const float Split = 0.72f;
        private PlacementFlightPath.Geometry _geometry;

        [SetUp]
        public void SetUp()
        {
            var settings = new PlacementFlightPath.Settings(
                riseRatio: 0.7f,
                overshootRatio: 0.9f,
                approachWidthRatio: 1.25f,
                approachDropRatio: 0.3f);
            _geometry = PlacementFlightPath.Create(
                new Vector2(0f, -300f),
                new Vector2(0f, 100f),
                new Vector2(96f, 132f),
                settings);
        }

        [Test]
        public void Tangents_follow_12_to_2_to_10_to_9_then_finish_at_12()
        {
            var start = PlacementFlightPath.Evaluate(_geometry, 0f, Split);
            var early = PlacementFlightPath.Evaluate(_geometry, Split * 0.25f, Split);
            var rewind = PlacementFlightPath.Evaluate(_geometry, Split * 0.95f, Split);
            var firstEnd = PlacementFlightPath.Evaluate(_geometry, Split, Split);
            var secondStart = PlacementFlightPath.Evaluate(
                _geometry, Split + 0.0001f, Split);
            var end = PlacementFlightPath.Evaluate(_geometry, 1f, Split);

            Assert.That(Vector2.Angle(Vector2.up, start.Tangent), Is.LessThan(0.01f));
            Assert.Greater(early.Tangent.x, 0f, "early tangent should turn toward 2 o'clock");
            Assert.Less(rewind.Tangent.x, 0f, "late first segment should turn toward 10 o'clock");
            Assert.Greater(rewind.Tangent.y, 0f);
            Assert.That(Vector2.Angle(Vector2.left, firstEnd.Tangent), Is.LessThan(0.01f));
            Assert.That(Vector2.Angle(Vector2.left, secondStart.Tangent), Is.LessThan(0.1f));
            Assert.That(Vector2.Angle(Vector2.up, end.Tangent), Is.LessThan(0.01f));
        }

        [Test]
        public void Final_segment_never_drops_below_the_silhouette_bottom()
        {
            float silhouetteBottom = 100f - 132f * 0.5f;
            for (int i = 0; i <= 20; i++)
            {
                float progress = Mathf.Lerp(Split, 1f, i / 20f);
                var sample = PlacementFlightPath.Evaluate(_geometry, progress, Split);
                Assert.GreaterOrEqual(sample.Position.y, silhouetteBottom);
            }
        }

        [Test]
        public void End_sample_matches_the_target_and_zero_degree_rotation()
        {
            var end = PlacementFlightPath.Evaluate(_geometry, 1f, Split);

            Assert.AreEqual(new Vector2(0f, 100f), end.Position);
            Assert.That(Mathf.Abs(end.AngleDegrees), Is.LessThan(0.01f));
        }
    }
}
```

- [ ] **Step 2: Write the failing rail integration assertion**

In `ExecutionRailInputTests.Placement_flight_hides_silhouette_and_settles_at_its_pose`, capture the start and target before starting the flight, place the flight below the target, and assert mid-flight curvature before completing:

```csharp
var preview = Field<RailCardView>(rail, "_placementPreview");
flight.position = preview.transform.position + Vector3.down * 300f;
Vector3 startPosition = flight.position;
Vector3 targetPosition = preview.transform.position;

Assert.IsTrue(rail.StartPlacementFlight(
    flight, () => completed = true));

var sequence = Field<Sequence>(rail, "_placementFlightSequence");
float duration = Field<float>(rail, "_placementFlightDuration");
sequence.Goto(duration * 0.35f, false);
Vector3 straightPoint = Vector3.Lerp(startPosition, targetPosition, 0.35f);
Assert.That(Mathf.Abs(flight.position.x - straightPoint.x), Is.GreaterThan(5f));
Assert.That(Mathf.Abs(Mathf.DeltaAngle(flight.eulerAngles.z, 0f)), Is.GreaterThan(5f));

sequence.Complete();
```

Keep the existing silhouette alpha, input lock, completion, final position, final rotation and sequence cleanup assertions. Add this final-size assertion after `sequence.Complete()`:

```csharp
var targetRect = (RectTransform)preview.transform;
Assert.That(
    Mathf.Abs(
        flight.rect.width * flight.lossyScale.x
        - targetRect.rect.width * targetRect.lossyScale.x),
    Is.LessThan(0.01f));
```

- [ ] **Step 3: Run the new tests and verify RED**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.PlacementFlightPathTests \
  -testResults /private/tmp/placement-flight-path-red.xml \
  -logFile /private/tmp/placement-flight-path-red.log
```

Expected: compile failure because `PlacementFlightPath` does not exist.

After the test assembly compiles in later steps, the rail integration assertion must also fail against the current straight `DOMove` because its X coordinate stays on the straight line.

- [ ] **Step 4: Implement the tested path math**

Create `PlacementFlightPath.cs`:

```csharp
using UnityEngine;

namespace FateWeaver.Unity
{
    public static class PlacementFlightPath
    {
        private const float MinDimension = 0.001f;
        private const float MinSegmentRatio = 0.01f;
        private const float MaxSegmentRatio = 0.99f;
        private const float MaxApproachDropRatio = 0.49f;

        public readonly struct Settings
        {
            public Settings(
                float riseRatio,
                float overshootRatio,
                float approachWidthRatio,
                float approachDropRatio)
            {
                RiseRatio = riseRatio;
                OvershootRatio = overshootRatio;
                ApproachWidthRatio = approachWidthRatio;
                ApproachDropRatio = approachDropRatio;
            }

            public float RiseRatio { get; }
            public float OvershootRatio { get; }
            public float ApproachWidthRatio { get; }
            public float ApproachDropRatio { get; }
        }

        public readonly struct Geometry
        {
            internal Geometry(
                Vector2 start,
                Vector2 firstControl,
                Vector2 secondControl,
                Vector2 approach,
                Vector2 settleControl,
                Vector2 target)
            {
                Start = start;
                FirstControl = firstControl;
                SecondControl = secondControl;
                Approach = approach;
                SettleControl = settleControl;
                Target = target;
            }

            public Vector2 Start { get; }
            public Vector2 FirstControl { get; }
            public Vector2 SecondControl { get; }
            public Vector2 Approach { get; }
            public Vector2 SettleControl { get; }
            public Vector2 Target { get; }
        }

        public readonly struct Sample
        {
            internal Sample(Vector2 position, Vector2 tangent)
            {
                Position = position;
                Tangent = tangent.sqrMagnitude > MinDimension * MinDimension
                    ? tangent.normalized
                    : Vector2.up;
                AngleDegrees = Vector2.SignedAngle(Vector2.up, Tangent);
            }

            public Vector2 Position { get; }
            public Vector2 Tangent { get; }
            public float AngleDegrees { get; }
        }

        public static Geometry Create(
            Vector2 start,
            Vector2 target,
            Vector2 targetSize,
            Settings settings)
        {
            float width = Mathf.Max(Mathf.Abs(targetSize.x), MinDimension);
            float height = Mathf.Max(Mathf.Abs(targetSize.y), MinDimension);
            float verticalGap = Mathf.Max(target.y - start.y, height);
            float dropRatio = Mathf.Clamp(
                settings.ApproachDropRatio, 0f, MaxApproachDropRatio);
            Vector2 approach = target
                + Vector2.right * width * settings.ApproachWidthRatio
                + Vector2.down * height * dropRatio;
            Vector2 firstControl = start
                + Vector2.up * verticalGap * settings.RiseRatio;
            Vector2 secondControl = approach
                + Vector2.right * width * settings.OvershootRatio;
            Vector2 settleControl = new Vector2(target.x, approach.y);
            return new Geometry(
                start,
                firstControl,
                secondControl,
                approach,
                settleControl,
                target);
        }

        public static Sample Evaluate(
            Geometry geometry,
            float progress,
            float segmentSplit)
        {
            float split = Mathf.Clamp(
                segmentSplit, MinSegmentRatio, MaxSegmentRatio);
            float clamped = Mathf.Clamp01(progress);
            if (clamped <= split)
            {
                float t = clamped / split;
                return new Sample(
                    Cubic(
                        geometry.Start,
                        geometry.FirstControl,
                        geometry.SecondControl,
                        geometry.Approach,
                        t),
                    CubicDerivative(
                        geometry.Start,
                        geometry.FirstControl,
                        geometry.SecondControl,
                        geometry.Approach,
                        t));
            }

            float settleT = (clamped - split) / (1f - split);
            return new Sample(
                Quadratic(
                    geometry.Approach,
                    geometry.SettleControl,
                    geometry.Target,
                    settleT),
                QuadraticDerivative(
                    geometry.Approach,
                    geometry.SettleControl,
                    geometry.Target,
                    settleT));
        }

        private static Vector2 Cubic(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * p0
                + 3f * inverse * inverse * t * p1
                + 3f * inverse * t * t * p2
                + t * t * t * p3;
        }

        private static Vector2 CubicDerivative(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * (p1 - p0)
                + 6f * inverse * t * (p2 - p1)
                + 3f * t * t * (p3 - p2);
        }

        private static Vector2 Quadratic(
            Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * p0
                + 2f * inverse * t * p1
                + t * t * p2;
        }

        private static Vector2 QuadraticDerivative(
            Vector2 p0, Vector2 p1, Vector2 p2, float t)
            => 2f * (1f - t) * (p1 - p0)
                + 2f * t * (p2 - p1);
    }
}
```

- [ ] **Step 5: Run the path tests and verify GREEN**

Run the Step 3 command again.

Expected: all three `PlacementFlightPathTests` pass. Confirm the first test proves both segments share the 9시 tangent and the final sample reaches 12시.

- [ ] **Step 6: Replace straight movement and separate tilt with path progress**

In `ExecutionRailView.cs`, replace the two tilt fields with serialized curve tuning values:

```csharp
[SerializeField, Range(0.1f, 0.95f)]
private float _placementFlightRiseRatio = 0.7f;
[SerializeField, Min(0f)]
private float _placementFlightOvershootRatio = 0.9f;
[SerializeField, Min(0.1f)]
private float _placementFlightApproachWidthRatio = 1.25f;
[SerializeField, Range(0.05f, 0.45f)]
private float _placementFlightApproachDropRatio = 0.3f;
[SerializeField, Range(0.5f, 0.9f)]
private float _placementFlightCurveSplit = 0.72f;
```

Keep `_placementFlightDuration` as the single duration tuning field. In `StartPlacementFlight`, replace
`tiltTime`, `targetEuler`, `DOMove`, and both `DORotate` tweens with:

```csharp
var target = (RectTransform)_placementPreview.transform;
Vector3 startLocal = flight.localPosition;
Vector3 targetLocal3 = _previewLayer.InverseTransformPoint(target.position);
Vector2 targetSize = SizeInLayer(target, _previewLayer);
var settings = new PlacementFlightPath.Settings(
    _placementFlightRiseRatio,
    _placementFlightOvershootRatio,
    _placementFlightApproachWidthRatio,
    _placementFlightApproachDropRatio);
var path = PlacementFlightPath.Create(
    new Vector2(startLocal.x, startLocal.y),
    new Vector2(targetLocal3.x, targetLocal3.y),
    targetSize,
    settings);
Vector3 endScale = ScaleForTarget(flight, target, _previewLayer.lossyScale);
float progress = 0f;

var movement = DOTween.To(
        () => progress,
        value =>
        {
            progress = value;
            var sample = PlacementFlightPath.Evaluate(
                path, value, _placementFlightCurveSplit);
            flight.localPosition = new Vector3(
                sample.Position.x, sample.Position.y, startLocal.z);
            flight.localRotation = Quaternion.Euler(
                0f, 0f, sample.AngleDegrees);
        },
        1f,
        _placementFlightDuration)
    .SetEase(Ease.InOutCubic);
```

Build the sequence with the existing exactly-once `finish` closure:

```csharp
_placementFlightSequence = DOTween.Sequence()
    .Append(movement)
    .Join(flight.DOScale(endScale, _placementFlightDuration)
        .SetEase(Ease.InOutCubic))
    .AppendCallback(() =>
    {
        flight.SetPositionAndRotation(target.position, target.rotation);
        flight.localScale = endScale;
    })
    .SetUpdate(true)
    .SetLink(flight.gameObject, LinkBehaviour.KillOnDestroy)
    .OnComplete(() => finish())
    .OnKill(() => finish());
```

Add this private helper near `ScaleForTarget`:

```csharp
private static Vector2 SizeInLayer(
    RectTransform target,
    RectTransform layer)
{
    var corners = new Vector3[4];
    target.GetWorldCorners(corners);
    Vector3 bottomLeft = layer.InverseTransformPoint(corners[0]);
    Vector3 topRight = layer.InverseTransformPoint(corners[2]);
    return new Vector2(
        Mathf.Abs(topRight.x - bottomLeft.x),
        Mathf.Abs(topRight.y - bottomLeft.y));
}
```

Do not change the existing validation, silhouette alpha/input lock, `finish` guard, `StopPlacementFlight`, or cleanup code.

- [ ] **Step 7: Run path and rail tests and verify GREEN**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.PlacementFlightPathTests \
  -testResults /private/tmp/placement-flight-path-green.xml \
  -logFile /private/tmp/placement-flight-path-green.log

/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests \
  -testResults /private/tmp/placement-flight-rail-green.xml \
  -logFile /private/tmp/placement-flight-rail-green.log
```

Expected: all path and rail tests pass. The rail test must observe nonzero X curvature during flight and exact target position/rotation on completion.

- [ ] **Step 8: Update manual playtest instructions**

Replace the integrated placement checklist wording in `Assets/Unity/PLAYTEST.md` with:

```markdown
손패 카드를 클릭하면 회전 없는 호버 자세로 고정되고 카드 내부 색은 유지된 채 프레임 외곽선만
푸르게 표시된다. 실행 카드 실루엣을 클릭하면 큰 카드가 하단 손패에서 출발해 숫자 3의 윗고리처럼
12시→2시→10시→9시 방향으로 감긴다. 9시 이후에는 실루엣 아래로 내려가지 않고 12시 정방향으로
풀리며 축소·안착한 뒤 실제 레일 카드로 전환된다. 연속 클릭해도 적용과 갱신은 한 번만 일어나야 한다.
```

- [ ] **Step 9: Run full verification**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck \
  -runTests \
  -testPlatform EditMode \
  -testResults /private/tmp/card-outline-curve-editmode.xml \
  -logFile /private/tmp/card-outline-curve-editmode.log

dotnet build FateWeaver.Tests.UnityEditMode.csproj --no-restore

dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 \
  --no-restore
```

Expected:

- Unity EditMode XML reports `failed="0"`.
- Unity C# build reports `0 Error(s)`; existing analyzer/reference warnings may remain.
- Headless tests report `Failed: 0`.
- If user-owned generated card edits cause a headless content mismatch, report the exact failure and do not modify or revert those files as part of this UI task.

- [ ] **Step 10: Commit Task 2 only**

First confirm Unity generated the two new `.meta` files. Then run:

```bash
git diff --check -- \
  Assets/Unity/PlacementFlightPath.cs \
  Assets/Unity/PlacementFlightPath.cs.meta \
  Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs \
  Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs.meta \
  Assets/Unity/ExecutionRailView.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs \
  Assets/Unity/PLAYTEST.md
git add \
  Assets/Unity/PlacementFlightPath.cs \
  Assets/Unity/PlacementFlightPath.cs.meta \
  Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs \
  Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs.meta \
  Assets/Unity/ExecutionRailView.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs \
  Assets/Unity/PLAYTEST.md
git commit -m "feat(ui): curve execution cards into the rail"
```

Expected: the commit contains only the seven listed paths. User-owned content and `.superpowers/` remain unstaged.

---

## Final Review

- [ ] `CardView` Secondary selection activates a blue root `Outline`, not a full-Rect color `Image`.
- [ ] `CardView` None selection disables the effect and leaves the frame `Image.color` untouched.
- [ ] `RailCardView` and placement silhouette behavior remain unchanged.
- [ ] First path segment starts at 12시, turns right, rewinds up-left, and ends at 9시.
- [ ] Second path segment starts at 9시, stays above the silhouette bottom, and ends at 12시.
- [ ] Flight completion snaps to the exact target pose and calls refresh once.
- [ ] No `DOTween.KillAll()`, runtime object construction, string search, new package, Core rule change, scene change or font change is included.
- [ ] `git status --short` shows only pre-existing user content changes plus the untracked visual companion directory after feature commits.
