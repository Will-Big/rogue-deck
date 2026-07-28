# P0-C 대상 선택 메타데이터 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 개입 핸들러가 대상 요구를 선언하고, 세션이 질의를 제공하고, UI는 키 해석 없이 집행하며, 코어가 최종 유효성을 판정한다. 도달 불가 유닛 대상 UI는 삭제한다.

**Architecture:** 스펙 [`2026-07-28-p0c-targeting-metadata-design.md`](../specs/2026-07-28-p0c-targeting-metadata-design.md)의 선언→질의→집행→검증 4단 경로. 코어에 `TargetingRequirement` 값 타입을 추가하고 `IInterventionActionHandler`에 정식 멤버 `Targeting`을 더한다. `CardTargetRules`와 키 비교 2곳, 유닛 대상 표면 전체를 삭제한다.

**Tech Stack:** 순수 C# (Unity 6의 C# 9 제약 — record struct 금지), NUnit, Unity 6000.5.2f1 batchmode EditMode.

## Global Constraints

- `FateWeaver.Core`는 UnityEngine 참조 금지 (asmdef `noEngineReferences`).
- 튜닝 수치 하드코딩 금지 — 이 작업은 수치를 추가하지 않는다.
- 헤드리스 테스트: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0` (로컬 SDK가 .NET 5뿐이라 `-p:TargetFramework=net5.0` 필수).
- Unity EditMode 배치: `/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /private/tmp/p0c-editmode-results.xml -logFile /private/tmp/p0c-editmode.log` (워크트리 경로에서 실행. 에디터가 무한 로딩이면 Unity Licensing Client 좀비 — `pkill -f "Unity Licensing Client"` 후 재시도, 로그의 505 거부는 정상).
- `.cs` 삭제 시 대응 `.meta`도 같은 커밋에서 삭제.
- 커밋 접두사: 코어 `feat:`, 삭제 `refactor:`, 테스트만이면 `test:`.
- 각 태스크 끝에 헤드리스 스위트 전체 실행. Unity EditMode는 Task 5·7 끝에서 실행(느리므로).

---

### Task 1: `TargetingRequirement` 타입과 핸들러 선언

**Files:**
- Create: `Assets/Core/Intervention/TargetingRequirement.cs` (+ Unity가 `.meta` 자동 생성 — batchmode 실행 후 생성물 확인해 함께 커밋)
- Modify: `Assets/Core/Intervention/IInterventionActionHandler.cs`
- Modify: `Assets/Core/Intervention/ChangeExecutionOrderHandler.cs`
- Modify: `Assets/Core/Intervention/SwapExecutionOrderHandler.cs`
- Modify: `Assets/Core/Intervention/LockHandler.cs`
- Test: `Assets/Core/Tests/EditMode/TargetingRequirementTests.cs` (신규)

**Interfaces:**
- Produces: `enum TargetKind { None, RailCard }`; `struct TargetingRequirement { TargetKind Kind; int Count; bool AllowDuplicates; static TargetingRequirement None; static TargetingRequirement RailCards(int count); }`; `IInterventionActionHandler.Targeting` (Task 2·3이 소비).

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/Core/Tests/EditMode/TargetingRequirementTests.cs`:

```csharp
using System;
using NUnit.Framework;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class TargetingRequirementTests
    {
        [Test]
        public void Change_and_lock_declare_one_rail_target()
        {
            Assert.AreEqual(TargetKind.RailCard, new ChangeExecutionOrderHandler().Targeting.Kind);
            Assert.AreEqual(1, new ChangeExecutionOrderHandler().Targeting.Count);
            Assert.AreEqual(TargetKind.RailCard, new LockHandler().Targeting.Kind);
            Assert.AreEqual(1, new LockHandler().Targeting.Count);
        }

        [Test]
        public void Swap_declares_two_distinct_rail_targets()
        {
            var targeting = new SwapExecutionOrderHandler().Targeting;
            Assert.AreEqual(TargetKind.RailCard, targeting.Kind);
            Assert.AreEqual(2, targeting.Count);
            Assert.IsFalse(targeting.AllowDuplicates);
        }

        [Test]
        public void None_requirement_is_the_default()
        {
            Assert.AreEqual(TargetKind.None, TargetingRequirement.None.Kind);
            Assert.AreEqual(0, TargetingRequirement.None.Count);
        }

        [Test]
        public void Rail_requirement_rejects_nonpositive_count()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TargetingRequirement.RailCards(0));
        }

        // 확장 증명: 새 2대상 핸들러 = 클래스 1개 + 키 등록. 선언 누락은 컴파일 에러.
        [Test]
        public void A_new_handler_exposes_its_requirement_through_the_registry()
        {
            var registry = new InterventionActionRegistry();
            registry.Register(new FakeDoubleLockHandler());

            var resolved = registry.Resolve(FakeDoubleLockHandler.FakeKey).Targeting;

            Assert.AreEqual(TargetKind.RailCard, resolved.Kind);
            Assert.AreEqual(2, resolved.Count);
        }

        private sealed class FakeDoubleLockHandler : IInterventionActionHandler
        {
            public static readonly InterventionActionKey FakeKey =
                new InterventionActionKey("test_double_lock");

            public InterventionActionKey Key => FakeKey;
            public TargetingRequirement Targeting => TargetingRequirement.RailCards(2);
            public bool CanApply(InterventionPlayContext ctx) => false;
            public void Apply(InterventionPlayContext ctx) { }
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: 컴파일 실패 — `TargetKind`, `Targeting` 미정의.

- [ ] **Step 3: 최소 구현**

`Assets/Core/Intervention/TargetingRequirement.cs` (신규):

```csharp
using System;

namespace FateWeaver.Core.Intervention
{
    /// <summary>What kind of thing the player must pick before a card can be played.
    /// New target kinds (ally, enemy, hand card...) are added here when the intervention
    /// card design lands — see the 2026-07-28 P0-C targeting spec.</summary>
    public enum TargetKind { None, RailCard }

    /// <summary>A card's target-selection demand, declared by its intervention handler.
    /// The UI drives selection from this; the core validates the final pick against it.</summary>
    public readonly struct TargetingRequirement
    {
        public TargetKind Kind { get; }
        public int Count { get; }
        public bool AllowDuplicates { get; }

        private TargetingRequirement(TargetKind kind, int count, bool allowDuplicates)
        {
            Kind = kind;
            Count = count;
            AllowDuplicates = allowDuplicates;
        }

        public static readonly TargetingRequirement None = default;

        public static TargetingRequirement RailCards(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count),
                    "A rail-card requirement needs at least one target.");
            }

            return new TargetingRequirement(TargetKind.RailCard, count, allowDuplicates: false);
        }
    }
}
```

`IInterventionActionHandler.cs` — 인터페이스에 멤버 추가 (정식 멤버: 새 핸들러의 선언 누락을 컴파일 에러로 만든다):

```csharp
    public interface IInterventionActionHandler
    {
        InterventionActionKey Key { get; }

        /// <summary>Target demand the UI must satisfy before play. Single source of truth —
        /// mirrors what CanApply checks (e.g. swap requires Target and SecondaryTarget).</summary>
        TargetingRequirement Targeting { get; }

        bool CanApply(InterventionPlayContext ctx);
        void Apply(InterventionPlayContext ctx);
    }
```

세 핸들러에 각각 한 줄 추가 (`Key` 프로퍼티 바로 아래):

```csharp
// ChangeExecutionOrderHandler.cs, LockHandler.cs
public TargetingRequirement Targeting => TargetingRequirement.RailCards(1);

// SwapExecutionOrderHandler.cs
public TargetingRequirement Targeting => TargetingRequirement.RailCards(2);
```

- [ ] **Step 4: 통과 확인 (전체 스위트)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: PASS (기존 328 + 신규 5).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Intervention/ Assets/Core/Tests/EditMode/TargetingRequirementTests.cs
git commit -m "feat(core): 개입 핸들러가 TargetingRequirement를 정식 멤버로 선언"
```

---

### Task 2: `DeckCombatSession.DescribeTargeting` 질의

**Files:**
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs` (생성자 ~142행, `PlayInterventionCard` 위 ~258행)
- Test: `Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: Task 1의 `TargetingRequirement`, `IInterventionActionHandler.Targeting`, 기존 `CombatRegistries.InterventionActions()` / `InterventionActionRegistry.Resolve(key)`.
- Produces: `public TargetingRequirement DescribeTargeting(int handIndex)` — Task 3·4가 소비.

- [ ] **Step 1: 실패하는 테스트 작성**

`DeckCombatSessionTests.cs`에 추가 (기존 `NewSession`/`HandIndex` 헬퍼 재사용, `using FateWeaver.Core.Intervention;` 추가):

```csharp
        [Test]
        public void Describe_targeting_answers_none_for_execution_cards()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            Assert.AreEqual(TargetKind.None,
                session.DescribeTargeting(HandIndex(session, "slash")).Kind);
        }

        [Test]
        public void Describe_targeting_answers_one_rail_card_for_pull_forward()
        {
            var session = NewSession(new[] { StarterDeck.PullForward() }, Goblin(4, 3));

            var req = session.DescribeTargeting(HandIndex(session, "pull_forward"));

            Assert.AreEqual(TargetKind.RailCard, req.Kind);
            Assert.AreEqual(1, req.Count);
        }

        [Test]
        public void Describe_targeting_answers_two_rail_cards_for_swap()
        {
            var session = NewSession(new[] { StarterDeck.SwapPositions() }, Goblin(4, 3));

            var req = session.DescribeTargeting(HandIndex(session, "swap_positions"));

            Assert.AreEqual(TargetKind.RailCard, req.Kind);
            Assert.AreEqual(2, req.Count);
            Assert.IsFalse(req.AllowDuplicates);
        }

        [Test]
        public void Describe_targeting_answers_none_for_out_of_range_indexes()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            Assert.AreEqual(TargetKind.None, session.DescribeTargeting(-1).Kind);
            Assert.AreEqual(TargetKind.None, session.DescribeTargeting(99).Kind);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: 컴파일 실패 — `DescribeTargeting` 미정의.

- [ ] **Step 3: 최소 구현**

생성자(142행 근처)에서 레지스트리를 필드로 보관 — 리졸버와 같은 인스턴스를 공유하도록 변경:

```csharp
// 필드 추가 (기존 _interventionResolver 선언 옆)
private readonly InterventionActionRegistry _interventionActions;

// 생성자 내 기존 줄 교체:
//   _interventionResolver = new InterventionPlayResolver(CombatRegistries.InterventionActions());
_interventionActions = CombatRegistries.InterventionActions();
_interventionResolver = new InterventionPlayResolver(_interventionActions);
```

`PlayInterventionCard` 바로 위에 질의 메서드 추가:

```csharp
        /// <summary>Answers what the player must pick before playing this hand card.
        /// Execution cards never require explicit targets (targets are authored via
        /// StatusApplyTarget / TargetSelector and resolved by the core).</summary>
        public TargetingRequirement DescribeTargeting(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return TargetingRequirement.None;
            }

            var def = _deck.Hand[handIndex].Def;
            if (def.Category != CardCategory.Intervention || def.InterventionAction == null)
            {
                return TargetingRequirement.None;
            }

            return _interventionActions.Resolve(def.InterventionAction.Key).Targeting;
        }
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs
git commit -m "feat(core): DeckCombatSession.DescribeTargeting 질의 추가"
```

---

### Task 3: 코어 중복 대상 검증 (P5 버그 수정)

**Files:**
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs` (`PlayInterventionCard` ~280행)
- Test: `Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs`

**Interfaces:**
- Consumes: Task 1·2. 기존 `ZoneIndex` 헬퍼.
- Produces: 없음 (동작 변경만 — 중복 대상 스왑이 자원 소모 없이 거부됨).

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
        [Test]
        public void Swap_with_the_same_target_twice_is_rejected_without_spending_anything()
        {
            var session = NewSession(
                new[] { StarterDeck.QuickCut(), StarterDeck.SwapPositions() }, Goblin(5, 3));
            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            int energyBefore = session.FateEnergy;
            int handBefore = session.Hand.Count;
            var orderBefore = session.CurrentOrder.Select(c => c.InstanceId).ToArray();
            int quickIndex = ZoneIndex(session, "quick_cut");

            bool played = session.PlayInterventionCard(
                HandIndex(session, "swap_positions"), quickIndex, quickIndex);

            Assert.IsFalse(played);
            Assert.AreEqual(energyBefore, session.FateEnergy);
            Assert.AreEqual(handBefore, session.Hand.Count);
            CollectionAssert.AreEqual(orderBefore,
                session.CurrentOrder.Select(c => c.InstanceId).ToArray());
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter Swap_with_the_same 2>&1 | tail -5`
Expected: FAIL — 현재 코어는 동일 인덱스를 통과시키고 운명력을 차감함 (`played == true`).

- [ ] **Step 3: 최소 구현**

`PlayInterventionCard`에서 secondary 인덱스 검증 블록에 중복 검사 추가:

```csharp
            var targeting = _interventionActions.Resolve(def.InterventionAction.Key).Targeting;
            ExecutionCardInstance secondary = null;
            if (secondaryZoneIndex >= 0)
            {
                if (secondaryZoneIndex >= order.Count)
                {
                    return false;
                }

                if (!targeting.AllowDuplicates && secondaryZoneIndex == targetZoneIndex)
                {
                    return false;
                }

                secondary = order[secondaryZoneIndex];
            }
```

- [ ] **Step 4: 통과 확인 (전체 스위트)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs
git commit -m "fix(core): 중복 대상 자리 교환을 자원 소모 없이 거부"
```

---

### Task 4: Unity 컨트롤러를 질의로 전환, `CardTargetRules` 삭제

**Files:**
- Modify: `Assets/Unity/BattleScreenController.cs` (`OnHandClicked` ~176-196행, `TryApplySelection` ~284-297행)
- Modify: `Assets/Unity/DeckPlaytestController.cs` (~131-132행)
- Delete: `Assets/Core/Simulation/Presentation/CardTargetRules.cs` + `.meta`
- Delete: `Assets/Core/Tests/EditMode/CardTargetRulesTests.cs` + `.meta` (Task 2의 `DescribeTargeting` 테스트가 같은 시나리오를 세션 경유로 대체)

**Interfaces:**
- Consumes: `DescribeTargeting(int)`, `TargetKind`.
- Produces: Unity 레이어의 `InterventionActionKeys` 비교 0곳.

- [ ] **Step 1: `BattleScreenController.OnHandClicked` 개입 분기 교체**

기존 (176-196행):

```csharp
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
                    handIndex, SelectionTargetKind.ExecutionCard, requiredTargets, targets);
                SetMessage(name + " — 대상 " + requiredTargets + "개를 선택하세요.");
            }
```

이후 (`requiredTargets > 2` 상한 가드 삭제 — 머신은 개수에 일반화되어 있음):

```csharp
            else
            {
                var req = _session.DescribeTargeting(handIndex);
                if (req.Kind != TargetKind.RailCard)
                {
                    SetMessage("사용할 수 없는 조작 카드입니다.");
                    return;
                }

                var targets = CurrentValidTargets(SelectionTargetKind.ExecutionCard);
                if (targets.Count < req.Count)
                {
                    SetMessage("대상으로 삼을 카드가 실행 순서에 부족합니다.");
                    return;
                }

                _selection.BeginTargetSelection(
                    handIndex, SelectionTargetKind.ExecutionCard, req.Count, targets);
                SetMessage(name + " — 대상 " + req.Count + "개를 선택하세요.");
            }
```

`using FateWeaver.Core.Intervention;` 추가, `FateWeaver.Simulation.Presentation`의 `CardTargetRules` 참조 제거 확인.

- [ ] **Step 2: `TryApplySelection` 교체**

기존 (284-297행)의 `CardTargetRules.RequiredRailTargets(def)` 기반 검사를 질의 기반으로:

```csharp
            var req = _session.DescribeTargeting(result.HandIndex);
            if (req.Kind != TargetKind.RailCard
                || result.Targets.Count != req.Count
                || result.Targets.Any(target => target.Kind != SelectionTargetKind.ExecutionCard))
            {
                SetMessage("대상/운명력/잠금 규칙으로 적용할 수 없습니다.");
                return false;
            }

            int secondaryTarget = req.Count == 2 ? result.Targets[1].Index : -1;
```

- [ ] **Step 3: `DeckPlaytestController` 키 비교 교체**

기존 132행:

```csharp
            var def = _session.Hand[_armedInterventionHandIndex].Def;
            var needsTwo = def.InterventionAction != null && def.InterventionAction.Key == InterventionActionKeys.SwapExecutionOrder;
```

이후 (`def` 지역 변수와 `using FateWeaver.Core.Intervention;`의 `InterventionActionKeys` 사용이 사라지면 using 정리):

```csharp
            var needsTwo = _session.DescribeTargeting(_armedInterventionHandIndex).Count == 2;
```

- [ ] **Step 4: 파일 삭제**

```bash
git rm Assets/Core/Simulation/Presentation/CardTargetRules.cs Assets/Core/Simulation/Presentation/CardTargetRules.cs.meta
git rm Assets/Core/Tests/EditMode/CardTargetRulesTests.cs Assets/Core/Tests/EditMode/CardTargetRulesTests.cs.meta
```

- [ ] **Step 5: 검증**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: PASS.

Run: `grep -rn "EffectKeys\.\|InterventionActionKeys\." Assets/Unity/`
Expected: 출력 없음 (백로그 P0-C 완료 조건 1).

Run: `grep -rn "CardTargetRules" Assets/`
Expected: 출력 없음.

- [ ] **Step 6: 커밋**

```bash
git add -A Assets/Unity/BattleScreenController.cs Assets/Unity/DeckPlaytestController.cs
git commit -m "refactor: UI 대상 흐름을 DescribeTargeting 질의로 전환, CardTargetRules 삭제"
```

---

### Task 5: 도달 불가 유닛 대상 UI 삭제

**Files:**
- Modify: `Assets/Unity/UnitView.cs`
- Modify: `Assets/Unity/CardSelectionController.cs`
- Modify: `Assets/Unity/BattleScreenController.cs` (`SpawnUnits` ~95-127행, `CurrentValidTargets` ~403-429행)
- Modify: `Assets/Tests/UnityEditMode/BattleScreenUnitIdentityTests.cs`
- Modify: `Assets/Tests/UnityEditMode/TargetSelectionVisualTests.cs`

**Interfaces:**
- Consumes: 없음.
- Produces: `UnitView`에서 대상 API 제거 — `BindTarget`/`SetTargetable`/`SetTargetSelection` 소멸. `CardSelectionController`에서 `RegisterUnitTarget`/`ClearUnitTargets` 소멸. Task 6이 이 상태를 전제.

- [ ] **Step 1: `UnitView` 대상부 삭제**

제거 목록 — 필드 `_targetHighlight`·`_targetDim`·`_targetButton`, 색 `TargetCandidate`·`TargetSelected`, `_memberId`, 메서드 `BindTarget`·`SetTargetable`·`SetTargetSelection`, `SetHp`의 `if (current <= 0) { SetTargetable(false); }` 블록, `using System;`(Action 전용이었음). `EditorCreate`에서 `targetHighlight`·`targetButton`·`targetDim` 생성·할당·`SetTargetable(false)` 줄 제거, `portrait.raycastTarget`을 `false`로 (버튼 targetGraphic 용도였음).

- [ ] **Step 2: `CardSelectionController` 유닛부 삭제**

제거 목록 — `_unitTargets` 딕셔너리, `RegisterUnitTarget`, `ClearUnitTargets`, `RefreshTargetVisuals`의 `foreach (var pair in _unitTargets)` 블록, `EndSelectionVisuals`의 `foreach (var view in _unitTargets.Values)` 블록, `IsPicked`(유닛 시각화 전용이었음). `UnitView` 참조가 0이 되었는지 확인.

- [ ] **Step 3: `BattleScreenController` 배선 삭제**

`SpawnUnits`: `_selection.ClearUnitTargets();` 줄, 파티·적 루프의 `SelectionTargetRef.PartyMember/Enemy` 생성, `view.BindTarget(...)` 호출, `_selection.RegisterUnitTarget(...)` 호출 제거 (뷰 생성·Bind·딕셔너리 등록은 유지).
`CurrentValidTargets`: `case SelectionTargetKind.PartyMember`·`case SelectionTargetKind.Enemy` 분기 제거.

- [ ] **Step 4: Unity 테스트 조정**

- `TargetSelectionVisualTests.cs`: `Unit_target_state_uses_gold_for_candidate_and_blue_for_selected` 테스트와 `CandidateOutline`·`SelectedOutline` 상수 삭제 (화살표·레일 테스트 2건은 유지).
- `BattleScreenUnitIdentityTests.cs`:
  - `Party_unit_button_completes_common_single_target_selection_without_confirm` 테스트 삭제.
  - `BeginPartyTargeting` 헬퍼와 호출 삭제.
  - `ViewById`/`MemberId`를 `_memberId` 리플렉션 대신 컨트롤러의 제품 딕셔너리로 교체:

```csharp
        private UnitView ViewById(RectTransform row, string id)
            => row == _partyRow
                ? GetField<Dictionary<string, UnitView>>(_controller, "_partyUnits")[id]
                : GetField<Dictionary<string, UnitView>>(_controller, "_enemyUnits")[id];
```

  - `Snapshot`/`ViewSnapshot`/`AssertViewUnchanged`에서 `ClickMemberId`·`Targetable` 필드 제거 (Name·Hp·Status·sibling 검증은 유지). `PartyViews().ToDictionary(MemberId, Snapshot)`는 컨트롤러 딕셔너리 순회로 교체:

```csharp
        private Dictionary<string, ViewSnapshot> PartySnapshots()
            => GetField<Dictionary<string, UnitView>>(_controller, "_partyUnits")
                .ToDictionary(pair => pair.Key, pair => Snapshot(pair.Value));

        private Dictionary<string, ViewSnapshot> EnemySnapshots()
            => GetField<Dictionary<string, UnitView>>(_controller, "_enemyUnits")
                .ToDictionary(pair => pair.Key, pair => Snapshot(pair.Value));
```

- [ ] **Step 5: 검증 (헤드리스 + Unity EditMode)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: PASS.

Run: `/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /private/tmp/p0c-editmode-results.xml -logFile /private/tmp/p0c-editmode.log; grep -o 'result="[^"]*"' /private/tmp/p0c-editmode-results.xml | head -3`
Expected: `result="Passed"`.

Run: `git status --short` — Unity가 만든 `TargetingRequirement.cs.meta` 등 신규 `.meta`만 있는지 확인하고 함께 스테이징. 그 외 생성물은 스테이징하지 않는다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Unity/UnitView.cs Assets/Unity/CardSelectionController.cs Assets/Unity/BattleScreenController.cs Assets/Tests/UnityEditMode/ Assets/Core/Intervention/TargetingRequirement.cs.meta
git commit -m "refactor(unity): 도달 불가 유닛 대상 선택 UI 삭제"
```

---

### Task 6: `SelectionTargetRef` 축소

**Files:**
- Modify: `Assets/Core/Simulation/Presentation/SelectionTargetRef.cs`
- Modify: `Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs`
- Modify: `Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs`

**Interfaces:**
- Consumes: Task 5 완료 상태 (유닛 소비자 0).
- Produces: `SelectionTargetKind { None, ExecutionCard }`, `SelectionTargetRef { Kind, Index }` — `EntityId`·`PartyMember`·`Enemy` 팩토리 소멸.

- [ ] **Step 1: 타입 축소**

`SelectionTargetRef.cs` 전체 교체:

```csharp
using System;

namespace FateWeaver.Simulation.Presentation
{
    // Unit kinds (party member, enemy...) return here when the intervention card design
    // adds unit targets — see the 2026-07-28 P0-C targeting spec, §2 policy 2.
    public enum SelectionTargetKind { None, ExecutionCard }

    public readonly struct SelectionTargetRef : IEquatable<SelectionTargetRef>
    {
        public SelectionTargetKind Kind { get; }
        public int Index { get; }

        private SelectionTargetRef(SelectionTargetKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static SelectionTargetRef ExecutionCard(int index)
            => new SelectionTargetRef(SelectionTargetKind.ExecutionCard, index);

        public bool Equals(SelectionTargetRef other)
            => Kind == other.Kind && Index == other.Index;

        public override bool Equals(object obj)
            => obj is SelectionTargetRef other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ Index;
            }
        }
    }
}
```

- [ ] **Step 2: 머신 테스트 치환**

`CardSelectionMachineTests.cs`에서 `SelectionTargetKind.PartyMember` → `SelectionTargetKind.ExecutionCard`, `SelectionTargetRef.PartyMember("member-a"/"member-b"/"member-c")` → `SelectionTargetRef.ExecutionCard(0/1/2)`로 기계 치환 (머신은 종류 불가지론이라 시나리오 의미 불변). 단 `Target_from_wrong_domain_is_ignored` 테스트는 남은 종류가 하나뿐이라 시나리오 구성이 불가능하므로 삭제 — 종류 불일치 가드는 유닛 종류가 돌아올 때 테스트와 함께 부활한다.

- [ ] **Step 3: 컨트롤러 테스트 치환**

`CardSelectionControllerTests.cs`의 `PartyMember` 참조 4개 테스트를 같은 규칙으로 치환 (`EntityId` 단언이 있으면 `Index` 단언으로).

- [ ] **Step 4: 검증**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 2>&1 | tail -5`
Expected: PASS.

Run: `grep -rn "PartyMember\|Enemy" Assets/Core/Simulation/Presentation/ Assets/Unity/CardSelectionController.cs`
Expected: 출력 없음.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Presentation/SelectionTargetRef.cs Assets/Core/Tests/EditMode/CardSelectionMachineTests.cs Assets/Tests/UnityEditMode/CardSelectionControllerTests.cs
git commit -m "refactor: SelectionTargetRef를 실존 대상 종류로 축소"
```

---

### Task 7: 최종 검증과 문서 마감

**Files:**
- Modify: `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md` (§5 상태를 구현 완료로)
- Move: 이 계획 → `docs/superpowers/archive/plans/2026-07-28-p0c-targeting-metadata.md`
- Modify: `docs/superpowers/README.md` (활성 계획 표에서 이 계획 제거 — 스펙 행은 `current`로 유지)

- [ ] **Step 1: 전체 헤드리스 + Unity EditMode 재실행**

Run: 두 명령 모두 (Global Constraints 참조).
Expected: 모두 PASS. 실패 시 여기서 멈추고 원인 수정.

- [ ] **Step 2: 완료 조건 대조**

```bash
grep -rn "EffectKeys\.\|InterventionActionKeys\." Assets/Unity/          # 출력 없음
grep -rn "CardTargetRules" Assets/ docs/superpowers/specs/ | grep -v archive | grep -v 2026-07-28   # 출력 없음
grep -rn "BindTarget\|SetTargetable\|RegisterUnitTarget" Assets/          # 출력 없음
git status --short                                                        # 깨끗함
```

- [ ] **Step 3: 백로그 §5 상태 갱신 + 계획 보관 + 색인**

백로그 §5 상태 줄을 "**구현 완료 (2026-07-28), 머지 후 사용자 Play 검증 대기**"로 바꾸고 구현 기록 링크 추가. `git mv`로 계획을 `archive/plans/`로 이동, README 활성 계획 표에서 제거.

- [ ] **Step 4: 커밋**

```bash
git add -A docs/superpowers/
git commit -m "docs: P0-C 구현 완료 기록과 계획 보관"
```

---

## 계획 자체 검토 기록

- 스펙 §3.1~§3.6 ↔ Task 1~6 대응 확인. §5 검증 계획 1→Task 1·2, 2→Task 3, 3→Task 1(Fake 핸들러), 4→Task 5·7, 5→기존 결정론 스위트가 커버.
- 유닛 종류 삭제로 `Target_from_wrong_domain_is_ignored`가 구성 불가능해지는 것은 Task 6 Step 2에 명시 (가드 코드는 유지, 테스트만 부활 대기).
- 프리팹의 TargetHighlight/TargetDim 자식과 직렬화 참조 잔재는 코드가 참조하지 않으므로 무해 — 스펙 §3.6대로 병합 후 사용자 정리 항목.
