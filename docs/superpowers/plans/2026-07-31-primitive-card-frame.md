# Primitive Card Frame and Structured Description Continuation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현재 프리미티브 카드 프레임 구현을 승인된 색상 전용 진영 문법, 카드 전체 대상 그룹화, 실행·개입별 폼팩터로 완성하고 반응형 손패 회귀를 닫는다.

**Architecture:** 순수 C# `DescriptionComposer`가 정확한 nullable `CardTargetKey?`별로 카드 전체 효과를 첫 등장 순서에 따라 묶고, Unity는 구조화된 `Target`을 역파싱하지 않고 표시한다. 실행 카드는 가운데 대상 패널에 0–2개의 프리미티브 glyph를 한 행으로 표시하고, 개입 카드는 대상 패널 없이 그 높이를 설명 영역에 사용한다. 코드와 자동 테스트는 Codex가 작성하며, 프리팹 구조·좌표·직렬화 색은 각 RED 체크포인트에서 사용자가 Unity Inspector로 직접 저작한다.

**Tech Stack:** Unity 6000.5.2f1, C# 9, .NET 6/net5.0 headless harness, NUnit 3, uGUI, TextMeshPro, ScriptableObject/YAML prefabs, Unity EditMode batch tests

## Global Constraints

- 작업 위치는 `/Users/ish/Git/rogue-deck-card-frame-design`, 브랜치는 `refactor/card-frame-design`이다. 새 워크트리·브랜치를 만들거나 메인 체크아웃의 브랜치를 전환하지 않는다.
- 권위 설계는 `docs/superpowers/specs/2026-07-31-primitive-card-frame-design.md`와 선행 문서 `docs/superpowers/specs/2026-07-27-position-targeting-card-text-design.md`다.
- `FateWeaver.Core`와 `FateWeaver.Simulation`은 `UnityEngine`을 참조하지 않는다.
- 설명 문장과 효과 횟수는 중복 제거하지 않는다. 같은 nullable 대상 키의 문장만 카드 전체에서 한 줄로 모은다.
- Unity 설명 줄은 대상 범위와 무관하게 같은 `◆`를 쓰고, 아군 `#5DADE2`·적군 `#E85D5D` 색만 심볼 한 글자에 적용한다.
- Unity 대상 glyph도 같은 두 색만 진영 구분에 사용한다. 윤곽/채움, 별도 방향 표식, 서로 다른 진영 기호를 혼합하지 않는다.
- 아군 전열은 오른쪽, 적군 전열은 왼쪽이다. `모두`는 `◇━━◇`이며 다른 위치 glyph와 같은 시각 폭을 사용한다.
- 실행 카드 대상은 한 진영이면 가운데, 양 진영이면 아군 왼쪽·적군 오른쪽의 한 행으로 가운데 정렬한다. 세로 배치는 없다.
- `∅`는 무대상 실행 카드에만 표시한다. 개입 카드에는 대상 패널과 `∅`가 모두 없고 `ExpandedDescriptionPanel`이 그 높이를 사용한다.
- 런타임 `new GameObject`, `GameObject.Find`, `FindObjectOfType`, 태그·레이어 이름 비교, `Resources.Load` 호출, 파일 경로 기반 프리팹 선택을 추가하지 않는다.
- 프리팹 참조와 색은 `[SerializeField] private`으로 저작한다. 색상 값을 규칙/표현 코드의 `const`나 `static readonly` 튜닝 상수로 만들지 않는다.
- 프리팹의 자식 좌표는 Inspector가 소유한다. C#은 범위 visual 활성화, 진영색 적용, 좌우 미러링만 담당한다.
- 카드 아트·수치·효과 실행 순서·개입 대상 규칙과 실행 영역의 소형 `RailCardView` 레이아웃은 변경하지 않는다.
- 외부 패키지나 에셋을 추가하지 않는다.
- 사용자가 수정 중인 `Assets/Unity/Prefabs/DescriptionLineView.prefab`을 자동 YAML 편집하거나 덮어쓰지 않는다.
- 헤드리스 명령은 `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`다.
- Unity 자동 검증은 이 워크트리를 `-projectPath`로 사용하고 결과·로그를 `/private/tmp`에 쓴다.
- 각 task는 RED → 최소 GREEN → 관련 회귀 → 사용자 프리팹 체크포인트(해당 시) → 제한 스테이징 → 커밋 순서를 지킨다.

---

## Current Baseline

다음 구현은 이미 현재 브랜치에 커밋됐다.

- 닫힌 위치 범위, 실행 시작 대상 스냅샷, 구조화 설명 모델: `dbeb678`–`80ab92c`
- `CardPresentation` 구조화 설명 전달: `f62a129`
- 카탈로그, 실행·개입 전체 카드, 초기 프리미티브 subview: `ab1b350`
- 전체 카드 소비처의 카탈로그 전환과 부팅 검증: `1321a55`–`2149695`
- 반응형 손패와 첫 번째 resize 상태 보존 수정: `48f15f8`, `6cfea6d`
- 최종 진영색·대상 문법·전역 그룹화 설계: `2b62162`, `558159a`

현재 워크트리에는 다음 사용자/진행 중 변경이 있으므로 계획 문서 커밋에 섞지 않는다.

- `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`: Task 1 RED 두 개
- `Assets/Unity/Prefabs/DescriptionLineView.prefab`: 사용자 Inspector 수정
- `.superpowers/`, `graphify-out/.vocab.txt`, `graphify-out/memory/`, `graphify-out/reflections/`: 로컬 도구 산출물

## Remaining File Map

### Responsive hand

- Modify `Assets/Unity/HandCardHoverEffect.cs`: 활성 여부를 노출하고 authored sibling 복원 계약을 유지한다.
- Modify `Assets/Unity/HandFanView.cs`: resize 전에 활성 카드의 현재 z-order를 캡처하고 그 순서대로 다시 올린다.
- Test `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`.

### Structured descriptions

- Modify `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs`: 인접 비교를 전역 ordered accumulator로 바꾼다.
- Modify `Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs`: 모든 대상 plain-text 심볼을 `◆`로 고정한다.
- Modify `Assets/Core/Tests/EditMode/StructuredCardDescriptionTests.cs`와 기존 설명 golden 테스트.

### Unity description and target views

- Modify `Assets/Unity/DescriptionLineView.cs`: TMP 한 흐름의 색상 심볼 접두사.
- User-modify `Assets/Unity/Prefabs/DescriptionLineView.prefab`: glyph 슬롯 제거, full-width TMP와 두 직렬화 색 할당.
- Modify `Assets/Unity/TargetGlyphView.cs`: 범위별 authored visual 선택, 미러링, 색 적용.
- User-modify `Assets/Unity/Prefabs/TargetGlyphView.prefab`: 범위별 동일 폭 프리미티브 계층.
- User-review `ExecutionCardView.prefab`, `InterventionCardView.prefab`: 한 행 중앙 정렬과 확장 설명 영역.
- Modify `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`, `CardPrefabCatalogTests.cs`.

### Verification and cleanup

- Modify `Assets/Unity/Editor/CardCodeGenerator.cs`, `Assets/Tests/UnityEditMode/CardCodeGeneratorTests.cs`.
- Modify `Assets/Unity/PLAYTEST.md`.
- Delete poster v2 PNG/meta 쌍은 GUID 참조 감사 결과가 완전히 비었을 때만 수행한다.
- Archive this plan and update both document indexes only after all verification passes.

---

### Task 1: Finish responsive active-card sibling ordering

**Files:**
- Modify: `Assets/Unity/HandCardHoverEffect.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Test: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`

**Interfaces:**
- Produces: `HandCardHoverEffect.IsActive` internal read-only state.
- Preserves: `UpdateBaseline(Vector2, Quaternion, int)`, `ReapplyActiveSiblingOrder()`, and exact authored sibling restoration.
- `HandFanView.RecalculateLayout()` reapplies active cards in their pre-resize sibling order, so the most recently activated card remains topmost.

- [x] **Step 1: Add the two RED tests already present in the worktree**

```csharp
[Test]
public void Most_recent_active_card_remains_last_after_resize()
{
    var hand = BuildResponsiveHand(root, FiveCards(), 650f, 260f);
    var views = root.GetComponentsInChildren<CardView>();
    hand.SetHeld(3, true);
    hand.SetHeld(1, true);

    ((RectTransform)root.transform).sizeDelta = new Vector2(900f, 260f);
    InvokeDimensionChange(hand);

    Assert.AreSame(views[1].transform, views[1].transform.parent.GetChild(4));
}

[Test]
public void Releasing_multiple_active_cards_restores_original_sibling_order()
{
    var hand = BuildResponsiveHand(root, FiveCards(), 650f, 260f);
    var views = root.GetComponentsInChildren<CardView>();
    hand.SetHeld(1, true);
    hand.SetHeld(3, true);
    InvokeDimensionChange(hand);
    hand.SetHeld(3, false);
    hand.SetHeld(1, false);

    for (var i = 0; i < views.Length; i++)
        Assert.AreEqual(i, views[i].transform.GetSiblingIndex());
}
```

- [x] **Step 2: Run the focused test and confirm RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.HandFanHoverTests \
  -testResults /private/tmp/hand-active-order-red.xml \
  -logFile /private/tmp/hand-active-order-red.log
```

Expected: `Most_recent_active_card_remains_last_after_resize` fails because the current foreach loop reapplies list order, not activation order.

- [x] **Step 3: Preserve the pre-layout active order**

```csharp
// HandCardHoverEffect.cs
internal bool IsActive => _hovering || _held;

// HandFanView.RecalculateLayout(), before UpdateBaseline mutates siblings
var activeInBackToFrontOrder = _hoverEffects
    .Where(effect => effect.IsActive)
    .OrderBy(effect => effect.transform.GetSiblingIndex())
    .ToArray();

// After every UpdateBaseline call
foreach (var effect in activeInBackToFrontOrder)
    effect.ReapplyActiveSiblingOrder();
```

Add `using System.Linq;` to `HandFanView.cs`. Do not add global/static activation counters.

- [x] **Step 4: Run focused and responsive regression tests**

Run Step 2, then `CardFrameResponsiveLayoutTests` and `HandFanResponsivePlayModeTests` in their existing test platforms. Expected: all pass and resize never changes the last-active card.

- [x] **Step 5: Commit only the responsive fix**

```bash
git add Assets/Unity/HandCardHoverEffect.cs Assets/Unity/HandFanView.cs \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs
git commit -m "fix(ui): preserve active hand card ordering"
```

---

### Task 2: Group description sentences across the whole card

**Files:**
- Modify: `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs`
- Modify: `Assets/Core/Tests/EditMode/StructuredCardDescriptionTests.cs`
- Modify: exact golden assertions returned by `rg -n '\[◇|\[◎|Repeated_nonconsecutive' Assets/Core/Tests Tests/Headless`

**Interfaces:**
- `DescriptionComposer.Compose` preserves target-group first-occurrence order.
- Every group preserves original sentence order and repetitions.
- Exact nullable `CardTargetKey?` equality is the only grouping key; `null` is one group.
- `KoreanDescriptionGrammar.Symbol(CardTargetKey)` returns `"◆"` for every faction/range.

- [x] **Step 1: Replace the old RED expectations**

```csharp
[Test]
public void Repeated_nonconsecutive_target_joins_the_first_matching_line()
{
    var layout = DescriptionComposer.Compose(
        Execution("repeat", DamageEnemy(3), BlockSelf(2), DamageEnemy(3)),
        Korean);

    Assert.AreEqual(2, layout.Lines.Count);
    Assert.AreEqual(
        new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne),
        layout.Lines[0].Target.Value);
    Assert.AreEqual("피해 3. 피해 3.", layout.Lines[0].Text);
    Assert.AreEqual(
        new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
        layout.Lines[1].Target.Value);
    Assert.AreEqual("방어 2.", layout.Lines[1].Text);
    Assert.AreEqual("[◆] 피해 3. 피해 3.\n[◆] 방어 2.", layout.PlainText);
}

[Test]
public void Nonconsecutive_null_targets_share_one_line_without_deduplication()
{
    var layout = DescriptionComposer.Compose(
        Execution(
            "repeat_null",
            new EffectData(EffectKeys.GrantNextTurnFate, 1),
            DamageEnemy(3),
            new EffectData(EffectKeys.GrantNextTurnFate, 2)),
        Korean);

    Assert.AreEqual(2, layout.Lines.Count);
    Assert.IsNull(layout.Lines[0].Target);
    Assert.AreEqual(
        "다음 사용 턴에 운명력 1 획득. 다음 사용 턴에 운명력 2 획득.",
        layout.Lines[0].Text);
}
```

Update `Toxic_reclaim_separates_enemy_and_ally_self_lines` plain text to:

```text
[◆] 독 최대 1 소비. 독 1.
[◆] 소비했다면 방어 4.
```

- [x] **Step 2: Run the focused headless tests and confirm RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo \
  --filter "FullyQualifiedName~StructuredCardDescriptionTests|FullyQualifiedName~DescriptionComposerTests|FullyQualifiedName~StarterPoolDescriptionTests"
```

Expected: current composer returns three lines for `Enemy → Ally → Enemy`, and `Ally/Self` still renders `◇◎`.

- [x] **Step 3: Replace adjacent append with an ordered accumulator**

```csharp
private static void AppendSentence(
    List<CardTargetKey?> lineTargets,
    List<StringBuilder> lineTexts,
    EffectDescriptionFragment fragment,
    string condition)
{
    var sentence = string.IsNullOrEmpty(condition)
        ? fragment.Text + "."
        : condition + " " + fragment.Text + ".";
    var index = lineTargets.FindIndex(
        target => Nullable.Equals(target, fragment.Target));
    if (index >= 0)
    {
        lineTexts[index].Append(' ').Append(sentence);
        return;
    }

    lineTargets.Add(fragment.Target);
    lineTexts.Add(new StringBuilder(sentence));
}
```

`KoreanDescriptionGrammar.Symbol` becomes:

```csharp
public string Symbol(CardTargetKey target) => "◆";
```

Do not alter `Layout` target-entry deduplication or its `Ally` then `Enemy` sorting.

- [x] **Step 4: Update all exact goldens and run the full headless suite**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
```

Expected: all pass; `rg -n '◇◎|◎◆' Assets/Core/Tests Tests/Headless Assets/Core/Simulation/Descriptions` has no production/golden match.

- [x] **Step 5: Commit the pure C# change**

```bash
git add Assets/Core/Simulation/Descriptions/DescriptionComposer.cs \
  Assets/Core/Simulation/Descriptions/KoreanDescriptionGrammar.cs \
  Assets/Core/Tests/EditMode Tests/Headless
git commit -m "refactor(sim): group descriptions by target"
```

---

### Task 3: Render description faction symbols in one TMP flow

**Files:**
- Modify: `Assets/Unity/DescriptionLineView.cs`
- User-modify: `Assets/Unity/Prefabs/DescriptionLineView.prefab`
- Modify: `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`
- Modify: affected assertions in `Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs`

**Interfaces:**
- `DescriptionLineView.Bind(CardDescriptionLine)` renders either plain body text or `<color=#RRGGBB>◆</color> {body}`.
- Serialized fields are exactly `TMP_Text _text`, `Color _allySymbolColor`, `Color _enemySymbolColor`.
- The prefab contains no `TargetGlyphView` and no fixed glyph slot.

- [x] **Step 1: Write RED binding and prefab-contract tests**

```csharp
[TestCase(CardTargetFaction.Ally, "#5DADE2")]
[TestCase(CardTargetFaction.Enemy, "#E85D5D")]
public void Description_line_colors_only_the_shared_symbol(
    CardTargetFaction faction,
    string expectedHex)
{
    var line = InstantiateDescriptionLine();
    try
    {
        line.Bind(new CardDescriptionLine(
            new CardTargetKey(faction, CardTargetRange.Self),
            "방어 2."));

        Assert.AreEqual(
            "<color=" + expectedHex + ">◆</color> 방어 2.",
            CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text);
    }
    finally
    {
        Object.DestroyImmediate(line.gameObject);
    }
}

[Test]
public void Description_line_uses_full_width_text_without_a_glyph_slot()
{
    var prefab = Load<DescriptionLineView>(CardPrefabCatalogTests.DescriptionLinePath);
    Assert.IsEmpty(prefab.GetComponentsInChildren<TargetGlyphView>(true));
    Assert.AreEqual(1, prefab.GetComponentsInChildren<TMP_Text>(true).Length);
    Assert.IsNull(
        typeof(DescriptionLineView).GetField(
            "_glyphSlot", BindingFlags.Instance | BindingFlags.NonPublic));
}
```

Keep a no-target assertion equal to `"카드 1장 뽑기."`, with no leading space or rich-text tag. Add a range-parameterized assertion proving `FrontOne`, `All`, and `Self` use the same `◆`.

```csharp
[TestCase(CardTargetRange.FrontOne)]
[TestCase(CardTargetRange.All)]
[TestCase(CardTargetRange.Self)]
public void Description_line_prefix_does_not_encode_range(CardTargetRange range)
{
    var line = InstantiateDescriptionLine();
    try
    {
        line.Bind(new CardDescriptionLine(
            new CardTargetKey(CardTargetFaction.Enemy, range),
            "피해 3."));
        Assert.AreEqual(
            "<color=#E85D5D>◆</color> 피해 3.",
            CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text);

        line.Bind(new CardDescriptionLine(null, "카드 1장 뽑기."));
        Assert.AreEqual(
            "카드 1장 뽑기.",
            CardPrefabCatalogTests.Field<TMP_Text>(line, "_text").text);
    }
    finally
    {
        Object.DestroyImmediate(line.gameObject);
    }
}
```

- [x] **Step 2: Run `CardFramePrefabTests` and confirm RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardFramePrefabTests \
  -testResults /private/tmp/description-inline-red.xml \
  -logFile /private/tmp/description-inline-red.log
```

Expected: current view still exposes `_glyphSlot` and `_glyph`.

- [x] **Step 3: Implement the TMP prefix without hardcoded palette constants**

```csharp
[SerializeField] private TMP_Text _text;
[SerializeField] private Color _allySymbolColor;
[SerializeField] private Color _enemySymbolColor;

public void Bind(CardDescriptionLine line)
{
    if (line == null) throw new ArgumentNullException(nameof(line));
    if (_text == null)
        throw new InvalidOperationException(
            "DescriptionLineView is missing its TMP text reference.");

    if (!line.Target.HasValue)
    {
        _text.text = line.Text;
        return;
    }

    Color color;
    switch (line.Target.Value.Faction)
    {
        case CardTargetFaction.Ally:
            color = _allySymbolColor;
            break;
        case CardTargetFaction.Enemy:
            color = _enemySymbolColor;
            break;
        default:
            throw new ArgumentOutOfRangeException(
                nameof(line),
                line.Target.Value.Faction,
                "Undefined target faction.");
    }
    _text.text = "<color=#" + ColorUtility.ToHtmlStringRGB(color)
        + ">◆</color> " + line.Text;
}
```

Validate undefined factions with `ArgumentOutOfRangeException`. Do not read `Range` when choosing the prefix.

- [x] **Step 4: Pause for the user’s DescriptionLineView prefab edit**

Codex provides these Inspector instructions and waits:

1. Open `Assets/Unity/Prefabs/DescriptionLineView.prefab` in Prefab Mode.
2. Delete the `GlyphSlot` child and its nested `TargetGlyphView`.
3. Keep `Text` as the only child.
4. Keep the root `HorizontalLayoutGroup`, set spacing `0`, child control width/height on, force expand width on, force expand height off.
5. Keep `Text` wrapping `Normal`; its layout width is controlled by the root and its preferred height drives the row.
6. On `DescriptionLineView`, assign `Text`; set ally color to `#5DADE2` and enemy color to `#E85D5D`.
7. Save the prefab and tell Codex the edit is complete.

Codex must not run a YAML rewrite against this prefab.

- [x] **Step 5: Verify the user-authored prefab and wrapping**

Run Step 2 plus `Description_line_wraps_to_remaining_width_and_grows_in_a_constrained_parent`. Expected: a long line wraps across the full 158-unit test parent width, and the second visual line begins beneath the symbol because symbol and body are one TMP flow.

- [x] **Step 6: Commit code, tests, and the reviewed prefab**

```bash
git add Assets/Unity/DescriptionLineView.cs \
  Assets/Unity/Prefabs/DescriptionLineView.prefab \
  Assets/Tests/UnityEditMode/CardFramePrefabTests.cs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs
git commit -m "refactor(ui): render inline faction symbols"
```

---

### Task 4: Replace faction-shaped target glyphs with colored range visuals

**Files:**
- Modify: `Assets/Unity/TargetGlyphView.cs`
- User-modify: `Assets/Unity/Prefabs/TargetGlyphView.prefab`
- Modify: `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`

**Interfaces:**
- Serialized visual roots: `_frontOneVisual`, `_frontTwoVisual`, `_backOneVisual`, `_backTwoVisual`, `_allVisual`, `_selfVisual`, `_emptyVisual`.
- Serialized faction colors: `_allyColor`, `_enemyColor`.
- `Bind(null)` activates neutral `Empty`; `Bind(key)` activates one range visual, colors every active `Graphic`, and mirrors that visual only for `Enemy`.

- [x] **Step 1: Replace old shape tests with final grammar tests**

```csharp
[TestCase(CardTargetRange.FrontOne, "FrontOne")]
[TestCase(CardTargetRange.FrontTwo, "FrontTwo")]
[TestCase(CardTargetRange.BackOne, "BackOne")]
[TestCase(CardTargetRange.BackTwo, "BackTwo")]
[TestCase(CardTargetRange.All, "All")]
[TestCase(CardTargetRange.Self, "Self")]
public void Target_glyph_activates_exactly_one_authored_range_visual(
    CardTargetRange range,
    string expectedName)
{
    var glyph = InstantiateGlyph();
    glyph.Bind(new CardTargetKey(CardTargetFaction.Ally, range));
    AssertActiveVisual(glyph, expectedName);
}

[TestCase(CardTargetFaction.Ally, "#5DADE2", 1f)]
[TestCase(CardTargetFaction.Enemy, "#E85D5D", -1f)]
public void Target_glyph_uses_color_only_and_points_front_toward_center(
    CardTargetFaction faction,
    string expectedHex,
    float expectedScaleSign)
{
    var glyph = InstantiateGlyph();
    glyph.Bind(new CardTargetKey(faction, CardTargetRange.FrontOne));
    var visual = Child(glyph.transform, "FrontOne");

    Assert.AreEqual(
        expectedScaleSign,
        Mathf.Sign(visual.localScale.x));
    Assert.IsTrue(
        visual.GetComponentsInChildren<Graphic>(true)
            .All(graphic =>
                "#" + ColorUtility.ToHtmlStringRGB(graphic.color) == expectedHex));
    Assert.IsEmpty(glyph.GetComponentsInChildren<Outline>(true));
}

private static void AssertActiveVisual(TargetGlyphView glyph, string expectedName)
{
    var names = new[]
    {
        "FrontOne", "FrontTwo", "BackOne", "BackTwo", "All", "Self", "Empty"
    };
    foreach (var name in names)
        Assert.AreEqual(name == expectedName, Child(glyph.transform, name).gameObject.activeSelf);
}
```

Add the following structure, width, self, and empty assertions:

```csharp
[Test]
public void Positional_target_visuals_have_equal_authored_widths()
{
    var prefab = Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath);
    var widths = new[] { "FrontOne", "FrontTwo", "BackOne", "BackTwo", "All" }
        .Select(name => ActiveGraphicBoundsWidth(Child(prefab.transform, name)))
        .ToArray();
    Assert.That(widths.Max() - widths.Min(), Is.LessThanOrEqualTo(0.5f));
}

[Test]
public void Self_and_empty_use_single_neutral_grammars()
{
    var glyph = InstantiateGlyph();
    try
    {
        glyph.Bind(new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self));
        AssertActiveVisual(glyph, "Self");
        var allyStructure = DirectChildNames(Child(glyph.transform, "Self"));
        Assert.IsTrue(Child(glyph.transform, "Self")
            .GetComponentsInChildren<Graphic>(true)
            .All(graphic => ColorUtility.ToHtmlStringRGB(graphic.color) == "5DADE2"));

        glyph.Bind(new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.Self));
        CollectionAssert.AreEqual(
            allyStructure,
            DirectChildNames(Child(glyph.transform, "Self")));
        Assert.IsTrue(Child(glyph.transform, "Self")
            .GetComponentsInChildren<Graphic>(true)
            .All(graphic => ColorUtility.ToHtmlStringRGB(graphic.color) == "E85D5D"));

        glyph.Bind(null);
        AssertActiveVisual(glyph, "Empty");
        Assert.IsTrue(Child(glyph.transform, "Empty")
            .GetComponentsInChildren<Graphic>(true)
            .All(graphic =>
            {
                var hex = ColorUtility.ToHtmlStringRGB(graphic.color);
                return hex != "5DADE2" && hex != "E85D5D";
            }));
    }
    finally
    {
        Object.DestroyImmediate(glyph.gameObject);
    }
}

[Test]
public void Target_glyph_has_no_faction_shape_nodes_and_all_has_two_endpoints()
{
    var prefab = Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath);
    var direct = DirectChildNames(prefab.transform);
    CollectionAssert.DoesNotContain(direct, "AllyDirection");
    CollectionAssert.DoesNotContain(direct, "EnemyDirection");
    CollectionAssert.AreEqual(
        new[] { "LeftDiamond", "Rail", "RightDiamond" },
        DirectChildNames(Child(prefab.transform, "All")));
    Assert.IsEmpty(prefab.GetComponentsInChildren<Outline>(true));
}

[Test]
public void Target_and_description_prefabs_share_the_same_faction_palette()
{
    var glyph = Load<TargetGlyphView>(CardPrefabCatalogTests.TargetGlyphPath);
    var line = Load<DescriptionLineView>(CardPrefabCatalogTests.DescriptionLinePath);
    Assert.AreEqual(
        CardPrefabCatalogTests.Field<Color>(glyph, "_allyColor"),
        CardPrefabCatalogTests.Field<Color>(line, "_allySymbolColor"));
    Assert.AreEqual(
        CardPrefabCatalogTests.Field<Color>(glyph, "_enemyColor"),
        CardPrefabCatalogTests.Field<Color>(line, "_enemySymbolColor"));
}

private static float ActiveGraphicBoundsWidth(Transform root)
{
    var corners = new Vector3[4];
    var min = float.PositiveInfinity;
    var max = float.NegativeInfinity;
    foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
    {
        graphic.rectTransform.GetWorldCorners(corners);
        min = Mathf.Min(min, corners[0].x);
        max = Mathf.Max(max, corners[2].x);
    }
    return max - min;
}
```

- [x] **Step 2: Run the focused test and confirm RED**

Run Task 3 Step 2 with results `/private/tmp/target-glyph-red.xml`. Expected: old direction nodes, outline/fill differences, five-rail `All`, and old serialized fields fail.

- [x] **Step 3: Implement authored visual selection and color application**

```csharp
[SerializeField] private RectTransform _frontOneVisual;
[SerializeField] private RectTransform _frontTwoVisual;
[SerializeField] private RectTransform _backOneVisual;
[SerializeField] private RectTransform _backTwoVisual;
[SerializeField] private RectTransform _allVisual;
[SerializeField] private RectTransform _selfVisual;
[SerializeField] private RectTransform _emptyVisual;
[SerializeField] private Color _allyColor;
[SerializeField] private Color _enemyColor;

public void Bind(CardTargetKey? key)
{
    if (!key.HasValue)
    {
        ActivateOnly(_emptyVisual);
        SetMirror(_emptyVisual, false);
        return;
    }

    Validate(key.Value);
    var visual = VisualFor(key.Value.Range);
    ActivateOnly(visual);
    SetMirror(visual, key.Value.Faction == CardTargetFaction.Enemy);
    var color = key.Value.Faction == CardTargetFaction.Ally
        ? _allyColor
        : _enemyColor;
    foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true))
        graphic.color = color;
}
```

`VisualFor` is an exhaustive switch over six ranges. `ActivateOnly` iterates the seven serialized roots. `SetMirror` preserves the authored absolute X scale and changes only its sign. No runtime anchored-position or size mutation is allowed.

- [x] **Step 4: Pause for the user’s TargetGlyphView prefab edit**

Codex provides these Inspector instructions and waits:

1. Open `Assets/Unity/Prefabs/TargetGlyphView.prefab`.
2. Keep the root `LayoutElement` fixed at width `52`, height `32`.
3. Replace the old direct children with seven `RectTransform` roots named `FrontOne`, `FrontTwo`, `BackOne`, `BackTwo`, `All`, `Self`, `Empty`; each root uses the same `52×32` rect and authored scale `(1,1,1)`.
4. Build the canonical ally-facing geometry: `FrontOne = ━━━◇`, `FrontTwo = ━━◇◇`, `BackOne = ◇━━━`, `BackTwo = ◇◇━━`, `All = ◇━━◇`, `Self = ◎`. The left/right active primitive bounds of the five positional roots must all be `48±0.5` units wide.
5. Build `Empty` from the existing circle and slash sprites, centered in the same root.
6. Use only `Image` primitives under these roots; remove all `Outline`, `AllyDirection`, and `EnemyDirection` objects.
7. Assign all seven roots to `TargetGlyphView`; set ally `#5DADE2`, enemy `#E85D5D`.
8. Save the prefab and tell Codex the edit is complete.

Enemy orientation is produced by mirroring the canonical root, so do not author separate enemy children.

- [x] **Step 5: Verify the user-authored prefab**

Run `CardFramePrefabTests` and `CardPrefabCatalogTests`. Expected: all six ranges, both faction colors, equal positional widths, self, and neutral empty glyph pass with no missing serialized reference.

- [x] **Step 6: Commit code, tests, and the reviewed prefab**

```bash
git add Assets/Unity/TargetGlyphView.cs \
  Assets/Unity/Prefabs/TargetGlyphView.prefab \
  Assets/Tests/UnityEditMode/CardFramePrefabTests.cs
git commit -m "refactor(ui): color target range glyphs"
```

---

### Task 5: Lock execution and intervention form-factor contracts

**Files:**
- User-review/modify if needed: `Assets/Unity/Prefabs/ExecutionCardView.prefab`
- User-review/modify if needed: `Assets/Unity/Prefabs/InterventionCardView.prefab`
- Modify: `Assets/Tests/UnityEditMode/CardFramePrefabTests.cs`
- Modify: `Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs`

**Interfaces:**
- Execution `SymbolOnlyTargetPanel` owns one centered `HorizontalLayoutGroup`.
- `CardView` iterates `TargetEntries`, already sorted `Ally` then `Enemy`; no C# coordinate branch is added.
- Intervention prefab has null `_targetContent` and `_targetPanel`; it never creates `TargetGlyphView` outside description lines.

- [x] **Step 1: Add RED layout-contract tests**

```csharp
[Test]
public void Execution_target_panel_is_one_centered_horizontal_row()
{
    var panel = Child(LoadExecution().transform, "SymbolOnlyTargetPanel");
    var layout = panel.GetComponent<HorizontalLayoutGroup>();

    Assert.IsNotNull(layout);
    Assert.AreEqual(TextAnchor.MiddleCenter, layout.childAlignment);
    Assert.IsFalse(layout.childForceExpandWidth);
    Assert.IsFalse(layout.childForceExpandHeight);
}

[Test]
public void Two_factions_bind_ally_left_enemy_right_on_the_same_y()
{
    var view = InstantiateConfigured(LoadExecution());
    view.Bind(CardPrefabCatalogTests.Presentation(
        CardCategory.Execution,
        new[]
        {
            new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self),
            new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne)
        },
        Array.Empty<CardDescriptionLine>()), null);

    var content = CardPrefabCatalogTests.Field<RectTransform>(view, "_targetContent");
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    var ally = (RectTransform)content.GetChild(0);
    var enemy = (RectTransform)content.GetChild(1);
    Assert.Less(ally.anchoredPosition.x, enemy.anchoredPosition.x);
    Assert.AreEqual(ally.anchoredPosition.y, enemy.anchoredPosition.y, 0.01f);
}
```

Also assert:

- a single target child center equals the panel center within `0.5f`;
- zero execution targets create exactly one `Empty` visual;
- intervention creates no target-panel glyph and no `Empty`;
- `ExpandedDescriptionPanel` starts at the execution target panel top and ends at the execution description panel bottom within `0.5f`, reclaiming the authored inter-panel gap as well.

```csharp
[Test]
public void One_target_centers_and_no_target_is_execution_only()
{
    var execution = InstantiateConfigured(LoadExecution());
    execution.Bind(CardPrefabCatalogTests.Presentation(
        CardCategory.Execution,
        new[]
        {
            new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.Self)
        },
        Array.Empty<CardDescriptionLine>()), null);
    var content = CardPrefabCatalogTests.Field<RectTransform>(execution, "_targetContent");
    Canvas.ForceUpdateCanvases();
    LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    Assert.AreEqual(0f, ((RectTransform)content.GetChild(0)).anchoredPosition.x, 0.5f);

    var intervention = InstantiateConfigured(LoadIntervention());
    intervention.Bind(CardPrefabCatalogTests.Presentation(
        CardCategory.Intervention,
        Array.Empty<CardTargetKey>(),
        new[] { new CardDescriptionLine(null, "순서를 바꾼다.") }), null);
    Assert.IsEmpty(intervention.GetComponentsInChildren<TargetGlyphView>(true));
}

[Test]
public void Intervention_description_reclaims_the_target_region_and_gap()
{
    var execution = LoadExecution();
    var intervention = LoadIntervention();
    var target = Child(execution.transform, "SymbolOnlyTargetPanel");
    var description = Child(execution.transform, "DescriptionPanel");
    var expanded = Child(intervention.transform, "ExpandedDescriptionPanel");
    var gap = target.anchoredPosition.y
        - target.rect.height
        - description.anchoredPosition.y;
    var expected = target.rect.height + gap + description.rect.height;
    Assert.AreEqual(target.anchoredPosition.y, expanded.anchoredPosition.y, 0.5f);
    Assert.AreEqual(
        expected,
        expanded.rect.height,
        0.5f);
}
```

- [x] **Step 2: Run form-factor tests and identify only authored-layout failures**

Run `CardFramePrefabTests` to `/private/tmp/card-form-factor-red.xml`. Do not change `CardView.BindTargetEntries` unless a test proves its category branch violates the contract.

- [x] **Step 3: Pause for the user’s full-card prefab review**

For `ExecutionCardView.prefab`, the user verifies:

1. `SymbolOnlyTargetPanel` remains between `ArtPanel` and `DescriptionPanel`.
2. Its `HorizontalLayoutGroup` uses Middle Center, left/right padding `8`, spacing `8`, no force expansion, no reverse arrangement.
3. `_targetContent` and `_targetPanel` reference this panel.
4. One `52`-wide glyph centers; two glyphs occupy `128` units including padding/spacing and remain inside the `140`-wide panel.

For `InterventionCardView.prefab`, the user verifies:

1. no `SymbolOnlyTargetPanel`, target-content child, or `ExecutionOrderBadge` exists;
2. `ExpandedDescriptionPanel` begins at the execution target region’s Y and has height `118`, reclaiming target `40` + authored gap `6` + execution description `72`; both card types end the description region at the same Y;
3. `CardView._targetContent`, `_targetPanel`, `_executionOrderBadge` remain unassigned.

The user saves only if Inspector values differ.

- [x] **Step 4: Run form-factor, catalog, and bind regression tests**

Run `CardFramePrefabTests`, `CardPrefabCatalogTests`, and `CardPresentationTests`. Expected: execution 0/1/2 states and intervention form factor all pass.

- [x] **Step 5: Commit only if a full-card prefab changed**

```bash
git add Assets/Unity/Prefabs/ExecutionCardView.prefab \
  Assets/Unity/Prefabs/InterventionCardView.prefab \
  Assets/Tests/UnityEditMode/CardFramePrefabTests.cs \
  Assets/Tests/UnityEditMode/CardPrefabCatalogTests.cs
git commit -m "fix(ui): lock card form factor layouts"
```

If neither full-card prefab changes, include only the tests in the nearest relevant Task 4 commit rather than creating an empty layout commit.

---

### Task 6: Retire unused poster frames and update the visual checklist

> **2026-08-04 plan correction:** the former `CardCodeGenerator`/`CardAsset` validation work is
> superseded. The current `master` has already removed both types during JSON runtime-content
> conversion, and [the document index](../README.md#후속-작업-대기열) defers the remaining JSON
> status projection. Do not restore or extend the legacy SO/code-generation path on this branch.

**Files:**
- Modify: `Assets/Unity/PLAYTEST.md`
- Delete only after an empty reference audit: seven `Assets/Unity/Resources/Cards/Frame/fw_*_poster_v2.png` files and matching `.meta`

**Interfaces:**
- The primitive sprites and prefabs remain the only card-frame presentation assets.
- No JSON, SO authoring, runtime card-status projection, scene, or prefab change belongs to this task.

- [x] **Step 1: Audit poster GUID references**

```bash
for meta in Assets/Unity/Resources/Cards/Frame/fw_*_poster_v2.png.meta; do
  guid=$(sed -n 's/^guid: //p' "$meta")
  rg -n "$guid" Assets --glob '!*.meta' || true
done
```

Every search was empty on 2026-08-04. Delete only the seven audited PNG/meta pairs.

- [x] **Step 2: Delete the audited poster assets**

Remove the seven tracked PNG/meta pairs. Do not delete primitive sprites or any status-grid/tooltip asset.

- [x] **Step 3: Update the manual visual checklist**

`PLAYTEST.md` must include:

- `Enemy → Ally → Enemy` renders two grouped description lines;
- both description symbols are `◆`, red/blue only;
- ally front points right and enemy front points left;
- `All` matches other positional glyph widths;
- target panel states 0/1/2 remain horizontal and centered;
- no-target execution shows `∅`;
- intervention has no target panel/`∅` and uses the expanded description region;
- mixed hands at 4:3, 16:10, 16:9, 21:9;
- latest hovered/held card remains topmost after resize.

- [x] **Step 4: Run focused tests and commit cleanup**

```bash
git add Assets/Unity/PLAYTEST.md Assets/Unity/Resources/Cards/Frame
git commit -m "chore(ui): retire poster card frame assets"
```

---

### Task 7: Run full verification and archive the plan

**Files:**
- Move after successful verification: this plan to `docs/superpowers/archive/plans/2026-07-31-primitive-card-frame.md`
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/archive/README.md`

- [ ] **Step 1: Run the full headless suite**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --nologo
```

Expected: all pass.

- [ ] **Step 2: Run the full Unity EditMode suite**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath /Users/ish/Git/rogue-deck-card-frame-design \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/primitive-card-frame-editmode.xml \
  -logFile /private/tmp/primitive-card-frame-editmode.log
```

Expected: XML `result="Passed"`; no compile, missing-script/reference, or import failure.

- [ ] **Step 3: Run structural audits**

```bash
rg -n '◇◎|◎◆|AllyDirection|EnemyDirection|_glyphSlot|_allyFill|_enemyFill' \
  Assets/Core Assets/Unity Assets/Tests Tests/Headless
rg -n 'Resources\.Load|GameObject\.Find|FindObjectOfType' \
  Assets/Unity/CardView.cs Assets/Unity/CardPrefabCatalog.cs \
  Assets/Unity/TargetGlyphView.cs Assets/Unity/DescriptionLineView.cs
git diff --check
git status --short
```

Expected: both searches empty and only intended local tool artifacts remain untracked.

- [ ] **Step 4: Render captures for user review**

Run `FateWeaver.Tests.UnityEditMode.CardFrameRenderCapture`. Inspect:

- execution and intervention at `1280×720`;
- toxic reclaim with blue `◎` left and red front-one glyph right;
- mixed five-card hands at `960×720`, `1280×800`, `1280×720`, `1680×720`.

Captures stay under `/private/tmp/primitive-card-frame-captures/`. Present them to the user and wait for visual approval before archiving.

- [ ] **Step 5: Archive the completed plan and update indexes**

Move this file to `docs/superpowers/archive/plans/`, remove its active row from `docs/superpowers/README.md`, add the archived row to `docs/superpowers/archive/README.md`, and keep the design spec current.

- [ ] **Step 6: Commit the completion record**

```bash
git add docs/superpowers/README.md docs/superpowers/archive/README.md \
  docs/superpowers/archive/plans/2026-07-31-primitive-card-frame.md
git commit -m "docs: archive primitive card frame implementation"
```

- [ ] **Step 7: Confirm branch state**

```bash
git status --short
git log --oneline --decorate -12
```

Expected: no uncommitted project file on `refactor/card-frame-design`. Do not merge to `master` without explicit user approval.
