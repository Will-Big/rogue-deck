# Card Status Grid and Tooltip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 카드에 직접 붙은 상태를 네 열 고정 그리드로 표시하고, JSON 콘텐츠에서 온 제목·설명을 공유 호버 툴팁으로 보여준다.

**Architecture:** UI 컴포넌트와 프리팹은 표시 준비가 끝난 `CardStatusPresentation`만 소비한다. JSON 중앙 카탈로그와 Unity Sprite 카탈로그의 결합은 `CardStatusPresentationFactory` 한 곳에서 수행하며, 진행 중인 JSON 리팩터링이 합류하기 전에는 이 조립 지점을 임시 SO·enum switch·하드코딩 문자열로 대신하지 않는다. Task 1–2는 현재 브랜치에서 독립 수행할 수 있고, Task 3부터는 JSON 리팩터링이 master에 반영된 뒤 사용자 승인을 받아 현재 브랜치에 동기화한 후 수행한다.

**Tech Stack:** Unity 6000.5.2f1, C# 9, NUnit 3, uGUI `GridLayoutGroup`/`ContentSizeFitter`, TextMeshPro, JSON 중앙 콘텐츠 카탈로그

## Global Constraints

- 작업 위치는 `/Users/ish/Git/rogue-deck-card-frame-design`, 브랜치는 `refactor/card-frame-design`이다. 새 워크트리·브랜치를 만들거나 메인 체크아웃의 브랜치를 전환하지 않는다.
- 권위 설계는 `docs/superpowers/specs/2026-08-03-card-status-grid-tooltip-design.md`다.
- 카드·상태 규칙과 콘텐츠의 저작 원본은 JSON 하나뿐이다. 임시 ScriptableObject 콘텐츠, C# 표시 문구, `CardStatusIcon`별 switch를 추가하지 않는다.
- Unity의 `StatusIconCatalog`는 JSON `iconKey`를 Sprite 참조로 바꾸는 표현 에셋이며 규칙·텍스트 콘텐츠 원본이 아니다.
- 상태 그리드에는 카드 인스턴스에 직접 붙은 상태만 표시한다. 런·전투 전체 상태의 간접 효과를 카드 아이콘으로 반복하지 않는다.
- 그리드는 한 행 네 칸, 셀 `26×26`, 간격 `4×4`, `Upper Left`, `Fixed Column Count = 4`다. 마지막 행을 가운데 정렬하지 않고 위쪽 기준점에서 아래로 확장한다.
- 툴팁은 `제목:`·`설명:` 접두사를 출력하지 않는다. 제목색은 `#F2C14E`, 설명색은 `#E8EDF2`이며 프리팹이 소유한다.
- 런타임 `new GameObject`, `GameObject.Find`, `FindObjectOfType`, `Resources.Load`, 하드코딩 에셋 경로를 추가하지 않는다.
- 프리팹·씬 참조는 `[SerializeField] private` 또는 명시적 생성 인자로 전달한다.
- 외부 패키지나 에셋을 추가하지 않는다.
- Unity 자동 검증은 이 워크트리를 `-projectPath`로 사용하고 결과·로그를 `/private/tmp`에 쓴다.
- 사용자가 조정한 `ExecutionCardView.prefab`의 비용 배지와 상태 영역 좌표를 보존한다. 구조를 만든 뒤 수치 조정은 사용자 체크포인트에서 수행한다.

---

## File Map

- Create `Assets/Unity/CardStatusPresentation.cs`: UI에 전달되는 불변 상태 표시 값.
- Create `Assets/Unity/CardStatusIconView.cs`: 아이콘 표시와 pointer enter/exit 전달.
- Create `Assets/Unity/CardStatusTooltipView.cs`: 공유 패널의 제목·본문·위치·소유자 수명 관리.
- Create `Assets/Unity/CardStatusPresentationFactory.cs`: JSON 표시 데이터와 Sprite 카탈로그의 유일한 결합점.
- Create `Assets/Unity/StatusIconCatalog.cs`: `iconKey -> Sprite` 인스펙터 매핑과 부팅 검증.
- Create `Assets/Unity/StatusIconCatalog.asset`: 기본 `lock` iconKey의 Sprite 참조.
- Create `Assets/Unity/Prefabs/CardStatusTooltipView.prefab`: Canvas 오버레이에 하나만 생성되는 툴팁.
- Modify `Assets/Unity/CardPresentation.cs`: 상태 enum 목록을 `CardStatusPresentation` 목록으로 교체.
- Modify `Assets/Unity/CardView.cs`: 일반 템플릿 복제, 재바인딩 정리, 툴팁 전달.
- Modify `Assets/Unity/CardPrefabCatalog.cs`: tooltip/status icon 표현 프리팹·카탈로그 검증.
- Modify `Assets/Unity/HandFanView.cs`, `ExecutionRailView.cs`, `PileView.cs`: 같은 툴팁 인스턴스를 전체 카드에 전달.
- Modify `Assets/Unity/BattleScreenController.cs`, `Assets/Unity/Editor/BattleSceneBuilder.cs`, `Assets/Scenes/FateWeaverBattle.unity`: JSON 표시 source와 Canvas overlay 툴팁 조립.
- Modify `Assets/Unity/Prefabs/ExecutionCardView.prefab`, `InterventionCardView.prefab`: 일반 4열 상태 그리드와 비활성 템플릿.
- Delete after replacement: `Assets/Unity/CardStatusIcon.cs`와 `.meta`.
- Test `Assets/Tests/UnityEditMode/CardStatusTooltipViewTests.cs`, `CardPresentationTests.cs`, `CardFramePrefabTests.cs`, `CardPrefabCatalogTests.cs`.

---

### Task 1: Build source-independent status icon and tooltip components

**Files:**
- Create: `Assets/Unity/CardStatusPresentation.cs`
- Create: `Assets/Unity/CardStatusIconView.cs`
- Create: `Assets/Unity/CardStatusTooltipView.cs`
- Test: `Assets/Tests/UnityEditMode/CardStatusTooltipViewTests.cs`

**Interfaces:**
- Produces: `CardStatusDisplayContent(string key, string displayName, string description, string iconKey)` as the pure JSON-catalog projection.
- Produces: `ICardStatusDisplaySource.Resolve(string key) -> CardStatusDisplayContent` as the only UI-side adapter seam.
- Produces: `CardStatusPresentation(string key, Sprite icon, string title, string description)` with non-empty validation.
- Produces: `CardStatusIconView.Bind(CardStatusPresentation data, CardStatusTooltipView tooltip)`.
- Produces: `CardStatusTooltipView.Show(CardStatusIconView owner, string title, string description, Vector2 screenPosition)` and `Hide(CardStatusIconView owner)`.

- [x] **Step 1: Write RED tests for validation and owner-aware hover lifetime**

```csharp
[Test]
public void Presentation_rejects_missing_display_fields()
{
    var sprite = Sprite.Create(
        new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
    Assert.Throws<ArgumentException>(
        () => new CardStatusPresentation("lock", sprite, "", "설명"));
    Assert.Throws<ArgumentException>(
        () => new CardStatusPresentation("lock", sprite, "잠금", ""));
}

[Test]
public void Older_icon_cannot_hide_newer_icons_tooltip()
{
    var tooltip = BuildTooltip(out var panel, out var title, out var body);
    var first = new GameObject("First").AddComponent<CardStatusIconView>();
    var second = new GameObject("Second").AddComponent<CardStatusIconView>();

    tooltip.Show(first, "잠금", "첫 설명", Vector2.zero);
    tooltip.Show(second, "독", "둘째 설명", Vector2.zero);
    tooltip.Hide(first);

    Assert.IsTrue(panel.activeSelf);
    Assert.AreEqual("독", title.text);
    Assert.AreEqual("둘째 설명", body.text);
}
```

- [x] **Step 2: Run the focused test and confirm RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardStatusTooltipViewTests \
  -testResults /private/tmp/card-status-tooltip-components-red.xml \
  -logFile /private/tmp/card-status-tooltip-components-red.log
```

Expected: compile failure because the three production types do not exist.

- [x] **Step 3: Implement the minimal contracts**

```csharp
public readonly struct CardStatusPresentation
{
    public string Key { get; }
    public Sprite Icon { get; }
    public string Title { get; }
    public string Description { get; }

    public CardStatusPresentation(
        string key, Sprite icon, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Status key is required.", nameof(key));
        Icon = icon != null ? icon
            : throw new ArgumentNullException(nameof(icon));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Status title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Status description is required.", nameof(description));
        Key = key;
        Title = title;
        Description = description;
    }
}
```

In the same file, define `CardStatusDisplayContent` with four validated string properties and:

```csharp
public interface ICardStatusDisplaySource
{
    CardStatusDisplayContent Resolve(string key);
}
```

`CardStatusIconView` stores serialized `Image _icon`, the bound value and tooltip. Pointer enter calls `Show(this, Title, Description, eventData.position)`; pointer exit and `OnDisable` call `Hide(this)`. `CardStatusTooltipView` changes only its current owner's panel and never prepends field labels.

- [x] **Step 4: Run focused tests and commit**

Run Step 2 and expect PASS.

```bash
git add Assets/Unity/CardStatusPresentation.cs Assets/Unity/CardStatusPresentation.cs.meta \
  Assets/Unity/CardStatusIconView.cs Assets/Unity/CardStatusIconView.cs.meta \
  Assets/Unity/CardStatusTooltipView.cs Assets/Unity/CardStatusTooltipView.cs.meta \
  Assets/Tests/UnityEditMode/CardStatusTooltipViewTests.cs \
  Assets/Tests/UnityEditMode/CardStatusTooltipViewTests.cs.meta
git commit -m "feat(ui): add card status tooltip components"
```

---

### Task 2: Author the generic grid and tooltip prefab structures

**Files:**
- Create: `Assets/Unity/Prefabs/CardStatusTooltipView.prefab`
- Create: `Assets/Unity/Prefabs/CardStatusTooltipView.prefab.meta`
- Modify: `Assets/Unity/Prefabs/ExecutionCardView.prefab`
- Modify: `Assets/Unity/Prefabs/InterventionCardView.prefab`
- Test: `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`

**Interfaces:**
- Execution and intervention roots contain `CardStatusGrid/StatusIconTemplate`; template is inactive and has `CardStatusIconView` plus its Image.
- The grid uses only standard `GridLayoutGroup` and `ContentSizeFitter`; no custom layout component.
- Tooltip prefab exposes panel, title TMP, description TMP, and screen offset through `CardStatusTooltipView` serialized fields.

- [x] **Step 1: Add RED prefab-contract tests**

```csharp
[TestCase(CardPrefabCatalogTests.ExecutionCardPath)]
[TestCase(CardPrefabCatalogTests.InterventionCardPath)]
public void Status_grid_is_four_columns_and_grows_down(string path)
{
    var card = Load<CardView>(path);
    var grid = Child(card.transform, "CardStatusGrid");
    var layout = grid.GetComponent<GridLayoutGroup>();
    var fitter = grid.GetComponent<ContentSizeFitter>();
    var template = Child(grid, "StatusIconTemplate");

    Assert.AreEqual(new Vector2(26f, 26f), layout.cellSize);
    Assert.AreEqual(new Vector2(4f, 4f), layout.spacing);
    Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, layout.constraint);
    Assert.AreEqual(4, layout.constraintCount);
    Assert.AreEqual(GridLayoutGroup.Corner.UpperLeft, layout.startCorner);
    Assert.AreEqual(TextAnchor.UpperLeft, layout.childAlignment);
    Assert.AreEqual(1f, ((RectTransform)grid).pivot.y);
    Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, fitter.verticalFit);
    Assert.IsFalse(template.gameObject.activeSelf);
    Assert.IsNotNull(template.GetComponent<CardStatusIconView>());
}
```

Add tooltip assertions for exactly two TMP children, no literal `제목:`/`설명:` text, title color `F2C14E`, description color `E8EDF2`, and disabled root by default.

- [x] **Step 2: Run `CardFramePrefabTests` and confirm RED**

Use Task 1's Unity command with filter `FateWeaver.Tests.UnityEditMode.CardFramePrefabTests` and results `/private/tmp/card-status-prefabs-red.xml`.

- [x] **Step 3: Codex creates the prefab objects and serialized references**

Create the tooltip prefab as a reusable asset. In both card prefabs rename `CardStatusRow` to `CardStatusGrid` and `LockIcon` to `StatusIconTemplate`, preserve the user's existing grid anchored position, add the standard layout components and attach `CardStatusIconView` to the template. Keep the current `_lockBadge` reference valid until Task 4 replaces it; this preserves current lock rendering while JSON wiring is unavailable.

- [x] **Step 4: Pause for user numeric review**

The user adjusts only these Inspector values if desired:

1. `CardStatusGrid` first-row anchored position while keeping its top pivot.
2. Tooltip panel width, padding, title/body font sizes, and pointer offset.
3. Do not change grid cell `26×26`, spacing `4×4`, or four-column constraint.
4. Keep title `#F2C14E`, description `#E8EDF2` and tell Codex when saved.

- [x] **Step 5: Verify and commit the reviewed structures**

Run `CardFramePrefabTests`; expected PASS without changing current `CardStatusIcon` runtime behavior.

```bash
git add Assets/Unity/Prefabs/CardStatusTooltipView.prefab \
  Assets/Unity/Prefabs/CardStatusTooltipView.prefab.meta \
  Assets/Unity/Prefabs/ExecutionCardView.prefab \
  Assets/Unity/Prefabs/InterventionCardView.prefab \
  Assets/Tests/UnityEditMode/CardFramePrefabTests.cs
git commit -m "refactor(ui): author generic card status prefabs"
```

---

### Integration Gate: wait for the JSON content refactor

Do not start Task 3 until all conditions are true:

1. the JSON runtime-content refactor is committed to `master`;
2. the user explicitly approves bringing current `master` into `refactor/card-frame-design`;
3. `git show master:docs/superpowers/README.md` identifies the current JSON/status catalog authority;
4. the merged catalog can expose a card-local status key, display name, description and icon key from JSON.

If the merged catalog uses different type names, only the `ICardStatusDisplaySource` composition in Task 4 changes; `CardStatusDisplayContent`, `CardStatusPresentation`, both view components and both prefabs remain unchanged.

---

### Task 3: Project JSON status content into card presentations

**Files:**
- Create: `Assets/Unity/StatusIconCatalog.cs`
- Create: `Assets/Unity/StatusIconCatalog.asset`
- Create: `Assets/Unity/CardStatusPresentationFactory.cs`
- Modify: `Assets/Unity/CardPrefabCatalog.cs`
- Test: `Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs`

**Interfaces:**
- Consumes: `ICardStatusDisplaySource` from Task 1; Task 4 supplies its JSON-catalog implementation.
- Produces: `CardStatusPresentationFactory.Create(string statusKey)`.
- `StatusIconCatalog.Resolve(string iconKey)` returns a Sprite or throws with the missing key.

- [ ] **Step 1: Write RED projection tests**

```csharp
[Test]
public void Factory_preserves_json_text_and_resolves_only_the_sprite()
{
    var icons = BuildIconCatalog(("lock", LockSprite));
    var source = new FakeStatusDisplaySource(new CardStatusDisplayContent(
        "lock", "잠금", "이 카드는 실행 순서를 변경할 수 없습니다.", "lock"));
    var factory = new CardStatusPresentationFactory(source, icons);
    var result = factory.Create("lock");

    Assert.AreEqual("lock", result.Key);
    Assert.AreSame(LockSprite, result.Icon);
    Assert.AreEqual("잠금", result.Title);
    Assert.AreEqual("이 카드는 실행 순서를 변경할 수 없습니다.", result.Description);
}

[Test]
public void Unknown_icon_key_fails_instead_of_using_a_fallback()
{
    var source = new FakeStatusDisplaySource(
        new CardStatusDisplayContent("lock", "잠금", "설명", "missing"));
    var factory = new CardStatusPresentationFactory(source, BuildIconCatalog());
    Assert.Throws<KeyNotFoundException>(() => factory.Create("lock"));
}
```

- [ ] **Step 2: Run `CardPresentationTests|CardPrefabCatalogTests` and confirm RED**

Run the Unity EditMode command with filter `FateWeaver.Tests.UnityEditMode.CardPrefabCatalogTests` and results `/private/tmp/card-status-json-projection-red.xml`.

- [ ] **Step 3: Implement the single projection boundary**

`StatusIconCatalog` uses a serialized entry array of `string Key` and `Sprite Icon`, validates empty/duplicate/null entries, and never loads paths. `CardStatusPresentationFactory` asks `ICardStatusDisplaySource` for one key, copies its strings unchanged and resolves only `IconKey`. Create `StatusIconCatalog.asset` with the existing lock Sprite, assign it to `CardPrefabCatalog.asset`, and validate the reference and entries. Do not modify the current `CardPresentation`/`CardView` binding in this task, so the branch remains functional between commits.

- [ ] **Step 4: Run focused tests and commit**

Expected: projection and missing/duplicate icon-key validation pass.

```bash
git add Assets/Unity/StatusIconCatalog.cs Assets/Unity/StatusIconCatalog.cs.meta \
  Assets/Unity/StatusIconCatalog.asset Assets/Unity/StatusIconCatalog.asset.meta \
  Assets/Unity/CardStatusPresentationFactory.cs Assets/Unity/CardStatusPresentationFactory.cs.meta \
  Assets/Unity/CardPrefabCatalog.cs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs
git commit -m "refactor(ui): project JSON card status content"
```

---

### Task 4: Wire one overlay tooltip through every full-card host

**Files:**
- Modify: `Assets/Unity/CardPresentation.cs`
- Modify: `Assets/Unity/CardView.cs`
- Modify: `Assets/Unity/CardPrefabCatalog.cs`
- Modify: `Assets/Unity/PlaytestCardArt.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Modify: `Assets/Unity/PileView.cs`
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/Scenes/FateWeaverBattle.unity`
- Delete: `Assets/Unity/CardStatusIcon.cs`, `Assets/Unity/CardStatusIcon.cs.meta`
- Test: `Assets/Tests/UnityEditMode/CardPresentationTests.cs`
- Test: `Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs`
- Test: `Assets/Tests/UnityEditMode/CardStatusTooltipViewTests.cs`

**Interfaces:**
- `CardPresentation.StatusIcons` becomes `IReadOnlyList<CardStatusPresentation>`.
- The merged JSON catalog is wrapped once as `ICardStatusDisplaySource`; no view knows its concrete type.
- `CardPrefabCatalog.Create(CardPresentation data, RectTransform parent, CardStatusTooltipView tooltip)` requires a tooltip for interactive full cards.
- `HandFanView`, `ExecutionRailView` and `PileView` receive the same tooltip instance through `EditorBuild`/initialization and pass it to every full `CardView`.
- Placement-flight copies with all raycasts disabled do not open the tooltip.

- [ ] **Step 1: Write RED shared-instance and stale-tooltip tests**

```csharp
[Test]
public void Rebinding_card_hides_tooltip_owned_by_removed_status_icon()
{
    var tooltip = InstantiateTooltip();
    var card = InstantiateConfiguredExecution(tooltip);
    card.Bind(PresentationWithStatuses(LockStatus), null);
    var icon = card.GetComponentInChildren<CardStatusIconView>();
    tooltip.Show(icon, LockStatus.Title, LockStatus.Description, Vector2.zero);

    card.Bind(PresentationWithStatuses(), null);

    Assert.IsFalse(tooltip.gameObject.activeSelf);
}
```

Add a scene/catalog test that the hand, rail preview and pile popup are initialized with the exact same `CardStatusTooltipView` reference.
Add `CardPresentationTests` proving a locked execution instance resolves the stable `lock` key through the injected `CardStatusPresentationFactory`, while an unlocked card produces an empty list. The test's fake `ICardStatusDisplaySource` owns the Korean strings; production Unity code does not.

- [ ] **Step 2: Run focused tests and confirm RED**

Run `CardStatusTooltipViewTests|CardPrefabCatalogTests` to `/private/tmp/card-status-tooltip-wiring-red.xml`.

- [ ] **Step 3: Implement explicit scene-to-prefab injection**

At the composition root, adapt the merged JSON status catalog to `ICardStatusDisplaySource` by copying its key, display name, description and icon key into `CardStatusDisplayContent`. Construct one `CardStatusPresentationFactory` and pass it to every `CardPresentation.From`/`FromDefinition` call. The stable rules key `lock` may be referenced through one typed/static key declaration, but its Korean title, description and icon key may not appear in production C#.

Replace `CardPresentation.StatusIcons` with the display-ready list, remove the `CardStatusIcon` enum and status-specific branch from `PlaytestCardArt`, and replace `_lockBadge` with `_statusIconTemplate`. `CardView.RefreshStatusIcons` clones the inactive template, calls `CardStatusIconView.Bind`, and tracks generated instances explicitly rather than assuming child index zero.

`BattleSceneBuilder` instantiates `CardStatusTooltipView.prefab` once under the existing overlay, assigns it to `BattleScreenController`, and passes the same instance to the three card hosts. No static singleton or hierarchy search is allowed. `CardView` hides a tooltip owned by one of its generated icons before clearing/rebinding them.

- [ ] **Step 4: Run focused tests and commit**

```bash
git add Assets/Unity/CardPresentation.cs Assets/Unity/CardView.cs \
  Assets/Unity/CardPrefabCatalog.cs Assets/Unity/PlaytestCardArt.cs \
  Assets/Unity/CardStatusIcon.cs Assets/Unity/CardStatusIcon.cs.meta \
  Assets/Unity/HandFanView.cs \
  Assets/Unity/ExecutionRailView.cs Assets/Unity/PileView.cs \
  Assets/Unity/BattleScreenController.cs Assets/Unity/Editor/BattleSceneBuilder.cs \
  Assets/Scenes/FateWeaverBattle.unity \
  Assets/Tests/UnityEditMode/CardPresentationTests.cs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs \
  Assets/Tests/UnityEditMode/CardStatusTooltipViewTests.cs
git commit -m "feat(ui): wire shared card status tooltip"
```

---

### Task 5: Verify layout, JSON ownership and manual hover behavior

**Files:**
- Modify: `Assets/Unity/PLAYTEST.md`
- Modify after completion: `docs/superpowers/README.md`, `docs/superpowers/archive/README.md`
- Move after completion: this plan to `docs/superpowers/archive/plans/2026-08-03-card-status-grid-tooltip.md`

- [ ] **Step 1: Run focused structural tests**

Run `CardStatusTooltipViewTests`, `CardFramePrefabTests`, `CardPrefabCatalogTests`, and `CardPresentationTests`. Expected: all pass, including 0/1/4/5 icons and second-row Y below first-row Y.

- [ ] **Step 2: Run the complete automated suites**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo

/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/card-status-grid-tooltip-editmode.xml \
  -logFile /private/tmp/card-status-grid-tooltip-editmode.log
```

- [ ] **Step 3: Audit forbidden fallback sources**

```bash
rg -n 'CardStatusIcon|제목:|설명:|이 카드는 실행 순서를 변경할 수 없습니다|Resources\.Load|FindObjectOfType' \
  Assets/Unity Assets/Tests/UnityEditMode
```

Expected: no production `CardStatusIcon`, tooltip labels, lock description literal, runtime path load or scene search. Test fixtures may contain the expected Korean content.

- [ ] **Step 4: Pause for user Play Mode review**

The user verifies:

1. one status starts at the first cell and five statuses place four on row one, one on row two;
2. added rows grow down from the adjusted reference position;
3. hover shows `잠금` and `이 카드는 실행 순서를 변경할 수 없습니다.` without field labels;
4. title is gold and description is light gray;
5. leaving the icon, rebuilding the hand, opening rail preview or pile popup never leaves a stale tooltip;
6. clicking through an icon still invokes the owning card action.

- [ ] **Step 5: Update checklist, archive and commit after approval**

Add the six checks to `PLAYTEST.md`, move this plan to `archive/plans`, remove its active README row, add its archive row, then commit:

```bash
git add Assets/Unity/PLAYTEST.md docs/superpowers/README.md \
  docs/superpowers/archive/README.md \
  docs/superpowers/archive/plans/2026-08-03-card-status-grid-tooltip.md
git commit -m "docs: archive card status tooltip plan"
```

Do not merge to `master` without explicit user approval.
