# Placement Flight Card Flip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 배치 비행의 안착 구간에서 카드가 Y축 플립으로 텍스트 카드에서 레일 미니 카드 면으로 전환되어 착지하게 한다.

**Architecture:** `PlacementFlightPath`에 순수 플립 각도 함수(`FlipAngle`, `SettleProgress`)를 추가하고, `ExecutionRailView.StartPlacementFlight`가 비행 Rect 자식으로 미니 카드 면(RailCardView 클론)을 준비해 90° 시점에 활성화하며 기존 진행률 tween 콜백에서 Y 플립을 Z 접선 회전과 합성한다.

**Tech Stack:** Unity 6000.5.2f1, C# 9, uGUI, DOTween, NUnit Unity EditMode tests

**Spec:** `docs/superpowers/specs/2026-07-19-placement-flight-flip-design.md`

## Global Constraints

- 전용 워크트리 `/Users/ish/Git/rogue-deck/.claude/worktrees/card-outline-curved-placement-cf7ba9` (브랜치 `claude/card-outline-curved-placement-cf7ba9`)에서 작업한다.
- Unity `-batchmode` 테스트의 `-projectPath`는 반드시 위 워크트리 경로를 사용한다 (메인 체크아웃 금지).
- 배치 실행이 변경하는 `ProjectSettings/ProjectSettings.asset`은 스테이징하지 않고 마지막에 `git checkout --`으로 되돌린다.
- 런타임 `new GameObject`, 문자열 탐색, 하드코딩된 에셋 경로, 새 외부 패키지를 추가하지 않는다. 프리팹 `Instantiate`는 기존 패턴대로 허용.
- `DOTween.KillAll()` 없이 `ExecutionRailView`가 소유한 sequence만 종료한다. 기존 sequence 구조(`SetUpdate(true)`, `SetLink`, exactly-once `finish`)를 바꾸지 않는다.
- 새 튜닝값을 추가하지 않는다. 플립 구간은 기존 `_placementFlightCurveSplit`에 종속된다.
- 매직 넘버 금지: 90° 교체 시점은 `PlacementFlightPath.FlipSwapProgress` 상수로 둔다.
- Core·Simulation·프리팹·씬 변경 없음.
- 각 production 변경 전에 대응 Unity EditMode 테스트를 추가하고 의도한 RED를 확인한다.

---

## File Map

- Modify `Assets/Unity/PlacementFlightPath.cs`: `FlipSwapProgress` 상수, `SettleProgress`, `FlipAngle` 순수 함수를 추가한다.
- Modify `Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs`: 플립 각도와 안착 진행률 경계값을 검증한다.
- Modify `Assets/Unity/ExecutionRailView.cs`: 미니 카드 면 생성·활성화와 Y 플립 회전 합성을 추가한다.
- Modify `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`: 플립 전 미니 면 비활성 / 90° 이후 활성 + Y 회전 범위 / 착지 Y=0을 검증한다.
- Modify `Assets/Unity/PLAYTEST.md`: 플립 수동 확인 절차를 기록한다.

---

### Task 1: Pure flip math on PlacementFlightPath

**Files:**
- Modify: `Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs`
- Modify: `Assets/Unity/PlacementFlightPath.cs`

**Interfaces:**
- Produces: `PlacementFlightPath.FlipSwapProgress` (`public const float`, 0.5f),
  `PlacementFlightPath.SettleProgress(float progress, float segmentSplit)` → `float` (첫 구간 0, 안착 구간 0→1),
  `PlacementFlightPath.FlipAngle(float settleT)` → `float` (0→90, 점프, -90→0).
- Consumes: 기존 `MinSegmentRatio`/`MaxSegmentRatio` 상수 (Evaluate와 동일한 클램프).
- Preserves: `Create`, `Evaluate`, `Settings`, `Geometry`, `Sample` — 시그니처와 동작 불변.

- [ ] **Step 1: Write the failing flip math tests**

`PlacementFlightPathTests.cs`의 클래스 끝에 다음 테스트를 추가한다:

```csharp
[Test]
public void Flip_angle_rises_to_edge_on_then_unfolds_to_zero()
{
    Assert.That(PlacementFlightPath.FlipAngle(0f), Is.EqualTo(0f).Within(0.001f));
    Assert.That(PlacementFlightPath.FlipAngle(0.25f), Is.EqualTo(45f).Within(0.001f));
    Assert.That(PlacementFlightPath.FlipAngle(0.4999f), Is.EqualTo(89.982f).Within(0.01f));
    Assert.That(PlacementFlightPath.FlipAngle(0.5f), Is.EqualTo(-90f).Within(0.001f));
    Assert.That(PlacementFlightPath.FlipAngle(0.75f), Is.EqualTo(-45f).Within(0.001f));
    Assert.That(PlacementFlightPath.FlipAngle(1f), Is.EqualTo(0f).Within(0.001f));
}

[Test]
public void Flip_angle_clamps_out_of_range_progress()
{
    Assert.That(PlacementFlightPath.FlipAngle(-1f), Is.EqualTo(0f).Within(0.001f));
    Assert.That(PlacementFlightPath.FlipAngle(2f), Is.EqualTo(0f).Within(0.001f));
}

[Test]
public void Settle_progress_is_zero_on_the_first_segment_and_normalized_after()
{
    Assert.That(PlacementFlightPath.SettleProgress(0f, Split), Is.EqualTo(0f).Within(0.001f));
    Assert.That(PlacementFlightPath.SettleProgress(Split, Split), Is.EqualTo(0f).Within(0.001f));
    Assert.That(
        PlacementFlightPath.SettleProgress(Split + (1f - Split) * 0.5f, Split),
        Is.EqualTo(0.5f).Within(0.001f));
    Assert.That(PlacementFlightPath.SettleProgress(1f, Split), Is.EqualTo(1f).Within(0.001f));
}
```

- [ ] **Step 2: Run the path tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.claude/worktrees/card-outline-curved-placement-cf7ba9 \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.PlacementFlightPathTests \
  -testResults /private/tmp/flight-flip-math-red.xml \
  -logFile /private/tmp/flight-flip-math-red.log
```

Expected: `FlipAngle`/`SettleProgress`가 없어 컴파일 실패 (RED).

- [ ] **Step 3: Implement the flip math**

`PlacementFlightPath.cs`의 `Evaluate` 메서드 바로 아래에 추가한다:

```csharp
public const float FlipSwapProgress = 0.5f;

/// <summary>0 on the first segment; normalized 0→1 across the settle segment,
/// using the same split clamp as Evaluate.</summary>
public static float SettleProgress(float progress, float segmentSplit)
{
    float split = Mathf.Clamp(
        segmentSplit, MinSegmentRatio, MaxSegmentRatio);
    float clamped = Mathf.Clamp01(progress);
    return clamped <= split
        ? 0f
        : (clamped - split) / (1f - split);
}

/// <summary>Y-axis flip: the front face turns 0→90° until FlipSwapProgress,
/// then the mini face unfolds -90°→0°. Both halves meet edge-on, so the swap
/// is invisible and the flight lands at exactly 0°.</summary>
public static float FlipAngle(float settleT)
{
    float clamped = Mathf.Clamp01(settleT);
    return clamped < FlipSwapProgress
        ? clamped * 180f
        : clamped * 180f - 180f;
}
```

- [ ] **Step 4: Run the path tests and verify GREEN**

Step 2의 명령을 `-testResults /private/tmp/flight-flip-math-green.xml -logFile /private/tmp/flight-flip-math-green.log`로 다시 실행한다.

Expected: `PlacementFlightPathTests` 전체(기존 3 + 신규 3) 통과.

- [ ] **Step 5: Commit Task 1 only**

```bash
git diff --check -- \
  Assets/Unity/PlacementFlightPath.cs \
  Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs
git add \
  Assets/Unity/PlacementFlightPath.cs \
  Assets/Tests/UnityEditMode/PlacementFlightPathTests.cs
git commit -m "feat(ui): add placement flight flip angle math"
```

Expected: 커밋에 위 2개 파일만 포함. `ProjectSettings.asset`은 스테이징하지 않는다.

---

### Task 2: Flip the flight card into the mini face

**Files:**
- Modify: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Modify: `Assets/Unity/PLAYTEST.md`

**Interfaces:**
- Consumes: `PlacementFlightPath.SettleProgress(float, float)`, `PlacementFlightPath.FlipAngle(float)`,
  `PlacementFlightPath.FlipSwapProgress` (Task 1), `RailCardView.Bind(CardPresentation, Action, Action<bool>)`,
  `RailCardView.SetInteractable(bool)`.
- Produces: `ExecutionRailView.StartPlacementFlight`가 비행 Rect 자식으로 비활성 `RailCardView` 미니 면을
  생성하고, 안착 구간 `settleT >= FlipSwapProgress`부터 활성화한다. 공개 시그니처 불변.
- Preserves: 배치 입력 잠금, 실루엣 알파, 비행 축소, `SetUpdate(true)`, `SetLink`, exactly-once 완료,
  착지 스냅 콜백, `StopPlacementFlight`.

- [ ] **Step 1: Write the failing rail flip test**

`ExecutionRailInputTests.cs`의 `Placement_flight_hides_silhouette_and_settles_at_its_pose` 테스트 뒤에 추가한다:

```csharp
[Test]
public void Placement_flight_flips_to_the_mini_card_face_in_the_settle_segment()
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
        var preview = Field<RailCardView>(rail, "_placementPreview");
        flight.position = preview.transform.position + Vector3.down * 300f;

        Assert.IsTrue(rail.StartPlacementFlight(flight, () => { }));

        var miniFace = flight.GetComponentInChildren<RailCardView>(true);
        Assert.IsNotNull(miniFace, "flight should carry a hidden mini card face");
        Assert.IsFalse(miniFace.gameObject.activeSelf);

        var sequence = Field<Sequence>(rail, "_placementFlightSequence");
        float duration = Field<float>(rail, "_placementFlightDuration");
        bool sawFrontFlip = false;
        bool sawMiniFace = false;
        for (int i = 1; i < 40; i++)
        {
            sequence.Goto(duration * i / 40f, false);
            float yAngle = Mathf.DeltaAngle(0f, flight.localEulerAngles.y);
            if (!miniFace.gameObject.activeSelf && yAngle > 5f && yAngle < 90f)
            {
                sawFrontFlip = true;
            }

            if (miniFace.gameObject.activeSelf && yAngle < -5f)
            {
                sawMiniFace = true;
            }
        }

        Assert.IsTrue(sawFrontFlip, "front face should turn toward edge-on before the swap");
        Assert.IsTrue(sawMiniFace, "mini face should unfold from -90 after the swap");

        sequence.Complete();

        Assert.IsTrue(miniFace.gameObject.activeSelf);
        Assert.That(
            Mathf.Abs(Mathf.DeltaAngle(flight.eulerAngles.y, 0f)),
            Is.LessThan(0.01f));
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

- [ ] **Step 2: Run the rail tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.claude/worktrees/card-outline-curved-placement-cf7ba9 \
  -runTests \
  -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests \
  -testResults /private/tmp/flight-flip-rail-red.xml \
  -logFile /private/tmp/flight-flip-rail-red.log
```

Expected: `Placement_flight_flips_to_the_mini_card_face_in_the_settle_segment`가
`miniFace` `IsNotNull` 단언에서 실패 (RED). 기존 테스트는 통과.

- [ ] **Step 3: Implement the mini face and flip rotation**

`ExecutionRailView.cs`의 `StartPlacementFlight`에서 `Vector3 endScale = ...` 줄 바로 위에 미니 면 생성을 추가한다:

```csharp
RailCardView miniFace = CreateFlightMiniFace(flight);
```

tween 콜백의 회전 줄을 다음으로 교체한다 (`flight.localPosition = ...`은 그대로):

```csharp
float settleT = PlacementFlightPath.SettleProgress(
    value, _placementFlightCurveSplit);
if (settleT >= PlacementFlightPath.FlipSwapProgress
    && !miniFace.gameObject.activeSelf)
{
    miniFace.gameObject.SetActive(true);
}

flight.localRotation = Quaternion.Euler(
    0f,
    PlacementFlightPath.FlipAngle(settleT),
    sample.AngleDegrees);
```

`SizeInLayer` 헬퍼 근처에 private 메서드를 추가한다:

```csharp
/// <summary>The mini card face shown after the flip passes edge-on. A child of the
/// flight rect, so ClearPlacementFlight destroys it with the flight visual.</summary>
private RailCardView CreateFlightMiniFace(RectTransform flight)
{
    var face = Instantiate(_cardPrefab, flight);
    var faceRect = (RectTransform)face.transform;
    faceRect.anchorMin = Vector2.zero;
    faceRect.anchorMax = Vector2.one;
    faceRect.offsetMin = Vector2.zero;
    faceRect.offsetMax = Vector2.zero;
    face.Bind(_placementPreviewCard.Value, null, null);
    face.SetInteractable(false);
    foreach (var graphic in face.GetComponentsInChildren<Graphic>(true))
    {
        graphic.raycastTarget = false;
    }

    face.gameObject.SetActive(false);
    return face;
}
```

`FlipAngle(0) == 0`이므로 첫 구간의 회전은 기존과 동일하다 — 별도 분기가 필요 없다.

- [ ] **Step 4: Run the rail tests and verify GREEN**

Step 2의 명령을 `-testResults /private/tmp/flight-flip-rail-green.xml -logFile /private/tmp/flight-flip-rail-green.log`로 다시 실행한다.

Expected: `ExecutionRailInputTests` 전체 통과. 기존
`Placement_flight_hides_silhouette_and_settles_at_its_pose`의 곡률·스냅·크기 단언도 유지된다.

- [ ] **Step 5: Update manual playtest instructions**

`Assets/Unity/PLAYTEST.md` 통합 체크리스트 1번 항목의 마지막 문장
"연속 클릭해도 적용과 갱신은 한 번만 일어나야 한다." 앞에 다음 문장을 추가한다:

```markdown
안착 구간에서 카드가 Y축으로 한 번 뒤집히며, 모서리만 보이는 90° 순간 이후에는
레일 미니 카드 모습으로 펼쳐져 착지한다.
```

- [ ] **Step 6: Run full verification**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.claude/worktrees/card-outline-curved-placement-cf7ba9 \
  -runTests \
  -testPlatform EditMode \
  -testResults /private/tmp/flight-flip-editmode.xml \
  -logFile /private/tmp/flight-flip-editmode.log

dotnet build FateWeaver.Tests.UnityEditMode.csproj --no-restore

dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0
```

Expected: EditMode XML `failed="0"`, 빌드 `0 Error(s)`, 헤드리스 `Failed: 0`.

- [ ] **Step 7: Commit Task 2 only and restore batch side effects**

```bash
git diff --check -- \
  Assets/Unity/ExecutionRailView.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs \
  Assets/Unity/PLAYTEST.md
git add \
  Assets/Unity/ExecutionRailView.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs \
  Assets/Unity/PLAYTEST.md
git commit -m "feat(ui): flip placement flight into mini card face"
git checkout -- ProjectSettings/ProjectSettings.asset
git status --short
```

Expected: 커밋에 위 3개 파일만 포함, 이후 워킹 트리 깨끗.

---

## Final Review

- [ ] 첫 구간과 `settleT < 0.5` 구간에서 미니 면 비활성, 앞면(텍스트 카드) 유지.
- [ ] `settleT >= 0.5`부터 미니 면 활성, 비행 Y 회전 -90°→0° 전개.
- [ ] 착지 시 Y=0, 기존 위치·Z 회전·크기 스냅 단언 유지.
- [ ] 중도 취소 시 미니 면이 비행 카드와 함께 파괴된다 (자식 관계 — 별도 코드 없음).
- [ ] 새 튜닝값·매직 넘버·프리팹·씬·Core 변경 없음.
- [ ] `git status --short`가 깨끗하다 (배치 부산물 복원 포함).
