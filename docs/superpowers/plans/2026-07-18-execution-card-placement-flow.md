# Execution Card Targetless Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 모든 실행 카드를 무대상 규칙으로 고정하고, 손패 호버의 정적 레일 실루엣과 손패 클릭 후 DOTween 펄스 실루엣 클릭으로 배치하는 흐름을 완성한다.

**Architecture:** 순수 코어는 실행 카드 프리뷰와 실제 배치를 대상 입력 없이 제공하고, 직접 대상을 요구하는 기본 실행 카드 정의를 세션 생성 시 거부한다. Unity 레이어는 손패 호버를 별도 이벤트로 전달하고 `ExecutionRailView`가 하나의 프리뷰를 `Hidden`, `HoverStatic`, `SelectedPulse` 상태로 관리하며, `CardSelectionController`의 기존 `ConfirmPlacement` 상태가 실루엣 클릭을 최종 배치 결과로 변환한다.

**Tech Stack:** C# 9, .NET/NUnit headless tests, Unity 6000.5.2f1, uGUI, Unity Input System, DOTween Free v1.3.030, Unity EditMode tests

## Global Constraints

- `FateWeaver.Core`와 `FateWeaver.Simulation`은 UnityEngine 및 DOTween을 참조하지 않는다.
- 실행 카드는 플레이어 직접 대상을 받지 않으며 `PlayExecutionCard(int handIndex)`만 제공한다.
- 조작 카드(`CardCategory.Intervention`)의 단일·다중 레일 대상 선택과 확인 흐름은 바꾸지 않는다.
- 호버 프리뷰는 운명력 부족 상태에서도 보이지만 실제 배치는 운명력을 다시 검증한다.
- 프리뷰는 운명력, 손패, 버림패, 미래 영역, 인스턴스 ID와 RNG를 변경하지 않는다.
- 프리뷰와 실제 배치는 같은 상태 효과 계산과 `FutureZone` 정렬 규칙을 공유한다.
- 호버 실루엣은 정적이고 입력 불가이며, 손패 클릭 후에만 `1.0 ↔ 1.06`, 편도 `0.45`초, `Ease.InOutSine`, `LoopType.Yoyo`, `SetUpdate(true)` 펄스를 사용한다.
- tween 종료 시 소유한 `Tween`만 kill하고 스케일을 `Vector3.one`으로 복원한다. `DOTween.KillAll()`은 금지한다.
- DOTween은 공식 무료판 v1.3.030 배포본을 수정하지 않고 `Assets/Plugins/Demigiant`에 보존한다.
- 새 카드/레일 프리팹을 만들거나 씬·프리팹 YAML을 직접 수정하지 않는다.
- 기존 사용자 변경인 `Assets/Scenes/FateWeaverBattle.unity`, `RailCardView.prefab`, `UnitView.prefab`, `KoreanTMP.asset`, `TargetingArrowView.prefab`은 스테이징하거나 덮어쓰지 않는다.
- 새 규칙 로직은 Unity 없이 `dotnet test` 가능한 헤드리스 테스트를 먼저 작성한다.

## File Map

- Create `Assets/Plugins/Demigiant/DOTween/**`: 공식 DOTween v1.3.030 바이너리, 모듈, 에디터 도구, readme와 메타데이터.
- Create `Assets/Resources/DOTweenSettings.asset`: DOTween Utility Panel이 생성하는 설정과 asmdef 유지 정보.
- Create `Assets/Tests/UnityEditMode/DotweenDependencyTests.cs`: 의존성 로드 스모크 테스트.
- Modify `Assets/Unity/FateWeaver.Unity.asmdef`: 생성된 `DOTween.Modules` 어셈블리 참조.
- Modify `Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef`: DOTween 모듈 및 `DOTween.dll` 테스트 참조.
- Modify `Assets/Core/Simulation/DeckCombatSession.cs`: 무대상 실행 카드 배치, 비용과 분리된 위치 프리뷰, 부팅 검증.
- Modify `Assets/Core/Combat/FutureZone.cs`: 프리뷰와 실제 배치가 공유하는 무변이 삽입 위치 계산을 함께 커밋.
- Modify `Assets/Core/Combat/PartyTargetRules.cs`: 기본 실행 카드 직접 대상 불변식 판정.
- Modify `Assets/Core/Simulation/PartyPrototypeDeck.cs`: 레거시 검증 카드를 `Self`로 마이그레이션.
- Modify `Assets/Core/Simulation/Authoring/PartyPrototypeDeckSpecs.cs`: 저작 스펙을 `Self`로 마이그레이션.
- Modify `Assets/Unity/CardSO/Validation/fixture_selected_block.asset`: 기존 카드 시드 파이프라인으로 실제 전투 덱 SO를 `Self`로 재생성.
- Modify `Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs`: 무대상 배치와 직접 대상 정의 거부 테스트.
- Modify `Assets/Core/Tests/EditMode/FutureZoneTests.cs`: 삽입 위치 프리뷰의 정렬·무변이 회귀 테스트.
- Modify `Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs`: 검증 카드 자동 대상 테스트.
- Modify `Assets/Unity/ExecutionRailView.cs`: 실루엣 표시 상태, 클릭, DOTween 펄스, 상세 호버 공존.
- Modify `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`: 정적/펄스/클릭/정리/상세 호버 테스트.
- Modify `Assets/Unity/HandCardHoverEffect.cs`: 카드별 호버 상태 콜백.
- Modify `Assets/Unity/HandFanView.cs`: 클릭과 호버 인덱스 전달.
- Create `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`: 손패 호버 전달 테스트.
- Modify `Assets/Unity/CardSelectionController.cs`: 호버 프리뷰와 배치 대기 소유권, 마우스 추적 제거.
- Modify `Assets/Unity/BattleScreenController.cs`: 실행/조작 카드 분기와 무대상 적용.
- Modify `Assets/Unity/Editor/BattleSceneBuilder.cs`: 제거된 선택 컨트롤러 직렬화 필드 배선 정리.
- Modify `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`: 실루엣 클릭 배치와 조작 카드 회귀 테스트.
- Modify `Assets/Unity/PLAYTEST.md`: 새 실행 카드 입력과 DOTween 수동 검증.

---

### Task 1: Vendor and wire DOTween Free v1.3.030

**Files:**
- Create: `Assets/Plugins/Demigiant/DOTween/**`
- Create: `Assets/Resources/DOTweenSettings.asset`
- Create: `Assets/Tests/UnityEditMode/DotweenDependencyTests.cs`
- Modify: `Assets/Unity/FateWeaver.Unity.asmdef`
- Modify: `Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef`

**Interfaces:**
- Produces: namespace `DG.Tweening`, types `Tween`, `DOTween`, `Ease`, `LoopType`, and `ShortcutExtensions.DOScale(Transform, Vector3, float)`.
- Produces: assembly reference `DOTween.Modules` and precompiled reference `DOTween.dll`.
- Consumes: official archive `https://dotween.demigiant.com/downloads/DOTween_1_3_030.zip`.

- [ ] **Step 1: Write the failing dependency smoke test**

Create `Assets/Tests/UnityEditMode/DotweenDependencyTests.cs`:

```csharp
using DG.Tweening;
using NUnit.Framework;

namespace FateWeaver.Tests.UnityEditMode
{
    public class DotweenDependencyTests
    {
        [Test]
        public void Dotween_runtime_is_available_to_unity_tests()
        {
            Assert.IsNotNull(typeof(DOTween));
            Assert.IsNotNull(typeof(Tween));
        }
    }
}
```

- [ ] **Step 2: Run Unity compilation and verify RED**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.DotweenDependencyTests \
  -testResults /private/tmp/dotween-dependency-red.xml \
  -logFile /private/tmp/dotween-dependency-red.log \
  -quit
```

Expected: compilation fails with `CS0246` for namespace `DG.Tweening`. If another Unity process owns the project, record that exact failure and use the generated Unity project compilation after the import for GREEN; do not claim Test Runner execution.

- [ ] **Step 3: Download and verify the official archive**

Run:

```bash
curl -fL https://dotween.demigiant.com/downloads/DOTween_1_3_030.zip \
  -o /private/tmp/DOTween_1_3_030.zip
echo "62a0ececd274e1587eb0dea15f3afab392fbda5a0f8cac7287fbf7f64925a1ba  /private/tmp/DOTween_1_3_030.zip" \
  | shasum -a 256 -c -
unzip -o /private/tmp/DOTween_1_3_030.zip -d /private/tmp/DOTween_1_3_030
echo "e4d89e791ae2ed11d49d5f27bfe7d923c0942cefa8eabf19e18af86274d2cabf  /private/tmp/DOTween_1_3_030/DOTween_1_3_030.unityPackage" \
  | shasum -a 256 -c -
```

Expected: both checksum checks print `OK`.

- [ ] **Step 4: Import the official package verbatim**

Close any Unity instance that owns this worktree, then run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -importPackage /private/tmp/DOTween_1_3_030/DOTween_1_3_030.unityPackage \
  -logFile /private/tmp/dotween-import.log \
  -quit
rg -n "DOTween and DOTween Pro are copyright" \
  Assets/Plugins/Demigiant/DOTween/readme.txt
```

Expected: `Assets/Plugins/Demigiant/DOTween/DOTween.dll`, `DOTween.XML`, `readme.txt`, `Modules/`, and `Editor/` exist with their official `.meta` files; the copyright search matches the official readme. Do not modify vendored DOTween files.

- [ ] **Step 5: Run DOTween setup and generate asmdefs**

Open the worktree in Unity and perform exactly:

```text
Tools > Demigiant > DOTween Utility Panel
Setup DOTween...
Apply
Generate ASMDEF
```

Keep only built-in Unity modules required by the imported free package; do not activate modules for absent third-party assets. Save the project, then verify:

```bash
test -f Assets/Resources/DOTweenSettings.asset
test -f Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef
rg -n '"name": "DOTween.Modules"' \
  Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef
```

Expected: all commands exit 0. The official DOTween setup is an explicit user-approved dependency action; if the Utility Panel cannot run because Unity licensing is unavailable, stop Task 1 and request the user to perform these four UI actions before implementation continues.

- [ ] **Step 6: Add exact assembly references**

In `Assets/Unity/FateWeaver.Unity.asmdef`, add `DOTween.Modules` to `references`:

```json
"references": [
    "FateWeaver.Core",
    "FateWeaver.Simulation",
    "Unity.TextMeshPro",
    "UnityEngine.UI",
    "Unity.InputSystem",
    "DOTween.Modules"
]
```

In `Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef`, add the module reference and precompiled DLL:

```json
"references": [
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner",
    "FateWeaver.Core",
    "FateWeaver.Simulation",
    "FateWeaver.Unity",
    "Unity.TextMeshPro",
    "DOTween.Modules"
],
"precompiledReferences": [
    "nunit.framework.dll",
    "DOTween.dll"
]
```

- [ ] **Step 7: Run the dependency smoke test and verify GREEN**

Run:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.DotweenDependencyTests \
  -testResults /private/tmp/dotween-dependency-green.xml \
  -logFile /private/tmp/dotween-dependency-green.log \
  -quit
```

Expected: one test passes, zero failures, and the Unity log has no DOTween setup or missing-reference errors.

- [ ] **Step 8: Commit the dependency**

```bash
git diff --check -- Assets/Unity/FateWeaver.Unity.asmdef \
  Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef \
  Assets/Tests/UnityEditMode/DotweenDependencyTests.cs
git add Assets/Plugins.meta Assets/Plugins/Demigiant \
  Assets/Resources.meta Assets/Resources/DOTweenSettings.asset \
  Assets/Resources/DOTweenSettings.asset.meta \
  Assets/Unity/FateWeaver.Unity.asmdef \
  Assets/Tests/UnityEditMode/FateWeaver.Tests.UnityEditMode.asmdef \
  Assets/Tests/UnityEditMode/DotweenDependencyTests.cs \
  Assets/Tests/UnityEditMode/DotweenDependencyTests.cs.meta
git commit -m "build(unity): add DOTween runtime"
```

Do not stage any other generated Resources, scene, prefab, font, or arrow assets.

---

### Task 2: Enforce targetless execution cards in the pure core

**Files:**
- Modify: `Assets/Core/Combat/FutureZone.cs`
- Modify: `Assets/Core/Combat/PartyTargetRules.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Modify: `Assets/Core/Simulation/PartyPrototypeDeck.cs`
- Modify: `Assets/Core/Simulation/Authoring/PartyPrototypeDeckSpecs.cs`
- Modify: `Assets/Unity/CardSO/Validation/fixture_selected_block.asset`
- Modify: `Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs`
- Modify: `Assets/Core/Tests/EditMode/FutureZoneTests.cs`
- Modify: `Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs`
- Modify: `Assets/Unity/BattleScreenController.cs` (새 코어 시그니처를 위한 최소 컴파일 연결만)

**Interfaces:**
- Produces: `int FutureZone.PreviewInsertionIndex(ExecutionCardInstance candidate)` using the same stable ordering as `ResolutionOrder()`.
- Produces: `bool PartyTargetRules.IsValidBaseExecutionDefinition(CardDefinition definition)`.
- Produces: `bool DeckCombatSession.PlayExecutionCard(int handIndex)` with no target parameter.
- Produces: `bool DeckCombatSession.TryPreviewExecutionPlacement(int handIndex, out ExecutionPlacementPreview preview)` that does not reject only for insufficient energy.
- Consumes: existing `EffectiveExecutionOrderFor(OwnedCard)` and `FutureZone.PreviewInsertionIndex(ExecutionCardInstance)`.

- [ ] **Step 1: Write failing validation and targetless-play tests**

Replace the legacy direct-target placement tests in `PartyDeckCombatSessionTests.cs` with:

```csharp
[Test]
public void Session_rejects_player_execution_card_that_requires_direct_target()
{
    var direct = DirectBlock();

    Assert.Throws<ArgumentException>(() => Session(new[]
    {
        Loadout("a", new[] { direct }),
        Loadout("b")
    }));
}

[Test]
public void Targetless_execution_play_spends_energy_and_places_owned_card()
{
    var session = Session(new[]
    {
        Loadout("a", new[] { Execution("guard") })
    }, new[] { EnemyStrike(damage: 0) });
    int energyBefore = session.FateEnergy;

    Assert.IsTrue(session.PlayExecutionCard(0));

    var placed = session.CurrentOrder.Single(card => card.Def.Id == "guard");
    Assert.AreEqual("a", placed.OwnerId);
    Assert.IsNull(placed.TargetId);
    Assert.AreEqual(energyBefore - placed.Def.EnergyCost, session.FateEnergy);
}

[Test]
public void Unaffordable_execution_card_still_returns_read_only_position_preview()
{
    var session = Session(new[]
    {
        Loadout("a", new[] { Execution("costly", cost: 4, order: 3) })
    }, new[] { EnemyStrike(order: 5, damage: 0) }, fateEnergyPerTurn: 3);
    int energyBefore = session.FateEnergy;
    var handBefore = session.Hand.ToArray();
    var orderBefore = session.CurrentOrder.ToArray();

    Assert.IsTrue(session.TryPreviewExecutionPlacement(0, out var preview));
    Assert.AreEqual(3, preview.ExecutionOrder);
    Assert.AreEqual(0, preview.InsertionIndex);
    Assert.AreEqual(energyBefore, session.FateEnergy);
    CollectionAssert.AreEqual(handBefore, session.Hand);
    CollectionAssert.AreEqual(orderBefore, session.CurrentOrder);
    Assert.IsFalse(session.PlayExecutionCard(0));
}
```

Add a legacy-constructor test using the existing legacy session helper or direct constructor:

```csharp
[Test]
public void Legacy_session_also_rejects_direct_target_execution_definition()
{
    Assert.Throws<ArgumentException>(() => new DeckCombatSession(
        new[] { DirectBlock() },
        playerHp: 30,
        enemies: Array.Empty<Enemy>(),
        enemyPolicy: new EnemyIntent(Array.Empty<IReadOnlyList<CardDefinition>>())));
}
```

Update the existing `Placement_preview_rejects_invalid_unaffordable_nonexecution_and_resolved_turn` regression test: rename it to `Placement_preview_rejects_invalid_nonexecution_and_resolved_turn`, remove only the unaffordable rejection setup/assertion, and retain the negative-index, wrong-category, and resolved-turn assertions. The new `Unaffordable_execution_card_still_returns_read_only_position_preview` test owns the changed affordability expectation. Keep the existing status-adjusted preview/real-position equivalence and RNG non-mutation tests unchanged. Preserve and commit the existing `FutureZoneTests` coverage for before/between/after insertion, player/enemy ties, and zone non-mutation; these tests are the RED/GREEN evidence for the shared ordering helper already present as uncommitted prerequisite work.

- [ ] **Step 2: Write the failing prototype migration test**

Replace `Selected_block_requires_explicit_ally_target` in `PartyPrototypeDataTests.cs` with:

```csharp
[Test]
public void Owner_block_uses_self_without_direct_target_selection()
{
    var ownerBlock = PartyPrototypeDeck.Build()
        .First(card => card.Id == "fixture_selected_block");

    Assert.IsTrue(PartyTargetRules.IsValidBaseExecutionDefinition(ownerBlock));
    Assert.IsFalse(PartyTargetRules.RequiresExplicitAllyTarget(ownerBlock));
    Assert.AreEqual(StatusApplyTarget.Self, ownerBlock.Effects.Single().StatusTarget);
}
```

- [ ] **Step 3: Run focused headless tests and verify RED**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --no-restore \
  --filter "FullyQualifiedName~PartyDeckCombatSessionTests|FullyQualifiedName~PartyPrototypeDataTests"
```

Expected: compile or assertion failures because `IsValidBaseExecutionDefinition` does not exist, direct-target sessions are accepted, unaffordable preview is rejected, and the prototype still uses `PartyMember`.

- [ ] **Step 4: Add the authored execution-card invariant**

Add to `PartyTargetRules.cs`:

```csharp
public static bool IsValidBaseExecutionDefinition(CardDefinition definition)
{
    if (definition == null)
    {
        return false;
    }

    return definition.Side != Side.Player
        || definition.Category != CardCategory.Execution
        || !RequiresExplicitAllyTarget(definition);
}
```

In the private `DeckCombatSession` constructor, call this before `_allCards` and `_deck` are created:

```csharp
ValidateBaseExecutionDefinitions(deckCards);
```

Add the helper to `DeckCombatSession.cs`:

```csharp
private static void ValidateBaseExecutionDefinitions(IReadOnlyList<OwnedCard> cards)
{
    if (cards == null)
    {
        throw new System.ArgumentException("Deck cards are required.");
    }

    foreach (var card in cards)
    {
        if (card == null || card.Def == null)
        {
            throw new System.ArgumentException("Deck contains an invalid owned card.");
        }

        if (!PartyTargetRules.IsValidBaseExecutionDefinition(card.Def))
        {
            throw new System.ArgumentException(
                "Player execution cards cannot require a directly selected target: "
                + card.Def.Id);
        }
    }
}
```

- [ ] **Step 5: Make preview affordability-independent and placement targetless**

Change the preview guard in `TryPreviewExecutionPlacement` to:

```csharp
var card = _deck.Hand[handIndex];
if (card.Def.Category != CardCategory.Execution)
{
    return false;
}
```

Replace the placement signature and remove direct-target validation and assignment:

```csharp
public bool PlayExecutionCard(int handIndex)
{
    if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
    {
        return false;
    }

    var card = _deck.Hand[handIndex];
    var def = card.Def;
    if (def.Category != CardCategory.Execution || _state.FateEnergy < def.EnergyCost)
    {
        return false;
    }

    _state.FateEnergy -= def.EnergyCost;
    var placed = new ExecutionCardInstance(def)
    {
        InstanceId = _nextInstanceId++,
        OwnerId = card.OwnerId,
        ExecutionOrder = EffectiveExecutionOrderFor(card)
    };
    _state.Zone.Add(placed);
    _deck.DiscardFromHand(handIndex);
    return true;
}
```

Update every core call site from `PlayExecutionCard(index, targetId)` to `PlayExecutionCard(index)`. Also make the minimal compile-bridge change in `BattleScreenController.TryApplySelection` from `_session.PlayExecutionCard(result.HandIndex, targetId)` to `_session.PlayExecutionCard(result.HandIndex)`; Task 4 removes that branch's obsolete target parsing and messages completely. Direct-target tests that exercise `ApplyStatusHandler` through a manually created `ExecutionCardInstance.TargetId` remain valid because internal resolution targeting is not deleted.

- [ ] **Step 6: Migrate the validation fixture to `Self`**

In `PartyPrototypeDeck.SelectedBlock`, use:

```csharp
EffectData.ApplyStatus(
    StatusKeys.Block,
    StatusLifetime.ThisTurn,
    StatusApplyTarget.Self,
    BlockMagnitude)
```

In `PartyPrototypeDeckSpecs.SelectedBlock`, use:

```csharp
Target = StatusApplyTarget.Self
```

Do not rename `fixture_selected_block` in this change; preserving the ID keeps authored/generated equivalence stable while the behavior migrates.

- [ ] **Step 7: Regenerate the referenced validation CardAsset through the existing pipeline**

The current battle scene references `member_b.asset`, whose deck references `fixture_selected_block.asset`; leaving that SO at `PartyMember` would make session boot fail after the new invariant. With the worktree closed in any interactive Unity editor, run the existing idempotent authoring command:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -executeMethod FateWeaver.Unity.Editor.CardCodeGenerator.SeedPartyPrototype \
  -logFile /private/tmp/seed-targetless-party-prototype.log \
  -quit
git diff -- Assets/Unity/CardSO/Validation/fixture_selected_block.asset
```

Expected: the fixture's serialized `Target` changes from `2` (`PartyMember`) to `0` (`Self`) and no art reference changes. Review `git status`; do not stage scene, prefab, font, or unrelated generated changes. This is generated output from the repository's supported ScriptableObject authoring path—do not hand-edit the YAML.

- [ ] **Step 8: Run focused and full headless GREEN verification**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --no-restore \
  --filter "FullyQualifiedName~PartyDeckCombatSessionTests|FullyQualifiedName~PartyPrototypeDataTests"
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --no-restore
rg -n "PlayExecutionCard\([^)]*," Assets/Core -g '*.cs'
```

Expected: focused and full suites have zero failures; signature search returns no matches. Then compile the Unity production assemblies to verify the signature bridge and regenerated SO import:

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.CardPresentationTests \
  -testResults /private/tmp/targetless-core-unity-compile.xml \
  -logFile /private/tmp/targetless-core-unity-compile.log \
  -quit
```

Expected: Unity compilation succeeds and the focused test has zero failures. If an interactive editor owns the project, record that exact limitation and perform this compile check before Task 3.

- [ ] **Step 9: Commit the core invariant**

```bash
git diff --check -- Assets/Core/Combat/FutureZone.cs \
  Assets/Core/Combat/PartyTargetRules.cs \
  Assets/Core/Simulation/DeckCombatSession.cs \
  Assets/Core/Simulation/PartyPrototypeDeck.cs \
  Assets/Core/Simulation/Authoring/PartyPrototypeDeckSpecs.cs \
  Assets/Unity/CardSO/Validation/fixture_selected_block.asset \
  Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs \
  Assets/Core/Tests/EditMode/FutureZoneTests.cs \
  Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs \
  Assets/Unity/BattleScreenController.cs
git add Assets/Core/Combat/FutureZone.cs \
  Assets/Core/Combat/PartyTargetRules.cs \
  Assets/Core/Simulation/DeckCombatSession.cs \
  Assets/Core/Simulation/PartyPrototypeDeck.cs \
  Assets/Core/Simulation/Authoring/PartyPrototypeDeckSpecs.cs \
  Assets/Unity/CardSO/Validation/fixture_selected_block.asset \
  Assets/Core/Tests/EditMode/PartyDeckCombatSessionTests.cs \
  Assets/Core/Tests/EditMode/FutureZoneTests.cs \
  Assets/Core/Tests/EditMode/PartyPrototypeDataTests.cs \
  Assets/Unity/BattleScreenController.cs
git commit -m "refactor(core): make execution placement targetless"
```

---

### Task 3: Give the rail preview static and DOTween-pulsing states

**Files:**
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Modify: `Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs`

**Interfaces:**
- Consumes: `DG.Tweening.Tween`, `ShortcutExtensions.DOScale`, `Ease.InOutSine`, `LoopType.Yoyo`.
- Produces: `void ExecutionRailView.ShowPlacementHover(CardPresentation card, int insertionIndex)`.
- Produces: `void ExecutionRailView.ArmPlacementPreview(Action onClick)`.
- Produces: `void ExecutionRailView.ClearPlacementPreview()`.
- Removes: `IPointerEnterHandler`, `IPointerExitHandler`, `OnPointerEnter`, `OnPointerExit`, and the old rail-entry-gated preview behavior.
- Removes: whole-rail confirmation (`SetRailClicked`, `_onRailClicked`, `_railClickButton`, and its `Awake` listener); only the armed silhouette confirms placement.

- [ ] **Step 1: Replace the old preview tests with failing state tests**

Add `using DG.Tweening;` to `ExecutionRailInputTests.cs`, then replace the pointer-enter preview test with:

```csharp
[Test]
public void Hover_preview_is_immediate_static_translucent_and_noninteractive()
{
    var root = new GameObject("Root", typeof(RectTransform));
    var overlay = ChildRect(root.transform, "Overlay");
    try
    {
        var prefab = RailCardView.EditorCreate(
            ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
        var rail = Child<ExecutionRailView>(root.transform, "Rail");
        rail.EditorBuild(null, prefab, overlay);
        var existing = Card("existing", order: 4, side: Side.Enemy);
        var candidate = Card("candidate", order: 3, side: Side.Player);
        rail.SetCards(new[] { existing, existing }, _ => { });

        rail.ShowPlacementHover(candidate, 1);

        var preview = Field<RailCardView>(rail, "_placementPreview");
        Assert.IsTrue(preview.gameObject.activeSelf);
        Assert.AreEqual(1, preview.transform.GetSiblingIndex());
        Assert.AreEqual(Vector3.one, preview.transform.localScale);
        Assert.AreEqual(0.5f, preview.GetComponent<CanvasGroup>().alpha);
        Assert.IsFalse(Field<Button>(preview, "_button").interactable);
        Assert.IsNull(Field<Tween>(rail, "_placementPreviewTween"));
        Assert.AreEqual(BlueOutline, Field<Image>(preview, "_selectionOutline").color);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

Add the helper used above:

```csharp
private static CardPresentation Card(string id, int order, Side side)
    => new CardPresentation(
        id, id, order, 1, side, string.Empty, null, false);
```

- [ ] **Step 2: Add failing armed-pulse and cleanup tests**

Add:

```csharp
[Test]
public void Armed_preview_is_clickable_and_owns_a_yoyo_tween()
{
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
        int clicks = 0;

        rail.ArmPlacementPreview(() => clicks++);

        var preview = Field<RailCardView>(rail, "_placementPreview");
        var tween = Field<Tween>(rail, "_placementPreviewTween");
        Assert.IsTrue(Field<Button>(preview, "_button").interactable);
        Assert.IsTrue(tween.IsActive());
        Field<Button>(preview, "_button").onClick.Invoke();
        Assert.AreEqual(1, clicks);

        rail.ClearPlacementPreview();

        Assert.IsFalse(preview.gameObject.activeSelf);
        Assert.IsFalse(tween.IsActive());
        Assert.AreEqual(Vector3.one, preview.transform.localScale);
        Assert.IsNull(Field<Tween>(rail, "_placementPreviewTween"));
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

Keep and update `Rebuilding_cards_clears_active_placement_preview` to call `ShowPlacementHover` and `ArmPlacementPreview`, then assert the tween is killed and scale reset after `SetCards`.

- [ ] **Step 3: Add the failing rail-detail coexistence test**

Use the serialized card prefab in an EditMode test:

```csharp
[Test]
public void Existing_rail_card_hover_still_opens_detail_while_preview_is_armed()
{
    var root = new GameObject("Root", typeof(RectTransform));
    var overlay = ChildRect(root.transform, "Overlay");
    try
    {
        var fullPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<CardView>(
            "Assets/Unity/Prefabs/CardView.prefab");
        Assert.IsNotNull(fullPrefab);
        var miniPrefab = RailCardView.EditorCreate(
            ChildRect(root.transform, "PrefabRoot"), new Vector2(96f, 132f));
        var rail = Child<ExecutionRailView>(root.transform, "Rail");
        rail.EditorBuild(fullPrefab, miniPrefab, overlay);
        rail.SetCards(new[] { Card("existing", 4, Side.Enemy) }, _ => { });
        rail.ShowPlacementHover(Card("candidate", 3, Side.Player), 0);
        rail.ArmPlacementPreview(() => { });
        var existing = Field<List<RailCardView>>(rail, "_views")[0];

        existing.OnPointerEnter(null);

        var detail = Field<CardView>(rail, "_preview");
        Assert.IsNotNull(detail);
        Assert.IsTrue(detail.gameObject.activeSelf);
    }
    finally
    {
        Object.DestroyImmediate(root);
    }
}
```

- [ ] **Step 4: Run focused Unity tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests \
  -testResults /private/tmp/execution-rail-states-red.xml \
  -logFile /private/tmp/execution-rail-states-red.log \
  -quit
```

Expected: compile failures for `ShowPlacementHover`, `ArmPlacementPreview`, and `_placementPreviewTween`.

- [ ] **Step 5: Implement the three rail preview states**

In `ExecutionRailView.cs`, remove `UnityEngine.EventSystems`, pointer interfaces, `OnPointerEnter`, and `OnPointerExit`. Also remove `SetRailClicked`, `_onRailClicked`, `_railClickButton`, its `Awake` listener, and the background `Button` creation/assignment from `EditorBuild`; the existing serialized scene button may remain inert until a later authorized scene rebuild. Add:

```csharp
using DG.Tweening;
```

Add exact constants and fields:

```csharp
private const float PlacementPreviewAlpha = 0.5f;
private const float PlacementPulseScale = 1.06f;
private const float PlacementPulseHalfDuration = 0.45f;

private RailCardView _placementPreview;
private CanvasGroup _placementPreviewGroup;
private CardPresentation? _placementPreviewCard;
private int _placementPreviewIndex = -1;
private Tween _placementPreviewTween;
```

Replace the old placement APIs with:

```csharp
public void ShowPlacementHover(CardPresentation card, int insertionIndex)
{
    if (insertionIndex < 0 || insertionIndex > _views.Count)
    {
        throw new ArgumentOutOfRangeException(nameof(insertionIndex));
    }

    _placementPreviewCard = card;
    _placementPreviewIndex = insertionIndex;
    EnsurePlacementPreview();
    StopPlacementPulse();
    BindPlacementPreview(null, interactable: false);
    ShowPlacementPreview();
    HidePreview();
}

public void ArmPlacementPreview(Action onClick)
{
    if (!_placementPreviewCard.HasValue || _placementPreview == null)
    {
        throw new InvalidOperationException(
            "Placement hover preview must exist before it can be armed.");
    }

    BindPlacementPreview(onClick, interactable: true);
    StartPlacementPulse();
}

public void ClearPlacementPreview()
{
    StopPlacementPulse();
    _placementPreviewCard = null;
    _placementPreviewIndex = -1;
    if (_placementPreview != null)
    {
        _placementPreview.SetInteractable(false);
        _placementPreview.gameObject.SetActive(false);
    }
}
```

Implement the helpers:

```csharp
private void EnsurePlacementPreview()
{
    if (_placementPreview != null)
    {
        return;
    }

    _placementPreview = Instantiate(_cardPrefab, _content);
    ((RectTransform)_placementPreview.transform).sizeDelta = CardSize;
    _placementPreviewGroup = _placementPreview.gameObject.AddComponent<CanvasGroup>();
    _placementPreviewGroup.alpha = PlacementPreviewAlpha;
    _placementPreview.gameObject.SetActive(false);
}

private void BindPlacementPreview(Action onClick, bool interactable)
{
    _placementPreview.Bind(_placementPreviewCard.Value, onClick, null);
    _placementPreview.SetSelection(CardView.SelectionKind.Secondary);
    _placementPreview.SetInteractable(interactable);
    _placementPreviewGroup.interactable = interactable;
    _placementPreviewGroup.blocksRaycasts = interactable;
}

private void ShowPlacementPreview()
{
    _placementPreview.transform.SetSiblingIndex(_placementPreviewIndex);
    _placementPreview.gameObject.SetActive(true);
    LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
}

private void StartPlacementPulse()
{
    StopPlacementPulse();
    _placementPreviewTween = _placementPreview.transform
        .DOScale(Vector3.one * PlacementPulseScale, PlacementPulseHalfDuration)
        .SetEase(Ease.InOutSine)
        .SetLoops(-1, LoopType.Yoyo)
        .SetUpdate(true)
        .SetLink(_placementPreview.gameObject, LinkBehaviour.KillOnDestroy);
}

private void StopPlacementPulse()
{
    if (_placementPreviewTween != null)
    {
        _placementPreviewTween.Kill();
        _placementPreviewTween = null;
    }

    if (_placementPreview != null)
    {
        _placementPreview.transform.localScale = Vector3.one;
    }
}
```

Keep `ClearPlacementPreview()` as the first operation in `SetCards`. Remove the `_placementPreviewCard.HasValue` early return from normal `OnHover` so existing rail-card detail can coexist.

- [ ] **Step 6: Run focused GREEN verification**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests \
  -testResults /private/tmp/execution-rail-states-green.xml \
  -logFile /private/tmp/execution-rail-states-green.log \
  -quit
```

Expected: all `ExecutionRailInputTests` pass, no active tween warnings remain after teardown, and no DOTween safe-mode errors appear.

- [ ] **Step 7: Commit the rail state implementation**

```bash
git diff --check -- Assets/Unity/ExecutionRailView.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs
git add Assets/Unity/ExecutionRailView.cs \
  Assets/Tests/UnityEditMode/ExecutionRailInputTests.cs
git commit -m "feat(ui): animate selected execution preview"
```

---

### Task 4: Connect hand hover, silhouette click placement, and intervention-only targeting

**Files:**
- Modify: `Assets/Unity/HandCardHoverEffect.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Create: `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`
- Modify: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/BattleScreenController.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`
- Modify: `Assets/Unity/PLAYTEST.md`

**Interfaces:**
- Consumes: `TryPreviewExecutionPlacement`, targetless `PlayExecutionCard`, `ShowPlacementHover`, `ArmPlacementPreview`, `ClearPlacementPreview`.
- Produces: `void HandFanView.SetCards(IReadOnlyList<CardPresentation> cards, Action<int> onClick, Action<int, bool> onHover)`.
- Produces: `void HandCardHoverEffect.Initialize(Action<bool> onHover)`.
- Produces: `void CardSelectionController.ShowPlacementHover(int handIndex, CardPresentation card, int insertionIndex)`.
- Produces: `void CardSelectionController.HidePlacementHover(int handIndex)`.
- Keeps: `BeginTargetSelection` exclusively for intervention targeting.
- Removes: floating-card fields/methods, whole-rail confirmation, hand alpha ghosting, and serialized `_overlay`/`_cardPrefab` from `CardSelectionController`.

- [ ] **Step 1: Write the failing hand hover callback test**

Create `Assets/Tests/UnityEditMode/HandFanHoverTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class HandFanHoverTests
    {
        [Test]
        public void Hand_card_reports_its_index_on_hover_enter_and_exit()
        {
            var root = new GameObject("Hand", typeof(RectTransform));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<CardView>(
                    "Assets/Unity/Prefabs/CardView.prefab");
                Assert.IsNotNull(prefab);
                var hand = root.AddComponent<HandFanView>();
                hand.EditorBuild(prefab);
                var calls = new List<(int Index, bool Hovering)>();
                var cards = new[]
                {
                    new CardPresentation(
                        "execution", "execution", 3, 1, Side.Player,
                        string.Empty, null, false)
                };
                hand.SetCards(cards, _ => { },
                    (index, hovering) => calls.Add((index, hovering)));
                var hover = root.GetComponentInChildren<HandCardHoverEffect>();

                hover.OnPointerEnter(null);
                hover.OnPointerExit(null);

                CollectionAssert.AreEqual(
                    new[] { (0, true), (0, false) }, calls.ToArray());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
```

- [ ] **Step 2: Write failing controller tests for static hover and pulsing selection**

Add `using DG.Tweening;` to `CardSelectionControllerTests.cs`, then update `SetUp` so the rail has a runtime prefab and content:

```csharp
var rail = Child("Rail").AddComponent<ExecutionRailView>();
_overlay = (RectTransform)Child("Overlay", typeof(RectTransform)).transform;
var railPrefab = RailCardView.EditorCreate(
    (RectTransform)Child("RailPrefabRoot", typeof(RectTransform)).transform,
    new Vector2(96f, 132f));
rail.EditorBuild(null, railPrefab, _overlay);
rail.SetCards(Array.Empty<CardPresentation>(), _ => { });
```

Add `private RectTransform _overlay;` to the test fixture, create the targeting arrow under `_overlay`, remove the reflection assignment for the deleted controller `_overlay` field, and update `Target_click_does_not_create_center_emphasis_card` to count `_overlay.childCount` directly. This preserves the regression test without depending on removed serialization.

Add:

```csharp
[Test]
public void Placement_hover_is_static_until_hand_card_is_selected()
{
    var card = ExecutionPresentation();
    _controller.ShowPlacementHover(0, card, 0);
    var rail = Field<ExecutionRailView>(_controller, "_rail");
    var preview = Field<RailCardView>(rail, "_placementPreview");

    Assert.IsTrue(preview.gameObject.activeSelf);
    Assert.IsNull(Field<DG.Tweening.Tween>(rail, "_placementPreviewTween"));

    _controller.BeginPlacement(0, card, 0);

    Assert.IsTrue(_controller.SelectionActive);
    Assert.IsNotNull(Field<DG.Tweening.Tween>(rail, "_placementPreviewTween"));
    Assert.AreEqual(0, _appliedResults.Count);
}

[Test]
public void Armed_silhouette_click_dispatches_targetless_placement_once()
{
    var card = ExecutionPresentation();
    _controller.ShowPlacementHover(0, card, 0);
    _controller.BeginPlacement(0, card, 0);
    var rail = Field<ExecutionRailView>(_controller, "_rail");
    var preview = Field<RailCardView>(rail, "_placementPreview");

    Field<Button>(preview, "_button").onClick.Invoke();

    Assert.AreEqual(1, _appliedResults.Count);
    Assert.IsTrue(_appliedResults[0].IsComplete);
    Assert.AreEqual(0, _appliedResults[0].HandIndex);
    CollectionAssert.IsEmpty(_appliedResults[0].Targets);
    Assert.IsFalse(_controller.SelectionActive);
}
```

Add hover-exit, category-gate, and failed-placement cleanup tests:

```csharp
[Test]
public void Placement_hover_exit_hides_unselected_preview()
{
    _controller.ShowPlacementHover(0, ExecutionPresentation(), 0);
    var rail = Field<ExecutionRailView>(_controller, "_rail");
    var preview = Field<RailCardView>(rail, "_placementPreview");

    _controller.HidePlacementHover(0);

    Assert.IsFalse(preview.gameObject.activeSelf);
}

[Test]
public void Intervention_hover_never_creates_execution_preview()
{
    var execution = ExecutionPresentation();
    var intervention = new CardPresentation(
        "intervention", "intervention", 0, 1,
        FateWeaver.Core.Cards.Side.Player,
        string.Empty, null, false,
        category: FateWeaver.Core.Cards.CardCategory.Intervention);
    _controller.ShowPlacementHover(0, intervention, 0);
    var rail = Field<ExecutionRailView>(_controller, "_rail");

    Assert.IsNull(Field<RailCardView>(rail, "_placementPreview"));
}

[Test]
public void Rejected_targetless_placement_clears_selection_and_pulse()
{
    _controller.Initialize(
        result =>
        {
            _appliedResults.Add(result);
            return false;
        },
        _ => Array.Empty<SelectionTargetRef>(),
        () => { });
    _controller.ShowPlacementHover(0, ExecutionPresentation(), 0);
    _controller.BeginPlacement(0, ExecutionPresentation(), 0);
    var rail = Field<ExecutionRailView>(_controller, "_rail");
    var preview = Field<RailCardView>(rail, "_placementPreview");
    var tween = Field<DG.Tweening.Tween>(rail, "_placementPreviewTween");

    Field<Button>(preview, "_button").onClick.Invoke();

    Assert.AreEqual(1, _appliedResults.Count);
    Assert.IsFalse(_controller.SelectionActive);
    Assert.IsFalse(preview.gameObject.activeSelf);
    Assert.IsFalse(tween.IsActive());
}

[Test]
public void Existing_rail_card_click_does_not_confirm_armed_placement()
{
    _controller.ShowPlacementHover(0, ExecutionPresentation(), 0);
    _controller.BeginPlacement(0, ExecutionPresentation(), 0);

    _controller.OnTargetClicked(SelectionTargetRef.ExecutionCard(0));

    Assert.AreEqual(0, _appliedResults.Count);
    Assert.IsTrue(_controller.SelectionActive);
}
```

Add the test helper:

```csharp
private static CardPresentation ExecutionPresentation()
    => new CardPresentation(
        "execution", "execution", 3, 1,
        FateWeaver.Core.Cards.Side.Player,
        string.Empty, null, false);
```

Retain all existing single/multiple intervention target tests unchanged.

- [ ] **Step 3: Run changed Unity tests and verify RED**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter "FateWeaver.Tests.UnityEditMode.HandFanHoverTests;FateWeaver.Tests.UnityEditMode.CardSelectionControllerTests" \
  -testResults /private/tmp/execution-placement-integration-red.xml \
  -logFile /private/tmp/execution-placement-integration-red.log \
  -quit
```

Expected: compile failures for the three-argument `HandFanView.SetCards` and the new controller hover methods.

- [ ] **Step 4: Emit hover state from the hand card**

In `HandCardHoverEffect`, add:

```csharp
private System.Action<bool> _onHover;

public void Initialize(System.Action<bool> onHover)
{
    _onHover = onHover;
}
```

Update pointer handlers so callbacks occur only for a real accepted hover:

```csharp
public void OnPointerEnter(PointerEventData eventData)
{
    if (_suppressed || _held)
    {
        return;
    }

    _hovering = true;
    Enlarge();
    _onHover?.Invoke(true);
}

public void OnPointerExit(PointerEventData eventData)
{
    bool wasHovering = _hovering;
    _hovering = false;
    if (!_held)
    {
        Restore();
    }

    if (wasHovering)
    {
        _onHover?.Invoke(false);
    }
}
```

In `HandFanView`, replace `SetCards` with the three-callback signature and initialize each hover component:

```csharp
public void SetCards(
    IReadOnlyList<CardPresentation> cards,
    Action<int> onClick,
    Action<int, bool> onHover)
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
        hover.Initialize(hovering => onHover?.Invoke(captured, hovering));
        _hoverEffects.Add(hover);
        _groups.Add(view.gameObject.AddComponent<CanvasGroup>());
        _views.Add(view);
    }
}
```

- [ ] **Step 5: Remove floating placement and add controller preview ownership**

From `CardSelectionController`, remove only the placement-specific floating-card members:

```text
_overlay
_cardPrefab
FloatingScale
FloatingLift
_floatingCard
SpawnFloatingCard
DisableRaycasts
MoveToScreen
```

Keep `using UnityEngine.InputSystem`, `Update`, `MouseScreen`, and `SelectedCardScreen`: 조작 카드의 기존 타겟팅 화살표가 선택 카드에서 현재 마우스 위치까지 계속 추적해야 한다. Narrow `Update` to the explicit-target phases only by deleting just the `ConfirmPlacement`/floating-card branch.

Remove `OnRailAreaClicked` from `CardSelectionController`; `OnPlacementPreviewClicked` is the only targetless placement confirmation entry point. Remove `HandFanView.SetGhost` after deleting its controller call sites; `_groups` remains because intervention target selection still dims nonselected hand cards.

Add `using FateWeaver.Core.Cards;` to `CardSelectionController` for the execution-category guard.

Add:

```csharp
private int _hoverHandIndex = -1;

public void ShowPlacementHover(
    int handIndex, CardPresentation card, int insertionIndex)
{
    if (SelectionActive || card.Category != CardCategory.Execution)
    {
        return;
    }

    _hoverHandIndex = handIndex;
    _rail.ShowPlacementHover(card, insertionIndex);
}

public void HidePlacementHover(int handIndex)
{
    if (SelectionActive || _hoverHandIndex != handIndex)
    {
        return;
    }

    _hoverHandIndex = -1;
    _rail.ClearPlacementPreview();
}
```

Replace `BeginPlacement` with:

```csharp
public void BeginPlacement(
    int handIndex, CardPresentation card, int insertionIndex)
{
    EndSelectionVisuals();
    _machine.SelectCard(handIndex, SelectionTargetKind.None, 0);
    _visualHandIndex = handIndex;
    _hoverHandIndex = -1;
    _hand.SetHoverSuppressed(true);
    _hand.SetSelection(handIndex, CardView.SelectionKind.Primary);
    _rail.SetDropHint(true);
    _rail.ShowPlacementHover(card, insertionIndex);
    _rail.ArmPlacementPreview(OnPlacementPreviewClicked);
}

private void OnPlacementPreviewClicked()
{
    TryDispatch(_machine.ClickApplyArea());
}
```

In `EndSelectionVisuals`, keep target cleanup and add/reset:

```csharp
_hand.SetSelection(-1, CardView.SelectionKind.None);
_hoverHandIndex = -1;
```

Remove placement calls to `_hand.SetGhost` and all floating-card cleanup. Keep `_hand.SetHeld` only for intervention target selection.

In `TryDispatch`, a failed targetless placement must not remain as an armed ghost selection. Add this immediately after the failed `_tryApply` call and before target reloading:

```csharp
if (_machine.RequiredTargets <= 0)
{
    CancelSelection();
    return;
}
```

Explicit intervention rejection keeps the existing “remove stale picks and choose again” path.

- [ ] **Step 6: Make BattleScreen execution cards targetless and hover-driven**

Remove `_rail.SetRailClicked(_selection.OnRailAreaClicked)` from initialization; the preview button owns placement confirmation.

Add:

```csharp
private void OnHandHovered(int handIndex, bool hovering)
{
    if (_session == null || _selection.SelectionActive)
    {
        return;
    }

    if (!hovering)
    {
        _selection.HidePlacementHover(handIndex);
        return;
    }

    if (handIndex < 0 || handIndex >= _session.Hand.Count)
    {
        return;
    }

    var card = _session.Hand[handIndex];
    if (card.Def.Category != CardCategory.Execution
        || !_session.TryPreviewExecutionPlacement(handIndex, out var placement))
    {
        _selection.HidePlacementHover(handIndex);
        return;
    }

    _selection.ShowPlacementHover(
        handIndex,
        PresentationFor(card).WithExecutionOrder(placement.ExecutionOrder),
        placement.InsertionIndex);
}
```

In `RefreshAll`, bind both callbacks:

```csharp
_hand.SetCards(
    _session.Hand.Select(PresentationFor).ToList(),
    OnHandClicked,
    OnHandHovered);
```

Replace the execution branch of `OnHandClicked` with:

```csharp
if (def.Category == CardCategory.Execution)
{
    if (!_session.TryPreviewExecutionPlacement(handIndex, out var placement))
    {
        SetMessage("카드를 실행 순서에 배치할 수 없습니다.");
        return;
    }

    var presentation = PresentationFor(card)
        .WithExecutionOrder(placement.ExecutionOrder);
    _selection.BeginPlacement(
        handIndex, presentation, placement.InsertionIndex);
    SetMessage(name + " — 레일 실루엣을 클릭해 배치하세요.");
}
else
{
    int requiredTargets = CardTargetRules.RequiredRailTargets(def);
    if (def.Category != CardCategory.Intervention
        || requiredTargets < 1
        || requiredTargets > 2)
    {
        SetMessage("사용할 수 없는 조작 카드입니다.");
        return;
    }

    var targets = CurrentValidTargets(SelectionTargetKind.ExecutionCard);
    if (targets.Count < requiredTargets)
    {
        SetMessage("대상으로 삼을 카드가 실행 순서에 부족합니다.");
        return;
    }

    _selection.BeginTargetSelection(
        handIndex,
        SelectionTargetKind.ExecutionCard,
        requiredTargets,
        targets);
    SetMessage(name + " — 대상 " + requiredTargets + "개를 선택하세요.");
}
```

Keep the existing early energy check before this branch so an unaffordable hover can show its static position but click cannot arm it.

Replace the execution part of `TryApplySelection` with:

```csharp
if (def.Category == CardCategory.Execution)
{
    if (result.Targets.Count != 0)
    {
        SetMessage("실행 카드는 직접 대상을 선택하지 않습니다.");
        return false;
    }

    bool played = _session.PlayExecutionCard(result.HandIndex);
    SetMessage(played
        ? PlaytestKoreanText.CardName(def.Id, def.Name) + " 배치."
        : "운명력 또는 턴 상태로 카드를 배치할 수 없습니다.");
    return played;
}
```

- [ ] **Step 7: Remove obsolete editor serialization wiring**

In `BattleSceneBuilder`, remove only these assignments:

```csharp
selectionSo.FindProperty("_overlay").objectReferenceValue = overlay;
selectionSo.FindProperty("_cardPrefab").objectReferenceValue = cardPrefab;
```

Keep `_arrow`, `_hand`, `_rail`, `_dimLayer`, and `_confirmButton` assignments. Do not rebuild or save the battle scene as part of this source change.

- [ ] **Step 8: Update the manual playtest checklist**

Replace the first unified-target checklist item in `Assets/Unity/PLAYTEST.md` with:

```text
1. 모든 실행 카드는 호버만으로 상태 효과가 반영된 자동 위치에 정적인 알파 0.5 푸른 실루엣을 표시한다.
   호버 종료 시 미선택 실루엣은 사라진다. 손패 카드를 클릭하면 마우스 추적 카드 없이 손패 선택 테두리와
   고정 실루엣만 남고, 실루엣이 1.0↔1.06 크기로 부드럽게 반복된다. 실루엣을 클릭하면 실제 카드가
   같은 위치에 배치된다.
2. 실행 카드 배치 대기 중 기존 레일 카드에 호버하면 전체 카드 상세보기가 정상적으로 나타난다.
3. 운명력이 부족한 실행 카드는 호버 위치는 보이지만 손패 클릭으로 배치 대기 상태에 들어가지 않는다.
4. 조작 카드의 단일·다중 레일 대상 선택, 재선택 취소와 확인 버튼 동작은 기존과 같다.
5. 빈 영역 취소는 카드와 운명력을 소비하지 않고 실행 실루엣 또는 조작 선택 표현을 지운다.
```

Remove statements that execution cards follow the mouse, require direct ally selection, or place by clicking the whole rail. Renumber the remaining checklist without changing unrelated requirements.

- [ ] **Step 9: Run focused and full verification**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj \
  -p:TargetFramework=net5.0 --no-restore
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testFilter "FateWeaver.Tests.UnityEditMode.HandFanHoverTests;FateWeaver.Tests.UnityEditMode.CardSelectionControllerTests;FateWeaver.Tests.UnityEditMode.ExecutionRailInputTests" \
  -testResults /private/tmp/execution-placement-focused-green.xml \
  -logFile /private/tmp/execution-placement-focused-green.log \
  -quit
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/Git/rogue-deck/.worktrees/card-selection-integration \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/execution-placement-full-editmode.xml \
  -logFile /private/tmp/execution-placement-full-editmode.log \
  -quit
```

Expected: headless, focused Unity, and full EditMode suites have zero failures; Unity logs have no leaked tween, missing DOTween assembly, or target-null errors.

Run source-boundary searches:

```bash
rg -n "PlayExecutionCard\([^)]*,|RequiresExplicitAllyTarget\(def\)|SpawnFloatingCard|_floatingCard|MoveToScreen\(|OnRailAreaClicked|SetGhost\(" \
  Assets/Unity Assets/Core/Simulation -g '*.cs'
rg -n "BeginTargetSelection\(" Assets/Unity/BattleScreenController.cs
```

Expected: the first search has no production call sites for removed execution-placement behavior; the second search has one call in the intervention branch. `MouseScreen()` and the target-selection branch in `Update()` remain solely for the manipulation-card targeting arrow.

- [ ] **Step 10: Commit integration and documentation**

```bash
git diff --check -- Assets/Unity/HandCardHoverEffect.cs \
  Assets/Unity/HandFanView.cs \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs \
  Assets/Unity/CardSelectionController.cs \
  Assets/Unity/BattleScreenController.cs \
  Assets/Unity/Editor/BattleSceneBuilder.cs \
  Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs \
  Assets/Unity/PLAYTEST.md
git add Assets/Unity/HandCardHoverEffect.cs \
  Assets/Unity/HandFanView.cs \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs \
  Assets/Tests/UnityEditMode/HandFanHoverTests.cs.meta \
  Assets/Unity/CardSelectionController.cs \
  Assets/Unity/BattleScreenController.cs \
  Assets/Unity/Editor/BattleSceneBuilder.cs \
  Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs \
  Assets/Unity/PLAYTEST.md
git commit -m "feat(input): place execution cards through rail preview"
```

- [ ] **Step 11: Confirm the worktree boundary**

```bash
git status --short --branch
git log --oneline -8
```

Expected: four implementation commits follow the design and plan commits. Only pre-existing user scene, prefab, font, and targeting-arrow changes remain unstaged; no scene or prefab regeneration was performed.
