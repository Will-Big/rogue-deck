# 파티 전투 카드 선택 입력 통합 Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `feat/card-selection-input`의 호버·명시적 두 번째 입력·레일 대상 선택 UX를 현재 파티 전투 `master` 구조에 통합한다.

**Architecture:** 현재 `DeckCombatSession`의 `OwnedCard`, 파티 소유자, 직접 아군 대상, 취소 모델은 그대로 유지한다. 순수 C# `CardSelectionMachine`은 실행 카드 배치와 레일 개입 선택만 판정하고, Unity `CardSelectionController`가 해당 시각 상태를 소유한다. 직접 아군 대상은 기존 `BattleScreenController`의 `AllyTargeting`이 계속 담당하되 같은 빈 곳 취소 규칙과 손패 Held 표현을 사용한다.

**Tech Stack:** Unity 6 uGUI/TMP/Input System, 순수 C# 9, NUnit 헤드리스 테스트.

## Global Constraints

- `Assets/Core/**`는 `UnityEngine`을 참조하지 않는다.
- 무작위와 전투 규칙은 기존 결정론 경로를 유지한다.
- 새 효과나 콘텐츠를 추가하지 않는다.
- Unity 참조는 `[SerializeField] private`으로 배선하고 런타임 문자열 탐색을 사용하지 않는다.
- 런타임 시각 복제는 기존 `CardView` 프리팹만 사용한다. 화살표는 씬 빌더가 미리 만들고 직렬화한다.
- 기존 파티의 `OwnedCard`, `OwnerId`, 직접 아군 대상, 개인 상태, 대형, 카드 취소 의미를 보존한다.
- 헤드리스 명령은 `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`다.
- 사용자 작업인 원본 checkout의 `Assets/Unity/Resources/Fonts/KoreanTMP.asset`과 `.DS_Store`를 건드리지 않는다.

---

### Task 1: 레일 선택 규칙과 상태 머신 이식

**Files:**
- Create: `Assets/Core/Simulation/Presentation/CardTargetRules.cs`
- Create: `Assets/Core/Simulation/Presentation/CardSelectionMachine.cs`
- Create: `Assets/Core/Tests/EditMode/CardTargetRulesTests.cs`
- Create: `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`

**Interfaces:**
- Consumes: `CardDefinition`, `InterventionActionKeys.SwapExecutionOrder`.
- Produces: `CardTargetRules.RequiredRailTargets(CardDefinition)`, `CardSelectionMachine`, `SelectionPhase`, `SelectionCommand`.
- `RequiredRailTargets`는 실행 카드와 null에 0, 일반 개입에 1, 교환 개입에 2를 반환한다. 직접 아군 대상 여부는 기존 `PartyTargetRules`가 별도로 판정한다.
- `SelectionCommand`는 실행 카드 배치 또는 레일 개입 적용 중 하나만 표현하며 파티 대상 ID를 표현하지 않는다.

- [ ] **Step 1: 실패 테스트 작성**

`CardTargetRulesTests`에 `Execution_card_needs_no_rail_targets`, `Single_target_intervention_needs_one_rail_target`, `Swap_intervention_needs_two_rail_targets`, `Null_definition_needs_no_rail_targets`를 작성한다.

`CardSelectionMachineTests`에 다음 동작을 작성한다.

```csharp
machine.SelectCard(handIndex: 2, requiredTargets: 0);
Assert.AreEqual(SelectionPhase.ConfirmPlacement, machine.Phase);
Assert.IsTrue(machine.ClickApplyArea().PlayExecution);

machine.SelectCard(handIndex: 1, requiredTargets: 1);
Assert.IsTrue(machine.ClickTarget(3).PlayIntervention);

machine.SelectCard(handIndex: 4, requiredTargets: 2);
machine.ClickTarget(1);
machine.ClickTarget(3);
Assert.AreEqual(SelectionPhase.ReadyToConfirm, machine.Phase);
var command = machine.Confirm();
Assert.AreEqual(1, command.TargetA);
Assert.AreEqual(3, command.TargetB);
```

중복 대상 무시, 준비 전 확인 무시, 취소 초기화, 새 카드 선택 시 기존 픽 초기화도 각각 독립 테스트로 둔다.

- [ ] **Step 2: RED 확인**

Run:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "CardTargetRulesTests|CardSelectionMachineTests"
```

Expected: 새 타입이 없어 컴파일 실패.

- [ ] **Step 3: 최소 구현**

`CardTargetRules.RequiredRailTargets`는 카드 분류와 개입 키만 읽는다. `CardSelectionMachine`은 내부 `List<int>`를 읽기 전용으로 노출하고 `Idle`, `ConfirmPlacement`, `PickSingleTarget`, `PickMultipleTargets`, `ReadyToConfirm` 전이만 구현한다. 세션과 Unity 타입은 참조하지 않는다.

- [ ] **Step 4: GREEN 확인**

Run: Step 2 명령.

Expected: 신규 테스트 14개 통과.

- [ ] **Step 5: 전체 회귀**

Run: 전역 헤드리스 명령.

Expected: 기존 257개와 신규 테스트 모두 통과.

---

### Task 2: 덱 뷰의 변경 불가능성 봉인

**Files:**
- Modify: `Assets/Core/Combat/Deck.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Modify: `Assets/Core/Tests/EditMode/DeckPileVisibilityTests.cs`
- Modify: `Assets/Core/Simulation/Presentation/HandFanLayout.cs`

**Interfaces:**
- Consumes: 현재 `OwnedCard` 기반 덱과 세션.
- Produces: 런타임 타입도 `List<OwnedCard>`로 다운캐스트할 수 없는 `DrawPile`, `DiscardPile`, `AllDeckCards`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
[Test]
public void Piles_are_not_downcastable_to_mutable_lists()
{
    var session = NewSession();
    Assert.IsNotInstanceOf<List<OwnedCard>>(session.DrawPile);
    Assert.IsNotInstanceOf<List<OwnedCard>>(session.DiscardPile);
    Assert.IsNotInstanceOf<List<OwnedCard>>(session.AllDeckCards);
}
```

- [ ] **Step 2: RED 확인**

Run: 헤드리스 명령에 `--filter DeckPileVisibilityTests` 추가.

Expected: 세 컬렉션 중 적어도 하나가 `List<OwnedCard>`라 실패.

- [ ] **Step 3: 최소 구현**

`Deck`은 생성자에서 `_draw.AsReadOnly()`, `_discard.AsReadOnly()`를 한 번 저장한다. `DeckCombatSession`은 모든 생성자 경로가 완성한 카드 목록을 `AsReadOnly()`로 보관한다. 내부 변경은 기존 리스트를 통해서만 수행한다.

- [ ] **Step 4: GREEN 및 전체 회귀**

Run: 대상 테스트 후 전역 헤드리스 명령.

Expected: 전체 통과. `HandFanLayout`은 동작 변경 없이 좌표/각도 XML 문서만 명확히 한다.

---

### Task 3: 손패 호버와 레일 선택 표현

**Files:**
- Create: `Assets/Unity/HandCardHoverEffect.cs`
- Create: `Assets/Unity/TargetingArrowView.cs`
- Modify: `Assets/Unity/HandFanView.cs`
- Modify: `Assets/Unity/ExecutionRailView.cs`
- Modify: `Assets/Unity/FateWeaver.Unity.asmdef`

**Interfaces:**
- `HandFanView.SetHeld(int, bool)`, `SetGhost(int, bool)`, 기존 `SetSelection`, `SetInputEnabled`를 모두 유지한다.
- `ExecutionRailView.SetRailClicked(Action)`, `SetDropHint(bool)`, `SetPickedTargets(IReadOnlyList<int>)`를 추가하고 기존 prefab 기반 카드 생성과 입력 잠금을 유지한다.
- `TargetingArrowView.EditorBuild(RectTransform overlay)`, `Show(Vector2)`, `Track(Vector2)`, `Hide()`를 제공한다. 런타임 `Create` 팩토리는 두지 않는다.

- [ ] **Step 1: HandFanView 통합**

카드 생성 시 `HandCardHoverEffect`와 `CanvasGroup`을 붙여 fan pose를 캡처한다. 호버는 직립, 1.35배 확대, 46px 상승, 최상위 sibling으로 이동하고 종료 시 원복한다. `SetHeld`는 아군/단일 레일 대상 선택 동안 확대를 고정하며 `SetGhost`는 실행 카드 원본 alpha를 0.35로 바꾼다.

- [ ] **Step 2: ExecutionRailView 통합**

기존 `_scrollRect`, `_cardPrefab`, `SetSelection`, `SetInputEnabled`를 보존한다. viewport 배경 `Image`와 `Button`을 직렬화하고, 배치 대기 중 배경을 약한 호박색으로 바꾸며 빈 레일 클릭을 콜백으로 전달한다. 선택된 여러 레일 카드는 Secondary 외곽선을 사용한다.

- [ ] **Step 3: 화살표와 Input System**

화살표는 overlay의 미리 생성된 자식 이미지 두 개(shaft/head)를 참조한다. `CardSelectionController`가 `Mouse.current`를 사용하므로 asmdef에 `Unity.InputSystem`을 추가한다.

- [ ] **Step 4: 정적 검증**

Run:

```bash
rg -n "GameObject.Find|FindObjectOfType|Resources.Load\\(\"" Assets/Unity/HandCardHoverEffect.cs Assets/Unity/TargetingArrowView.cs Assets/Unity/HandFanView.cs Assets/Unity/ExecutionRailView.cs
```

Expected: 결과 없음.

---

### Task 4: 선택 컨트롤러와 파티 전투 입력 통합

**Files:**
- Create: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/BattleScreenController.cs`

**Interfaces:**
- `CardSelectionController.Initialize(Action<SelectionCommand>)`
- `BeginSelection(int handIndex, int requiredTargets, CardPresentation card)`
- `OnZoneClicked(int zoneIndex, CardPresentation card)`, `OnRailAreaClicked()`, `CancelSelection()`
- `BattleScreenController`는 `AllyTargeting`만 자체 소유한다. 실행/개입 레일 선택은 `_selection.SelectionActive`로 판정한다.

- [ ] **Step 1: CardSelectionController 작성**

`[SerializeField] private` 참조로 손패, 레일, dim, 확인 버튼, overlay, CardView prefab, 미리 생성된 화살표를 받는다. 실행 카드 선택은 ghost 카드와 prefab 복제본을 커서에 표시한다. 단일 개입은 원본 Held와 화살표를 표시한다. 교환은 dim, 중복 없는 대상 외곽선, 중앙 강조, 요구 수 충족 후 확인 버튼을 사용한다. 취소/커밋은 모든 coroutine과 시각 상태를 정리한다.

- [ ] **Step 2: OnHandClicked 재배선**

순서는 다음과 같다.

1. 세션, 인덱스, 턴 상태, 현재 입력 상태 검증.
2. 운명력 부족이면 선택에 진입하지 않음.
3. 실행 카드이면서 `PartyTargetRules.RequiresExplicitAllyTarget`이면 기존 `AllyTargeting` 진입, `SetHeld` 적용.
4. 나머지는 `CardTargetRules.RequiredRailTargets` 계산 후 레일 카드 수 검증.
5. `_selection.BeginSelection` 호출. 첫 클릭에서는 세션 API를 호출하지 않음.

- [ ] **Step 3: 명령 적용과 취소 통합**

실행 명령은 현재 `OwnedCard`의 이름을 보존한 뒤 `PlayExecutionCard(handIndex)`를 호출한다. 개입 명령은 `PlayInterventionCard(handIndex, targetA, targetB)`를 호출한다. 빈 곳과 dim 클릭은 카드 선택 또는 아군 선택을 취소하고 비용/손패를 유지한다. 아군 클릭은 기존 `PlayExecutionCard(handIndex, memberId)` 검증을 그대로 사용한다.

- [ ] **Step 4: 파티 회귀 보존 정독**

다음이 남아 있는지 확인한다.

```bash
rg -n "CharacterAsset\[\]|OwnerPresentation|SetStatuses|SetTargetable|PartyTargetRules|PlayExecutionCard\([^,]+, memberId\)" Assets/Unity/BattleScreenController.cs
```

Expected: 파티 에셋, 소유자 칩, 개인 상태, 살아 있는 아군 대상 경로가 모두 검색된다.

---

### Task 5: 씬 빌더, 문서, 최종 검증

**Files:**
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Modify: `Assets/Unity/PLAYTEST.md`
- Generate through Unity: new `.meta` files and `Assets/Scenes/FateWeaverBattle.unity`

**Interfaces:**
- 씬 빌더는 파티/카드/유닛/레일 prefab을 `NewScene` 이후 다시 로드하는 현재 순서를 유지한다.
- 씬에는 full-screen 빈 곳 catcher, dim click catcher, 우하단 확인 버튼, `CardSelectionController`, 미리 생성된 `TargetingArrowView`가 직렬화된다.

- [ ] **Step 1: 빌더 재배선**

기존 좌측 취소 버튼과 `_cancelButton` 배선을 제거한다. 투명 빈 곳 catcher는 배경 위, 실제 UI 아래에 둔다. dim 이미지는 클릭 가능한 취소 catcher로 사용한다. Z-order는 dim → rail → confirm → message → overlay 순서다. 파티 CharacterAsset과 UnitView/RailCardView prefab 배선을 보존한다. InputActions가 없으면 명확한 경고를 출력한다.

- [ ] **Step 2: PLAYTEST 문서 갱신**

전투 화면 제목을 `시각 개편 1–2단계`로 바꾸고 카드 입력을 다음 의미로 기록한다: 호버는 보기, 첫 클릭은 선택, 실행 카드는 레일 재클릭, 단일 개입은 레일 대상 클릭, 교환은 두 대상과 확인, 직접 아군 카드는 살아 있는 유닛 클릭, 빈 곳/dim 클릭은 무비용 취소.

- [ ] **Step 3: 헤드리스 최종 회귀**

Run: 전역 헤드리스 명령.

Expected: Failed 0.

- [ ] **Step 4: Unity 배치 검증**

Unity에서 `Fate Weaver ▸ Build Battle Scene`을 실행하고 컴파일 에러, Missing Script, 누락 SerializeField가 없는지 확인한다. 가능하면 batchmode로 먼저 컴파일/씬 생성을 실행한다.

- [ ] **Step 5: Play 수동 체크리스트**

1. 손패 호버 확대와 원복.
2. 실행 카드 첫 클릭은 소비하지 않고 레일 클릭 때만 배치.
3. 직접 아군 대상은 살아 있는 파티원 클릭 때만 배치.
4. 단일 개입은 화살표와 레일 대상 클릭.
5. 교환은 중복 없는 두 대상, 확인 버튼, 중앙 강조.
6. 빈 곳/dim 취소 시 손패와 운명력 유지.
7. 파티 HP, 개인 상태, 대형, 소유자 칩 유지.
8. 콘솔 오류 0.

- [ ] **Step 6: 최종 상태 확인**

`git status --short`, `git diff --check`, 신규 `.meta` 추적 여부를 확인한다. 사용자 폰트 에셋은 통합 브랜치 diff에 포함하지 않는다.
