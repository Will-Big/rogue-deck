# P0-C — 대상 선택 메타데이터와 입력 흐름 일반화 설계

- 날짜: 2026-07-28
- 상태: `current`
- 권위 범위: 카드 플레이 전 대상 요구의 선언·질의·검증 경로
- 선행 문서: [아키텍처 리팩터링 백로그 §5](../plans/2026-07-16-architecture-refactor-backlog.md),
  [파티 기반 전투](2026-07-15-party-foundation-design.md),
  [열린 카드 저작 구조](2026-07-19-open-card-authoring-design.md)

## 1. 문제

"이 카드를 내려면 플레이어에게 무엇을 물어야 하는가"라는 하나의 질문에 대한 답이 다섯 곳에
흩어져 있고, 그중 두 곳은 사실을 소유자에게 묻는 대신 개입 키 이름에서 추측한다.

| 위치 | 형태 | 문제 |
|---|---|---|
| `CardTargetRules.RequiredRailTargets` | `Key == SwapExecutionOrder ? 2 : 1` | 키 이름으로 대상 수 추측. 새 2대상 개입은 침묵 실패 |
| `DeckPlaytestController:132` | 같은 키 비교의 복사본 | 위와 동일 + 중복 선택 무방비 |
| `BattleScreenController:162-196` | 4가지 단정(실행=무대상, 스왑=2, 개입=1~2, 대상=레일) | 새 대상형 능력마다 UI 수정 강제 |
| `PartyTargetRules.RequiresExplicitAllyTarget` | `ApplyStatusPayload + PartyMember` 비교 | 반쪽 메타데이터(bool, 아군 고정) |
| `SwapExecutionOrderHandler.CanApply` | `Target != null && SecondaryTarget != null` | 대상 수의 진짜 원본이지만 밖에서 질의 불가 |

부수 결함:

- **실버그** — 자리 교환에서 1번 대상 == 2번 대상이 코어를 통과한다. 레거시 화면에서 같은 레일
  카드를 두 번 클릭하면 운명력이 차감되고 카드가 버려지지만 아무 일도 일어나지 않는다.
- **도달 불가 코드 ~35줄** — 유닛(아군·적) 대상 선택 UI(`UnitView` 대상부,
  `CardSelectionController`의 유닛 등록부, `BattleScreenController.CurrentValidTargets`의 유닛
  분기)가 완성된 채 배선이 끊겨 있다. 유일한 `BeginTargetSelection` 제품 호출부가
  `SelectionTargetKind.ExecutionCard`를 하드코딩하므로 유닛 버튼의 `interactable`은 항상 false다.
- **유령 배선** — `ExecutionCardInstance.TargetId`는 읽는 곳이 4곳이지만 제품 코드에서 쓰는 곳이
  0곳이다. 런타임에 항상 null이라 명시적 대상 분기 전체가 테스트에서만 실행된다.

## 2. 확정 정책

설계 대화에서 확정한 전제이며, 이 스펙의 모든 결정이 이 전제 위에 있다.

1. **실행 카드는 플레이 시 대상을 고르지 않는다.** 실행 효과의 대상은 저작 데이터
   (`StatusApplyTarget`, `TargetSelector`)로 명시되고 코어가 자동 해결한다.
   `DeckCombatSession`의 덱 구성 시점 금지(아군 대상 실행 카드 거부)는 버그가 아니라 이 정책의
   강제 장치이므로 유지한다.
2. **대상 선택은 개입 카드의 몫이다.** 개입 카드의 대상 종류 확장(적·아군·손패 카드 등)은 개입
   카드 설계가 확정될 때 진행한다. 현재 확정된 대상 종류는 실행 순서(레일) 카드뿐이다.
3. **키는 라우팅 전용이다.** `EffectKey`·`InterventionActionKey`는 레지스트리가 핸들러를 찾는
   토큰이며, 키 이름을 의미 판정(대상 수 추측 등)에 쓰는 코드는 제거 대상이다.
4. **도달 불가 코드는 지금 지운다.** 소비자가 생길 때 그 설계에 맞게 재작성한다. git 히스토리가
   보존하므로 유실이 아니다.

## 3. 목표 구조

```text
[선언]  IInterventionActionHandler.Targeting        ← 핸들러가 자기 요구를 정식 멤버로 선언
   ↓
[질의]  DeckCombatSession.DescribeTargeting(handIndex) → TargetingRequirement
   ↓
[집행]  UI: 답의 종류·개수대로 CardSelectionMachine 구동 (키 해석 없음)
   ↓
[검증]  DeckCombatSession.PlayInterventionCard: 중복 대상 등 최종 유효성 판정
```

### 3.1 `TargetingRequirement` (코어, 신규)

```csharp
// FateWeaver.Core.Intervention (Assets/Core/Intervention/TargetingRequirement.cs)
public enum TargetKind { None, RailCard }

public readonly struct TargetingRequirement
{
    public TargetKind Kind { get; }
    public int Count { get; }
    public bool AllowDuplicates { get; }

    public static readonly TargetingRequirement None;          // default: (None, 0, false)
    public static TargetingRequirement RailCards(int count);   // (RailCard, count, false)
}
```

- `TargetKind`는 §10(의도적으로 닫힌 분기)과 같은 지위의 닫힌 열거다. 새 대상 종류는 값 추가로
  확장하며, 개입 설계 확정 전에는 `RailCard`뿐이다. `Ally`, `Enemy`, `HandCard` 등은 그때 추가한다.
- 백로그가 예시한 제약(생존 조건, 자기 자신 허용 등)은 지금 소비자가 없으므로 필드를 만들지
  않는다. 필요해질 때 이 구조체에 추가한다. `AllowDuplicates`만 실버그(중복 스왑)의 직접 소비자가
  있어 포함한다.

### 3.2 핸들러 선언 (선언부)

`IInterventionActionHandler`에 **정식 멤버**로 추가한다. 선택적 인터페이스가 아닌 이유: 개입
핸들러에게 대상 수는 본질이며, 정식 멤버는 새 핸들러의 선언 누락을 침묵 실패가 아니라 컴파일
에러로 만든다.

```csharp
public interface IInterventionActionHandler
{
    InterventionActionKey Key { get; }
    TargetingRequirement Targeting { get; }   // 신규
    bool CanApply(InterventionPlayContext ctx);
    void Apply(InterventionPlayContext ctx);
}
```

| 핸들러 | 선언 |
|---|---|
| `ChangeExecutionOrderHandler` | `RailCards(1)` |
| `SwapExecutionOrderHandler` | `RailCards(2)` |
| `LockHandler` | `RailCards(1)` |

`IEffectHandler`는 변경하지 않는다. 정책 1에 따라 실행 효과에는 선언할 대상 요구가 없다.

### 3.3 세션 질의 (질의부)

키 추측이 존재했던 구조적 이유는 정적 헬퍼가 레지스트리에 닿을 수 없어서다. 레지스트리를 가진
`DeckCombatSession`이 질의 창구가 된다.

```csharp
// DeckCombatSession (신규)
public TargetingRequirement DescribeTargeting(int handIndex)
{
    // 범위 밖 인덱스: TargetingRequirement.None
    // 실행 카드(정책 1) 또는 InterventionAction 없음: TargetingRequirement.None
    // 개입 카드: _actions.Resolve(def.InterventionAction.Key).Targeting
}
```

`CardTargetRules.cs`는 파일째 삭제한다.

### 3.4 UI 집행 (집행부)

`BattleScreenController.OnHandClicked`의 개입 분기는 답을 집행만 한다.

- 대상 수 상한 가드(`requiredTargets > 2`)는 삭제한다. `CardSelectionMachine`은 이미 개수에
  일반화되어 있다.
- 후보 종류는 `req.Kind`를 `SelectionTargetKind`로 매핑해 얻는다. 종류→후보 뷰 매핑
  (`CurrentValidTargets`)은 뷰 배선 지식이므로 UI에 남는 것이 맞다.
- `Category == CardCategory.Execution` 분기(배치 연출 vs 선택 연출)는 유지한다. P0-B2가 확정한
  대로 플레이 경로 구분은 `CardCategory`의 정당한 몫이며, 제거 대상은 키 해석이지 카테고리
  분기가 아니다.
- `DeckPlaytestController:132`의 키 비교는 `DescribeTargeting` 질의로 대체한다.

Unity 컨트롤러에서 `EffectKeys`·`InterventionActionKeys` 직접 비교는 0곳이 된다
(백로그 P0-C 완료 조건 1).

### 3.5 코어 검증 (검증부)

`PlayInterventionCard`가 핸들러의 `Targeting`으로 최종 유효성을 판정한다.

- `!AllowDuplicates`이고 `secondaryZoneIndex == targetZoneIndex`이면 거부한다. 거부 시 운명력·
  손패·상태는 변하지 않는다(기존 거부 경로와 동일).
- 기존 검증(인덱스 범위, 카테고리, `CanApply`)은 유지한다. UI의 중복 방지는 이 규칙의
  미리보기가 된다.

### 3.6 도달 불가 코드 삭제

정책 4에 따라 다음을 삭제한다.

| 삭제 대상 | 내용 |
|---|---|
| `UnitView` 대상부 | `BindTarget`, `SetTargetable`, `SetTargetSelection`, `_targetButton`·`_targetDim`·`_targetHighlight` 필드, `TargetCandidate`·`TargetSelected` 색상 |
| `CardSelectionController` 유닛부 | `RegisterUnitTarget`, `ClearUnitTargets`, `_unitTargets` |
| `BattleScreenController` | `CurrentValidTargets`의 `PartyMember`·`Enemy` 분기 |
| `SelectionTargetRef` | `SelectionTargetKind.PartyMember`·`Enemy` 값과 대응 팩토리 |
| 관련 테스트 | 위 표면을 리플렉션으로 강제 실행하던 테스트(`BattleScreenUnitIdentityTests`의 대상 선택부, `CardSelectionControllerTests`의 유닛 종류 사용부) 삭제·조정 |

`UnitView` 프리팹이 삭제되는 직렬화 필드를 참조하면 Unity가 missing reference로 보고할 수 있다.
프리팹·씬 저작은 이 워크트리 범위 밖이므로(저장소 규칙 17), 병합 후 사용자 확인 항목으로
전달한다. EditMode의 `EditorCreate` 경로가 해당 필드를 코드로 만들면 함께 정리한다.

## 4. 범위 밖 (의도적 보류)

- **`ExecutionCardInstance.TargetId` 제거** — 유령 배선이지만, 제거는 `DamageHandler`·
  `ApplyStatusHandler`·`ConditionEvaluator`의 명시적 대상 분기와 그 테스트 표면까지 걷어낸다.
  개입 설계가 확정되면 이 필드가 다시 소비자를 얻을 가능성이 있어, 그 시점에 채우거나 지우는
  것으로 결정한다. 백로그에 후속 항목으로 기록한다.
- **적 선택 로직 복제 통합** (`DamageHandler.SelectEnemy` ≡ `ApplyStatusHandler.SelectTargetEnemy`)
  — 백로그 §12.3(P2)의 기존 항목이며 이 작업과 섞지 않는다.
- **개입 대상 슬롯 일반화** (`InterventionPlayContext`의 `ExecutionCardInstance` 고정 2슬롯) —
  정책 2에 따라 개입 설계 확정 시 진행한다.
- **`"[검증] 선택 방어"` 이름 정리** — 저작이 `Self`이므로 이름의 "선택"은 좌초된 옛 의도다.
  카드 콘텐츠 정리(백로그 §12.4 시작덱 표 대조)와 함께 처리한다.

## 5. 검증 계획

모든 신규 규칙 로직은 헤드리스 테스트로 검증한다(저장소 규칙 12).

1. **선언·질의** — 실행 카드 → `None`; `pull_forward`·`push_back`·`lock` → `RailCard × 1`;
   `swap_positions` → `RailCard × 2`. 범위 밖 인덱스 → `None`.
2. **중복 대상 회귀** — 자리 교환에 같은 존 인덱스 두 개 → `false` 반환, 운명력·손패·실행 순서
   불변(백로그 완료 조건 "잘못된 대상에서 상태·운명력·손패가 변하지 않는다").
3. **2대상 확장 증명** — 테스트 전용 2대상 개입 핸들러를 등록해 `DescribeTargeting`이 UI 수정
   없이 `RailCard × 2`를 돌려주는지 확인(백로그 완료 조건 "샘플 2대상 개입 액션을 중앙 UI 수정
   없이 추가"의 헤드리스 대응).
4. **기존 회귀** — 전체 헤드리스 스위트 + Unity `-batchmode` EditMode 스위트 통과. 삭제된 유닛
   대상 표면을 참조하던 테스트는 함께 제거되었는지 확인.
5. **타임라인 불변** — 같은 시나리오+시드의 이벤트 타임라인이 변경 전후 동일(이 작업은 규칙
   로직의 결과를 바꾸지 않고 선언·질의·검증 경로만 추가하므로).

## 6. 완료 조건

- [ ] Unity 레이어(`Assets/Unity`)에서 `EffectKeys`·`InterventionActionKeys` 직접 비교 0곳
- [ ] `CardTargetRules.cs` 삭제, 키 이름 기반 대상 수 추측 0곳
- [ ] `IInterventionActionHandler` 구현 3종 모두 `Targeting` 선언
- [ ] 중복 대상 자리 교환이 코어에서 거부되고 자원이 보존되는 회귀 테스트 통과
- [ ] 도달 불가 유닛 대상 UI 표면 삭제(§3.6 표 전체)
- [ ] 전체 헤드리스 테스트·Unity EditMode 테스트 통과
- [ ] 병합 후 사용자 Play 확인 + `UnitView` 프리팹 직렬화 참조 정리 확인

백로그 §5의 원 완료 조건 중 "샘플 신규 아군 대상 효과 추가"는 정책 1(실행 카드 무대상)에 따라
이 스펙에서 제외되었고, 아군·적 대상 종류는 개입 설계 확정 시 `TargetKind` 값 추가로 진행한다.
