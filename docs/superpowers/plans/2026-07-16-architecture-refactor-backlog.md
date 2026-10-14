# Fate Weaver — 확장성·하드코딩 후속 리팩토링 백로그

- 작성일: 2026-07-16
- 개정일: 2026-10-13 — 코드 생성 저작 경로 폐기와 JSON 직접 저작 방침을 반영했다
  (2026-08-04: 완료·대체된 항목의 현황을 아래 표로 반영)
- 문서 유형: `active-roadmap`
- 주 도메인: `architecture`
- 상태: `active` — P1-B, P1-C, P2와 §12·§13·§14·§15 항목이 남았다
- 현재 범위에서 분리된 작업: 저작 구조, 효과 기반 카드 성질 합성, 대상 선택 메타데이터, RNG 통합,
  SO 단일 원본화, Unity 프리팹화

## 0. 항목별 현황 (2026-08-04)

현재 유효한지는 이 표로 먼저 확인한다. §7·§14.1은 2026-10-13의 저작 도구 제거 방침을 반영한다.

| 절 | 항목 | 현황 |
|---|---|---|
| §3 | P0-A RNG 단일화 | **완료** (2026-07-18) |
| §4 | P0-B 열린 카드 저작 구조 | **완료** — [열린 카드 저작 구조 설계](../specs/2026-07-19-open-card-authoring-design.md) |
| §4.1 | P0-B2 `CardType` 제거 | **완료** — `CardDefinitionDataTests`가 부재를 잠근다 |
| §5 | P0-C 대상 선택 메타데이터 | **완료** — [대상 선택 메타데이터 설계](../specs/2026-07-28-p0c-targeting-metadata-design.md) |
| §6 | P1-A SO 단일 원본화 | **대체·완료** — 원본은 SO가 아니라 **JSON**이 됐고, 계획 3d(2026-08-05)가 남은 C# 골든 목록까지 지워 잔여가 없다. 아래 §6 머리말 참고 |
| §7 | P1-B Unity 프리팹화 | `active` — 저장된 프리팹·씬 직접 저작. 남은 런타임 컴포넌트 생성과 상태 아이콘 문자열 참조 점검은 §7·§14.1 |
| §8 | P1-C 전투 튜닝 데이터화 | `active` — 운명력(`fateEnergyPerTurn`)·생존자 수별 드로우(`drawByLivingCount`)는 `Content/combat_rules.json`으로, 파티원 최대 HP(`maxHp`)는 `Content/Characters/*.json`으로(생존 충전 `surviveCharges`도 옮겨졌으나 2026-09-18 전투 실행 계약 계획 T0에서 제거) 전투 노드 2단계(2026-09-17)가 옮겼다. 다만 `DeckCombatSession`(`Assets/Core/Simulation/DeckCombatSession.cs:51,66,90`) 생성자의 `fateEnergyPerTurn = 3` 기본값 등 제품 경로 매직 넘버가 남아 있고 `CombatTuning` 순수 모델도 없어, 절의 나머지 완료 조건이 끝나지 않았으므로 현황은 바꾸지 않는다 |
| §9 | P2 표현 경계 정리 | `active` — 전투 화면 분해가 일부 선행됐다 |
| §12·§13 | 2026-07-25 점검, 2026-07-30 상태 이상 논의 | `active` |
| §14 | 2026-09-04 규칙 부채 점검 | `active` |
| §15 | 상태 수치의 수명별 층과 저작값 읽는 통로 | `active` — [카드·상태 변경 계획](2026-10-07-card-status-mutation-roadmap.md)이 이어받았다(2026-10-08, 대기열 F10-1·F10-2). 이 절은 문제의 배경으로 남긴다 |

## 1. 목적

카드 설명 레지스트리 설계 과정에서 발견한 확장성 병목과 저장소 규칙 위반을 현재 작업에 섞지 않고,
독립적으로 설계·검증할 수 있는 후속 작업으로 관리한다.

우선순위 정의:

- **P0**: 결정론 또는 새로운 카드 능력 확장을 직접 막는 구조. 새 콘텐츠 확대 전에 해결.
- **P1**: 제품 콘텐츠의 단일 원본과 Unity 유지보수성을 훼손하는 구조. 제품화 전에 해결.
- **P2**: 현재 프로토타입은 동작하지만 아키텍처 경계를 약화하는 구조. P0/P1 이후 정리.

## 2. 권장 실행 순서

```text
P0-A RNG 단일화
    |
    v
P0-B 열린 카드 저작 구조
    |
    v
P0-B2 효과 기반 카드 성질 합성
    |
    v
P0-C 대상 선택 메타데이터
    |
    v
P1-A SO 단일 원본화
    |
    v
P1-B Unity 프리팹화
    |
    v
P2 표현 경계 정리
```

RNG 통합은 다른 작업과 독립적이지만 결정론 불변식 때문에 가장 먼저 수행한다. SO 단일 원본화는 새 저작 모델이
결정되고 `CardType`이 제거된 뒤 진행해야 재마이그레이션을 피할 수 있다. 대상 선택 메타데이터는 카드 성질을
효과에서 도출하는 공통 방향을 전제로 설계한다.

## 3. P0-A — 전투 RNG를 CombatState로 단일화

- 상태: **완료 (2026-07-18)** — 구현 기록:
  [`2026-07-18-p0a-rng-unification.md`](../.archive/plans/2026-07-18-p0a-rng-unification.md)

### 문제

`Deck`, `RandomMovesetPolicy`, `ShuffleBagPolicy`, `SelfLockPolicy`가 각각 `System.Random`을 생성한다. 개별 시드로
재현은 가능하지만 모든 규칙 무작위가 `CombatState`의 시드 RNG를 경유한다는 저장소 불변식과 다르다.

### 목표 구조

- 코어에 결정적 `IRandomSource` 또는 동등한 작은 추상화 정의
- `CombatState`가 전투 RNG의 유일한 소유자
- 덱 셔플, 적 카드 선택, 잠금 대상 선택은 RNG를 주입받아 사용
- 호출 순서와 RNG 소비가 이벤트 타임라인에서 재현 가능
- 정책별 임의 seed 파생과 `new Random()` 제거

### 완료 조건

- 제품 규칙 경로의 `new Random` 검색 결과가 `CombatState` 내부 생성 한 곳뿐
- 같은 초기 상태·입력·시드의 전체 이벤트 타임라인이 동일
- 다른 시드에서 의미 있는 분산이 유지됨
- 덱 재셔플과 적 정책 조합 회귀 테스트 통과

## 4. P0-B — 열린 카드 저작 구조

- 상태: **완료 (2026-07-19)** — 구현 기록:
  [`2026-07-19-p0b-implementation-record.md`](../.archive/plans/2026-07-19-p0b-implementation-record.md)
- 최종 검증: SO 재생성 diff 없음, 헤드리스 307/307, Unity EditMode 356/356, 사용자 Play 확인

### 문제

`EffectKind`, `StatusKindRef`, `InterventionKind` enum과 `CardSpecMapper` 중앙 switch가 코어의 타입 안전 키 기반 열린
확장을 다시 닫힌 집합으로 만든다. `EffectData`도 ApplyStatus 전용 필드를 직접 포함해 새로운 파라미터 형태가
추가될 때 공용 모델이 계속 커진다.

### 목표 구조

- 새 효과 저작 타입은 기존 enum/switch를 수정하지 않고 추가
- 콘텐츠 JSON과 순수 C# 스펙이 같은 로딩 계약을 사용
- 효과별 파라미터는 이름과 타입이 보존되며 핸들러가 자기 파라미터를 검증
- 카드·콘텐츠는 AI 세션에서 JSON을 직접 저작한다([콘텐츠 저작 규칙](../../agents/content-authoring.md))
- 잘못된 키·필드·파라미터는 JSON 로더·부팅 검증에서 실패
- 조건은 설계대로 닫힌 조합형을 유지하되 효과/상태/개입 액션은 열린 확장으로 유지

### 설계 시 비교할 대안

1. Unity `[SerializeReference]` 기반 효과별 authoring spec + 순수 DTO export
2. 타입 안전 키 + 검증된 이름 있는 parameter bag
3. 효과 정의 ScriptableObject + 순수 생성 코드 descriptor

리플렉션 자동 등록이나 raw string dictionary만으로 타입 안전성을 포기하는 방식은 채택하지 않는다.

### 완료 조건

- 샘플 신규 효과를 추가할 때 중앙 enum/mapper 수정 없음
- 신규 효과의 실행·설명·저작·검증 경로가 클래스/등록 단위로 국소화됨
- 기존 시작덱·적덱·파티 검증 카드의 JSON 로딩·행동 회귀 유지
- 헤드리스와 Unity가 같은 JSON 콘텐츠를 소비

## 4.1 P0-B2 — `CardType` 제거와 효과 기반 카드 성질 합성

- 상태: **구현 완료, 머지 후 사용자 Play 검증 대기 (2026-07-20)** — 구현 기록:
  [`2026-07-19-p0b2-implementation-record.md`](../.archive/plans/2026-07-19-p0b2-implementation-record.md)
- 우선순위: **P0** — 복합 효과 카드 확장을 직접 왜곡하므로 P0-C와 새 콘텐츠 확대 전에 해결

### 문제

`CardType`은 `Attack`, `Skill`, `Defense` 중 하나만 허용한다. 그러나 `Damage + ApplyStatus(Block)`처럼 피해와
방어를 동시에 수행하는 카드는 여러 성질을 가질 수 있으므로 단일 enum으로 정확히 분류할 수 없다. 현재
`AdjacentCardIs`, `PreviousExecutedCardIs`, `BeforeNextEnemyAttack`, `GrantNextPlayerAttackDamageBonusHandler`가
`CardType.Attack`을 직접 비교하기 때문에 어떤 단일 타입을 선택하느냐에 따라 동일한 효과 구성의 규칙 결과가 달라진다.
조작 카드도 실행 카드용 의미가 없는 `CardType.Skill`을 강제로 가진다.

### 목표 구조

- 실행/조작의 플레이 경로는 기존 `CardCategory.Execution`/`Intervention`만 담당
- `CardType`과 `CardDefinition`, `CardSpec`, `ZoneCardSpec`, `CardAsset`, 생성 코드의 `Type` 필드 제거
- 카드가 피해를 줄 수 있는지는 `Effects`에 `EffectKeys.Damage`가 존재하는지로 판정
- 인접·미래 카드 조건은 아직 실행 결과를 알 수 없으므로 정의의 효과 구성을 판정
- 이전 카드 조건이 "피해 효과를 가진 카드"를 뜻하면 정의의 효과 구성을, "실제로 피해를 준 카드"를 뜻하면
  `CardResolved.DamageDealt` 같은 실행 결과를 사용하며 두 의미를 이름으로 구분
- 기존 공격 타입 의존 조건과 다음 공격 강화는 `Damage` 효과 보유 여부를 사용하도록 이름·설명·저작 데이터 갱신
- 효과 존재 판정은 공통 타입 안전 질의로 모으고, 호출부마다 `Effects` 탐색을 중복하지 않음
- 범용 카드 태그나 capability 레지스트리는 당장 도입하지 않음. 간접 피해처럼 `EffectKeys.Damage`만으로 표현되지 않는
  실제 요구가 생길 때 별도 설계

### 마이그레이션·검증 원칙

- P1-A SO 단일 원본화와 섞지 않고 독립된 스키마 마이그레이션으로 수행
- 현재 카드 정의와 golden 서명에서 `CardType`만 제거한 기준선을 먼저 고정
- 기존 반격·엄호·다음 피해 강화의 행동 의도는 유지하되 판정 근거만 효과 구성으로 변경
- 콘텐츠 JSON에 폐기된 `Type` 필드를 되살리지 않고 로더·행동 회귀를 검증
- 새 외부 패키지, 리플렉션 자동 등록, raw string 효과 판정은 도입하지 않음

### 완료 조건

- 제품·저작·생성·테스트 코드에서 `CardType` 참조가 없음
- 카드 스펙과 JSON에 `Type` 필드가 없고 콘텐츠 로딩 회귀가 통과
- `Damage`만 가진 카드와 `Damage + Block` 복합 카드가 모두 피해 카드 조건을 만족
- `Block`만 가진 카드는 피해 카드 조건을 만족하지 않음
- 이전/인접 카드 조건과 다음 피해 카드 강화가 타입 대신 효과 구성으로 동작하는 헤드리스 회귀 테스트 통과
- 기존 카드 콘텐츠의 실행 순서·효과·설명 결과에서 의도하지 않은 변화가 없음
- 전체 헤드리스 테스트와 Unity EditMode 테스트 통과, 머지 후 사용자 Play 검증

## 5. P0-C — 대상 선택 메타데이터와 입력 흐름 일반화

- 상태: **구현 완료 (2026-07-28), 머지 후 사용자 Play 검증 대기** — 권위 문서:
  [`2026-07-28-p0c-targeting-metadata-design.md`](../specs/2026-07-28-p0c-targeting-metadata-design.md),
  구현 기록: [`2026-07-28-p0c-targeting-metadata.md`](../.archive/plans/2026-07-28-p0c-targeting-metadata.md).
  설계 과정에서 두 가지 정책이 확정되어 원 목표 구조를 좁혔다: 실행 카드는 플레이 시 대상을
  고르지 않으며(대상은 저작 데이터로 명시), 아군·적 등 새 대상 종류는 개입 카드 설계가 확정될 때
  `TargetKind` 값 추가로 진행한다. 아래 원문 중 "아군 1명"·"적 1명" 요구사항 예시는 그 시점의
  범위다. 후속: `ExecutionCardInstance.TargetId`(읽는 곳 4, 제품에서 쓰는 곳 0)는 개입 설계 확정
  시 채우거나 제거한다.

### 문제

`PartyTargetRules`는 `ApplyStatus + PartyMember`를 직접 비교하고, Unity 컨트롤러는
`SwapExecutionOrder`일 때만 두 대상을 요구한다고 직접 판단한다. 새로운 대상형 능력마다 코어와 UI 양쪽의
중앙 분기가 늘어난다.

### 목표 구조

- 카드 플레이 전에 순수 코어가 `TargetingRequirement`를 제공
- 요구사항 예: 없음, 아군 1명, 적 1명, 미래 영역 카드 1장, 미래 영역 카드 2장
- 대상 중복 허용, 생존 조건, 자기 자신 허용 여부 등 필요한 제약을 데이터/descriptor로 표현
- UI는 효과 키를 해석하지 않고 요구사항에 따라 선택 상태 머신을 구동
- 최종 유효성은 동일한 코어 validator가 판정
- 여러 효과가 가진 요구사항의 합성/충돌 규칙을 명시

### 완료 조건

- Unity 컨트롤러에서 `EffectKeys`와 `InterventionActionKeys` 직접 비교 제거
- 샘플 신규 아군 대상 효과와 2대상 개입 액션을 중앙 UI 수정 없이 추가 가능
- 잘못된 대상에서 상태·운명력·손패가 변하지 않는 회귀 테스트
- 대상 선택 취소/사망/중복 선택 흐름 헤드리스 검증

## 6. P1-A — ScriptableObject를 제품 콘텐츠의 단일 원본으로 확정

> **대체·완료 (2026-08-05).** 이 절이 진단한 **문제**는 실재했고 해결됐지만, **해법이 뒤집혔다** —
> 단일 원본은 SO가 아니라 `Assets/StreamingAssets/Content/<종류>/*.json`이다. 모딩 요구(UGC)가
> Unity 에디터 없이 편집 가능한 형식을 요구했기 때문이며, 근거는
> [카드 변형과 런타임 콘텐츠 로딩 설계](../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.5에 있다.
> 계획 3a·3b·3c·3d가 구현을 끝냈다. `CardAsset`·`CardPoolAsset`·`DeckAsset`·`GeneratedCards`·
> `StatusContentDefaults`는 3a–3c가, 골든 축으로 남아 있던 C# 목록(`StarterPoolSpecs`·
> `StarterDeckSpecs`·`PartyPrototypeDeckSpecs`·`StarterDeck`·`PartyPrototypeDeck`·
> `PartyPrototypeCharacterSpecs`·`ContentExportWriter`·`CardContentExporter`)은 계획 3d
> ([구현 기록](../.archive/plans/2026-08-05-card-spec-removal.md))가 지웠다.
>
> **아래 목표·완료 조건 중 `CardAsset`·SO·export를 가리키는 항목은 그대로 읽지 말 것.** 적 카드
> (`GoblinDeck`)의 JSON 전환은 전투 노드 2단계(2026-09-17)가 끝냈다 — 고블린의 적 정의는
> `Content/Enemies/goblin.json`, 고블린 전용 카드는 `Content/Cards/goblin_jab.json`·
> `sly_jab.json`·`crude_guard.json`으로 옮겼다. `WardenDeck`은 이 전환의 대상이 아니다 —
> 2026-08-31에 간수(Warden) 적 자체가 삭제되며 함께 없어졌다(커밋 `68acdda`). 남은 잔여가 없다.

현재 콘텐츠 원본과 AI 세션의 JSON 직접 저작 절차는
[콘텐츠 저작 규칙](../../agents/content-authoring.md)을 따른다. SO에서 규칙 데이터를 내보내거나
C# 카드 카탈로그를 생성하는 경로를 되살리지 않는다.

## 7. P1-B — Unity 프리팹·직렬화 참조 구조로 전환

### 현재 저작 방침

씬·프리팹은 저장된 에셋을 직접 수정하고 참조를 배선한다(AGENTS.md 규칙 17).
전투 화면을 코드로 다시 생성하는 메뉴·빌더·뷰 생성 훅은 사용하지 않는다.

### 남은 점검

- `ExecutionRailView`는 배치 미리보기 인스턴스에 `CanvasGroup`을 런타임에 추가한다
  (`Assets/Unity/Scripts/Battle/ExecutionRailView.cs:355`). 필요한 컴포넌트를 프리팹에 저장하는 경계를 검토한다.
- 상태 아이콘의 문자열 Resources 참조는 §14.1에서 다룬다.

### 완료 조건

- 제품 런타임 경로에 UI 조립 목적의 `new GameObject`, `AddComponent`, 문자열 `Resources.Load`가 없다.
- 주요 UI 프리팹 누락 시 명확한 부팅·검증 오류를 낸다.
- 기존 전투 화면의 카드·유닛·레일·더미 UI는 사용자 Play 확인을 거친다.
- 저장된 에셋의 GUID·직렬화 참조를 보존하고 Unity 배치 EditMode로 검증한다.

## 8. P1-C — 전투 튜닝 데이터화

### 문제

운명력, 손패 크기, 플레이어 HP, 시드 등이 `DeckCombatSession` 기본 인자와 Unity 컨트롤러 상수로 중복된다.

### 목표 구조

- `CombatTuning` 순수 데이터 모델 정의
- Unity에서는 대응 SO가 순수 tuning DTO로 변환
- 세션 생성자는 tuning을 명시적으로 받음
- 테스트 fixture만 의도가 드러나는 로컬 상수를 허용

### 완료 조건

- 제품 경로에서 운명력/손패/HP 기본 매직 넘버 제거
- 동일 tuning으로 Unity와 헤드리스 초기 상태가 일치
- 잘못된 tuning의 부팅 검증

## 9. P2 — 이벤트 타임라인 중심 표현 경계 정리

### 문제

Unity 컨트롤러가 `DeckCombatSession.State`, `Party`, `Enemies`, `CurrentOrder`를 직접 읽어 화면을 갱신한다. 코어의
유일 출력은 이벤트 타임라인이라는 설계 원칙과 차이가 있다.

### 목표 구조

- 코어 이벤트를 재생해 presentation model을 갱신
- UI는 core mutable state를 직접 조회하지 않음
- 이벤트 종류별 표현 확장은 presenter/renderer 등록으로 국소화
- 초기 화면은 명시적 snapshot 이벤트 또는 read-only presentation snapshot 사용

### 완료 조건

- Unity UI에서 `CombatState` 직접 탐색 제거
- 같은 타임라인을 즉시 재생/애니메이션 재생해도 최종 presentation state 동일
- 새 이벤트 표현 추가가 중앙 controller switch를 키우지 않음

## 10. 의도적으로 유지하는 닫힌 분기

모든 switch를 레지스트리로 바꾸지는 않는다.

- `ConditionEvaluator`: 조건은 작고 닫힌 조합형이라는 기존 설계에 따라 중앙 평가 유지
- `StatusLifetimeKind`: 닫힌 값 집합의 문법/변환 분기 유지. (`TargetSelector`·`StatusApplyTarget`은 2026-09-18
  [전투 실행 계약](../specs/2026-09-18-combat-execution-contract-design.md) 구현으로 제거됐다 — 위치는 카드 축 + 효과 진영.)
- `Outcome`, `ConditionTier`: 표현용 닫힌 enum 분기 유지

`CardType`은 복합 효과 카드에서 열린 조합 축임이 확인되어 P0-B2에서 제거한다. 나머지 항목이 실제로 열린 콘텐츠
확장 축으로 바뀔 때만 별도 설계를 거쳐 레지스트리화한다.

**조건 평가의 재검토 조건 (2026-09-18, 전투 실행 계약 계획 검토에서 범위 밖으로 뺐다).** 런타임
`ConditionEvaluator.Evaluate`는 타입 검사 사슬이고(`Assets/Core/Conditions/ConditionEvaluator.cs:10`),
저작 쪽 `ConditionSpec.ToCondition`의 switch는 유지 의도가 주석으로 적혀 있다
(`Assets/Core/Authoring/EffectSpec.cs:21`). 새 조건이 늘어 중앙이 자라는 것이 **실제로 문제될 때**
등록형 전환에 착수하며, 그때 저작 쪽 유지 결정도 다시 본다. 기본 캐릭터 설계(2026-10-06 진행 중)가
"자신이 전열"·"자신에게 상태가 있다" 같은 새 조건을 요구할 수 있어 이 조건이 곧 찰 수 있다.

## 11. 작업 분리 원칙

- 각 P0/P1 항목은 별도 brainstorming/spec/implementation plan을 가진다.
- 한 변경에서 두 개 이상의 대규모 데이터 마이그레이션을 섞지 않는다.
- 각 작업은 기존 콘텐츠의 동작 등가성 테스트를 먼저 확보한다.
- Unity asset 변경은 순수 headless 검증과 사용자 Play 검증을 분리해 기록한다.
- 외부 패키지나 reflection 기반 자동 등록은 사전 승인 없이 도입하지 않는다.

## 12. 2026-07-25 점검에서 추가된 항목

전투 시스템 전면 점검(코어·Unity·저작 파이프라인)에서 확인했으나 기존 P0~P2 항목에 포함되지 않던
구조 문제다. 정합성 결함 4건은 별도 계획
[`2026-07-25-combat-consistency-cleanup.md`](../.archive/plans/2026-07-25-combat-consistency-cleanup.md)에서 처리하며,
아래는 그 범위 밖으로 남은 항목이다.

### 12.1 P0급 — 확장을 직접 막거나 조용히 실패하는 구조

**조건 축의 침묵 실패.** 조건을 하나 추가하려면 서로 연결되지 않은 세 곳을 고쳐야 한다:
`ConditionEvaluator`(평가), `KoreanDescriptionGrammar.ConditionStem`(설명),
`ConditionSpec.ToCondition`(저작 변환). 뒤 두 곳의 `default`가 각각 빈 문자열과 `null`을 조용히
반환하고, `DescriptionCatalogValidator`는 효과·상태·개입 키만 검증하며 `effect.Condition`을 보지 않는다.
문법을 빠뜨리면 카드 설명이 어간 없이 렌더링되고, 저작 변환을 빠뜨리면 조건이 사라진 카드가 예외 없이
생성된다. §10이 조건을 "의도적으로 닫힌 분기"로 두는 판단 자체는 유효하나, 침묵 실패는 별개 문제이므로
최소한 부팅·저작 검증이 조건을 확인해야 한다.

**P2의 선행 조건 — 코어 이벤트 확충.** `ResolutionEvent`는 `TurnStarted`, `CardResolved`,
`CardCancelled`, `PartyMemberDied`, `DeathsDoorSurvived`, `TurnEnded` 6종뿐이다. HP 변화, 상태 부여,
상태 만료, 대형 이동 이벤트가 없어 **타임라인만 재생하는 UI는 방어 아이콘이나 둔화 디버프를 그릴 수
없다.** §9 P2는 컨트롤러 리팩터로 서술되어 있으나 실제로는 코어 이벤트 확충이 선행되어야 하며,
그 전에는 UI가 mutable state를 읽는 것이 강제된 선택이다.

### 12.2 P1급 — 레지스트리 원칙과 튜닝 규칙 위반

**`reward_nullified` 특수 처리.** 여섯 상태 중 이것만 `TurnResolver`가 상태 키를 직접 조회하고,
대응하는 `RewardSuppressionBehavior`는 훅을 하나도 오버라이드하지 않은 빈 클래스다. 나머지 다섯은
모두 `IStatusBehavior`를 경유한다. `ModifyConditionTier` 훅을 추가하면 이 특수 분기와 빈 클래스가 함께
사라진다.

**`VulnerableBehavior` 하드코딩.** ~~`(damage * 3) / 2`로 50%를 고정하고 자신의 `Magnitude`를
무시한다.~~ **2026-07-30 해소** — 배율이 `StatusRule.MultiplierPercent`로 이동해
`CombatState.StatusRules`에서 런타임 조절이 가능하고, 기본값은 `StatusRuleCatalog`에 모였다.
`Magnitude`를 세기로 쓰지 않는 것은 의도된 설계다: 취약의 count는 남은 턴이며 중첩은 지속을
늘릴 뿐 배율을 키우지 않는다("취약 2" = 2턴). 강도와 지속은 서로 다른 축이다.

**비용 이중 원본.** `CardDefinition.EnergyCost`와 `InterventionActionData.InterventionCost`가 별개
필드이고, 개입 플레이 경로는 전자를 아예 읽지 않는다. 현재는 `CardSpecMapper`가 둘 다 같은 값으로
채워 우연히 일치하므로, SO 저작자가 한쪽만 수정하면 표시 비용과 실제 차감 비용이 어긋난다.

**`DeckCombatSession` 모드 분리.** 505줄 한 클래스가 `_isPartyMode` 불리언으로 솔로·파티 두 모드를
겸한다. 파티 생성자는 `playerHp: 0`, `handSize: 0`을 죽은 플레이스홀더로 넘기고, 파티 덱 조립·검증
92줄이 턴 루프 드라이버 안에 있다. 정합성 정리에서 레거시 shim을 제거하면 분기 하나가 줄어들지만
구조 분리는 남는다.

### 12.3 P2급 — 중복과 확장 제약

**적 대상 선택 중복.** `DamageHandler.SelectEnemy`와 `ApplyStatusHandler.SelectTargetEnemy`가 주석까지
동일하다. 파티 쪽에는 `PartyTargeting`이 있으나 적 쪽에 대응 모듈이 없어, 새 적 대상 효과마다
fallback 정책을 복사해 재구현하게 된다.

**단일 적 가정.** 턴 루프가 텔레그래프 카드를 항상 `Enemies[0]`에 귀속시키고 0번 적의 둔화·가속만
실행 순서에 반영한다. `IEnemyTurnPolicy.CardsForTurn`이 소유자 없는 `CardDefinition`을 반환하므로
다중 적 조우를 표현하려면 이 인터페이스부터 바뀌어야 한다.

**러너 배선 중복.** `ZoneCardSpec`으로 미래 영역을 만드는 코드가 네 러너에 복사되어 있고(모두
`InstanceId`·`OwnerId`를 설정하지 않는다), `OutcomeOf(timeline)`은 세 곳에 그대로 중복된다.

### 12.4 문서 드리프트

**덱 루프 설계의 시작덱 표가 코드와 어긋난다.** [`2026-06-22-deck-loop-design.md`](../specs/2026-06-22-deck-loop-design.md)의
시작덱 표는 `current` 권위 문서인데도 베기 피해를 3으로 적고 있으나 코드는 4이며, 현재 시작덱이 싣지 않는
강타 행이 남아 있다. 2026-07-25 정합성 정리에서 앞당김·밀어내기 행만 현재 값으로 맞췄고 나머지는 범위 밖으로
남겼다. 카드 콘텐츠를 다시 손댈 때 이 표 전체를 코드와 대조해야 한다.

### 12.5 콘텐츠 블로커

**독 상태 미구현.** `poison` 상태 키도, `PoisonBehavior`도, 덱 루프 설계가 명시한 "행동 턴 종료 시 발동
후 1 증가"의 훅 지점도 없다. 캐릭터·카드풀 설계가 독을 아키타입 축으로 두고 있으므로, 해당 카드풀을
구현하려면 이 상태와 훅이 먼저 필요하다.

## 13. 2026-07-30 상태 이상 설계 논의에서 추가된 항목

### 13.1 P1급 — 확정된 규칙을 코드가 지키지 않음

**방어와 취약의 적용 순서가 걸린 순서에 좌우된다.** `DamageHandler.FoldIncoming`이 대상의 상태를
`bag.All` 삽입 순서대로 한 루프에서 접으므로, 방어가 취약보다 먼저 걸려 있으면 방어가 먼저 흡수하고
남은 값에 취약이 곱해진다. 확정된 규칙은 "취약을 먼저 곱하고 방어는 추가 체력처럼 마지막에 흡수"다.
**2026-07-30 해소** — `StatusDamageLayer`로 층을 선언하고 `StatusDamageFold`가 배율 층을 모두 접은
뒤 흡수 층을 적용한다. 걸린 순서와 무관하게 같은 결과가 나온다.

**상태 수명이 저작 시점에 고정된다.** `StatusLifetimeKind`는 적용마다 하나를 고르는 구조라
(`Permanent`/`ThisTurn`/`Turns`/`UntilConsumed`/`PhaseVisits`, `Assets/Core/Status/StatusLifetime.cs:5`),
"방어를 이 런 동안 영구로", "독을 이번 턴만으로" 같은 런타임 변경을 표현할 수 없다. 수명은 강도와
마찬가지로 상태별 규칙 파라미터여야 한다.

**2026-09-18 일부 해소 — 만료는 수명에서 분리됐다.** 전투 실행 계약 T6이 공통 만료 정책을 넣었다.
`StatusInstance`가 `Expiry`(만료 정책)와 `VisitsLeft`(남은 기준 시점 방문 수)를 갖고
(`Assets/Core/Status/StatusInstance.cs:15`·`18`), 수명 종류에 `PhaseVisits`가 추가됐다 — 방어의
"다음 턴 준비에 사라진다"가 상태 JSON의 `expiry`로 저작된다(`Content/Statuses/block.json`).
**남은 것은 둘이다:** 이름 정리와 공유 수치 구조.

*이름이 설계 규칙과 뒤집혀 있다 (2026-08-28 논의).* 사용자가 밝힌 규칙은 이렇다 — 상태이상 **임시
객체**(캐릭터 또는 카드에 붙는다)는 `count`만 관리하고, **정보 객체**(중앙 공유)가 `magnitude`를
관리한다. `count`는 남은 턴·충전·스택처럼 전투 중 변하는 수치이고 상태마다 해석이 다르며,
`magnitude`는 임시 객체들이 참조하는 대체로 고정된 공유값이다. 둘 다 카드·유물이 바꿀 수 있다.
그 규칙에 비추면 **저작 경계는 고칠 게 없다** — 카드가 적는 `count` 하나는 임시 객체의 초기 `count`이고,
`magnitude`는 이미 상태 JSON에 있다(`poison.json`의 `growthPerTurn`). "카드 JSON의 `count`를 `turns`와
`magnitude`로 쪼개자"는 방향은 이 규칙과 어긋나 채택하지 않는다. 고칠 곳은 런타임이다: `StatusInstance`가
`Count`와 `Magnitude`를 둘 다 들고 있고(`StatusInstance.cs:11`·`12`), 독은 전투 중 변하는 스택이
`Instance.Magnitude`(규칙상 `count`), 고정 공유값인 성장량이 카탈로그의 `GrowthPerTurnOf`(규칙상
`magnitude`)다. 위치는 맞는데 이름이 서로 바뀌어 있고, 정보 객체 쪽에는 일반화된 `magnitude` 슬롯 없이
상태별 전용 접근자만 있다. **이 정리는 §15와 같은 작업이다 — 따로 하면 두 번 뜯는다.**

**2026-10-08 — [카드·상태 변경 계획](2026-10-07-card-status-mutation-roadmap.md) 큰 계획 1이 이 정리를 맡는다.** 그 계획은 인스턴스의 수치를
`Stacks` 하나로 합치고 `Count`·`Magnitude`·`VisitsLeft`를 없애기로 했다. 위 문단의 `count`/`magnitude`는
2026-08-28 논의의 용어이고, 구현 필드명은 `Stacks`다. 위에서 "일부 해소"로 적은 `VisitsLeft`도 그 계획에서
없어진다 — 방어량과 독립된 N턴 만료는 지원하지 않기로 했다.

아래 목표 구조는 2026-07-30 작성 당시의 것이며, 만료 부분은 위 T6 구현이 대신한다.

목표 구조는 인스턴스에 count 하나만 두고(상태마다 의미가 다르다 — 취약은 남은 턴, 방어는 흡수량),
감쇠를 `{트리거 → 변화량}` 데이터로 옮기는 것이다. 트리거는 최소한 `턴 끝`과 `발동 시` 둘이며 동시에
켤 수 있어야 한다. 조건부 감쇠(독 안정이 독의 성장을 막는 것)는 데이터로 표현되지 않으므로
`데이터 = 기본 감쇠, behavior = 그 위의 예외` 2층을 유지한다. 이는 캐릭터·카드풀 설계 §3.3의
5층 우선순위와 같은 구조다.

영향 범위가 넓다 — `StatusBag`, `ApplyStatusPayload`, `ApplyStatusSpec`, 저작 콘텐츠
(`Content/Cards/*.json`, `Content/Enemies/goblin.json`), 설명 문법의
`LifetimeSuffix`. 별도 계획으로 분리한다. (2026-08-05 갱신: 원문의 `StarterPoolSpecs`·
`StarterDeckSpecs`·`PartyPrototypeDeckSpecs`·`GeneratedCards.cs`는 계획 3b·3d가 제거했다. 2026-09-17
갱신: 원문이 "아직 C#으로 남은 적 덱"이라 부르던 `GoblinDeck`은 전투 노드 2단계가 JSON으로
옮겼고, `WardenDeck`은 2026-08-31 간수 적 삭제로 함께 없어졌다 — 남은 C# 적 덱이 없다.)

### 13.2 P1급 — 플레이어 카드와 콘텐츠 로딩

**`OwnedCard`에 변형을 담을 자리가 없다.** 3계층 카드 모델의 중간 계층이 `Def`와 `OwnerId` 두
필드뿐이라 런 중 영구 강화도, 전투 한정 일시 강화도 표현할 수 없다.

~~**콘텐츠가 편집 시점 코드 생성으로 고정된다.** `CardCodeGenerator`가 `GeneratedCards.cs`를 만들고
코어가 그것을 컴파일하므로, 모드가 카드를 추가하려면 재컴파일이 필요하다.~~ **해결됨 (계획 3b·3c,
2026-08-03~04)** — 카드·상태·덱·풀·캐릭터가 전부 JSON이고 `ContentBootstrap.Load`가 부팅 시 읽는다.

두 항목의 요구와 경계는
[카드 변형과 런타임 콘텐츠 로딩 설계](../specs/2026-07-30-card-mutation-and-runtime-content-design.md)에
확정되어 있다. **콘텐츠 로딩은 계획 3a~3d와 3.5로 구현이 끝났다(2026-08-06). `OwnedCard` 변형은
계획 4로 아직 미착수다** — 선행은 전부 풀렸다. 2026-10-08부터 [카드·상태 변경 계획](2026-10-07-card-status-mutation-roadmap.md)의 큰 계획 3이
이 작업을 맡는다(대기열 F10-3). 범위는 카드에 한정하지 않기로 정해졌다 — 그 계획이 상태 수치와 중앙
상태 규칙을 먼저 다룬다. 단계별 상세는 착수할 때 쓴다.

### 13.3 P2급 — 중복

**카드 소유자 해결이 두 곳에 있다.** `ApplyStatusHandler`의 `ResolvePlayerSelf`/`ResolveEnemySelf`와
`CardActor.StatusesFor`가 같은 fallback 정책(OwnerId 우선, 없으면 후보가 하나일 때만 확정)을 각자
구현한다. 반환 타입이 달라(엔티티 vs `StatusBag`) 즉시 통합되지는 않는다.

**같은 보유자에게 곱셈 상태가 둘 이상 붙을 때의 순서 규칙이 없다.** 단계별 버림에서는 곱셈 순서가
결과를 바꾼다(피해 10에 ×0.75와 ×1.5를 순서만 바꿔 적용하면 10과 11로 갈린다). 현재는 약화가
공격자, 취약이 대상 쪽이라 파이프라인상 순서가 고정되어 문제가 드러나지 않는다. 곱셈 상태가 한
bag에 둘 이상 생기면 층 안의 순서를 규칙으로 정하거나 배율 누적 후 1회 버림으로 바꿔야 한다.

### 13.4 P1급 — 전투가 무엇을 했는지 볼 수 없다

~~**배틀 화면이 타임라인을 표시하지 않는다.** `BattleScreenController`는 `SetMessage`로 조작 안내만
띄우고 해석 결과를 보여주지 않는다.~~ **2026-08-28 해소** — `BattleScreenController`가 턴 해석 직후
`TimelineTextFormatter.Format`을 호출해 Console에 타임라인을 찍는다. 2026-07-31 검증에서 앞열
파티원이 HP 1에서 죽지 않는 현상을 확인했을 때, 실제 원인인 치명 버팀(`DeathsDoorSurvived`)이 정상
발행되고 있었는데도 화면에서 판단할 방법이 없었던 그 문제다.

~~**타임라인이 상호작용을 다 담지 않는다.** 상태가 부여·만료되는 사건에 대응하는 이벤트가 아예 없고
(`StatusTicked`·`StatusTransferred`만 있다), 피해가 약화·취약·방어로 어떻게 바뀌었는지는
`CardResolved.DamageDealt` 총합 하나로만 남는다.~~ **2026-08-28 해소** — `StatusApplied`·
`StatusExpired` 이벤트가 상태 부여·만료를 남기고, `CardResolved.DamageSteps`가 약화·취약·방어의
단계별 피해 변화를 싣는다. 규칙 11대로 여전히 타임라인이 유일한 원천이다.

~~**플레이테스트 화면의 렌더링이 조용히 버린다.** `DeckPlaytestController.RefreshTimeline`은
`CardResolved`와 `TurnEnded`만 처리하고 나머지 이벤트를 아무 표시 없이 건너뛴다.~~ **2026-08-28
정정** — 이 관찰은 이미 낡아 있었다. `DeckPlaytestController`는 이 계획 착수 전인 `ac4e47f`
(2026-08-03)가 삭제했다. 남은 화면은 `BattleScreenController` 하나뿐이라 위 항목으로 해소된다.

**전투 상호작용 로그 계획이 이 항목을 구현 완료했다 (머지 대기, 2026-08-28).**

## 14. 2026-09-04 규칙 부채 점검에서 추가된 항목

`Tools/verify.sh`의 규칙 검사를 도입하면서 드러난 항목이다. 알려진 예외는
`Tools/lint-allow.txt`에 사유와 함께 적는다.

### 14.1 P1급 — 상태 아이콘을 문자열 경로로 찾는다

`CardStatusIconSprites`는 `Status/icon_lock` 문자열을 저장하고 `Resources.LoadAll`·`Resources.Load`로
아이콘을 읽는다(`Assets/Unity/Scripts/Cards/CardStatusIconSprites.cs:12,42,49`). 폰트 문자열 로딩을
위한 예외는 생성 도구 제거와 함께 삭제한다(`Tools/lint-allow.txt`).

**목표 구조.** 아이콘을 표현 자원 카탈로그의 GUID 참조로 옮긴다. 이 카탈로그에는 규칙 수치를 담지
않는다([콘텐츠 저작 규칙](../../agents/content-authoring.md) 「남은 ScriptableObject 셋」).

**완료 조건.** `Tools/lint-allow.txt`의 상태 아이콘 R3 예외가 사라지고 제품 Unity 경로에
문자열 `Resources.Load` 호출이 남지 않는다.

### 14.2 P2급 — playtest 시절 이름이 실 코드에 남아 있다

**2026-10-14 완료.** 마지막 `PlaytestKoreanText`를 지워 `Playtest` 접두 클래스가 없고, 표시 이름의
원본은 번역 CSV 하나다. 아래는 기록이다.

**문제.** 덱 플레이테스트 씬 시절(`1adfb23`)의 이름이 정식 코드에 그대로 남아, 클래스 이름만 보면
테스트 도구로 오해된다. 2026-09-04 점검(367개 타입)에서 셋이 나왔고 둘은 닫혔다.

- `PlaytestCardArt` → `CardStatusIconSprites`로 개명 (2026-09-04 완료)
- ~~`PlaytestSession`(94줄)·`MultiTurnPlaytestSession`(133줄)~~ **2026-09-04 삭제.** 개명이 아니라
  제거였다 — 요약은 "used by the Unity playtest screen"이라 말했지만 그 화면이 없었고, 자기
  테스트(103줄) 말고 부르는 곳이 없었다. 전투 화면은 `DeckCombatSession`, CLI 비교 하니스는
  `ScenarioRunner`·`MultiTurnRunner`를 쓴다. **죽은 코드를 가려 준 것이 잘못된 이름이었다** —
  이름이 "playtest"라 아무도 이상하게 여기지 않았다.
- ~~`PlaytestKoreanText`~~ **2026-10-14 삭제.** 현지화 전환으로 카드·캐릭터·적 이름의 표시
  원본이 번역 CSV(`Localization/Csv/Content.csv`)로 옮겨 가 마지막 호출처(옛 카드 표현 팩토리)와
  함께 지웠다.

`PlaytestKoreanText`는 이름 문제만이 아니다. 카드·적·시나리오의 한글 이름을 `switch`로 코드에 박고
있는데(`BattleScreenController`·`CardPresentation` 등 7곳에서 호출), 카드 원본은 이미
`Assets/StreamingAssets/Content`의 JSON이다(규칙 5). **이름의 단일 원본이 둘로 갈려 있는 것**이므로,
개명만 하지 말고 JSON 쪽으로 흡수할 수 있는지 함께 본다.

**완료 조건.** `Playtest` 접두가 붙은 실 코드 클래스가 없다. 한글 표시 이름의 원본이 하나다.

### 14.3 P2급 — `Sample` 접두가 예제가 아닌 것에 붙어 있다

**문제.** `SampleScenarios`(197줄)와 `SampleMultiTurnScenarios`(290줄)는 이름이 예제를 뜻하지만,
실제로는 `ScenarioCliReport`가 시나리오를 찾는 **유일한 카탈로그**다(`ScenarioCliReport.cs:9`·`16`).
지우거나 건너뛰어도 되는 예제처럼 보이는 것이 문제다. `ScenarioCatalog`·`MultiTurnScenarioCatalog`
정도가 역할에 맞다.

오해의 폭이 §14.2보다 작아 우선순위는 낮다. 시나리오 카탈로그를 손댈 일이 생길 때 함께 고친다.

**완료 조건.** 이름이 "예제 모음"이 아니라 "카탈로그"임을 말한다.

## 15. 상태 수치의 수명별 층과 저작값 읽는 통로 (2026-08-28 조사, 2026-10-06 색인에서 옮김)

문제가 둘이고 **같은 작업에서 풀린다.** 따로 착수하면 두 번 뜯게 된다. §13.1의 count/magnitude 이름
정리도 이 작업에 속한다.

**2026-10-08 — 이 절의 작업은 [카드·상태 변경 계획](2026-10-07-card-status-mutation-roadmap.md)이 이어받았다.** 수치 이름과 저장 구조는 큰 계획 1
(대기열 F10-1), 기간별 보관과 저작값 조회는 큰 계획 2(F10-2)다. 아래는 문제의 배경이며, 해법으로 적힌
부분은 그 계획의 해당 단계에서 다시 검토한다.

### 15.1 수명이 맞는 그릇이 없다

변경에는 살아야 하는 기간이 있다("이번 전투 동안 취약 200%", "이번 런 동안 독 성장량 +2"). 변경을 그
기간만큼 사는 객체에 담으면 지우는 코드 없이 알아서 사라진다. 지금 상태 수치가 담기는 곳은 기간이
맞지 않는다.

| 변경이 살아야 할 기간 | 맞는 그릇 | 현재 (2026-10-06 확인) |
|---|---|---|
| 이 전투 | `CombatState` | 전투마다 새로 만들어지지만 **자기 수치 표가 없다** — `StatusRules`가 카탈로그 것을 그대로 돌려준다(`Assets/Core/Combat/CombatState.cs:63`) |
| 이 런 | `RunState` | 전투 노드 흐름이 쓰고 있으나(`CombatNode`·`RunSetup`·`CombatNodeFlow`) **상태 수치를 담는 자리는 없다** |
| 안 변함 | `GameContent` | 있다. 지금은 전부 여기 쓴다 |

`GameContent`와 그 안의 `StatusContentCatalog`는 `CombatNodeFlow.Start()`에서 한 번 만들어지고
(`Assets/Unity/Scripts/Battle/CombatNodeFlow.cs:51`), 런을 재시작하거나 다음 노드로 넘어가도 같은
인스턴스를 쓴다. 그래서 `StatusContentCatalog.Rules`(`Assets/Core/Authoring/Statuses/StatusContentCatalog.cs:43`)에
쓴 값은 전투 경계를 넘어 남는다.

**가변인 것 자체는 결함이 아니다** — 유물 같은 효과가 수치를 바꾸게 하려는 의도다. 결함은 그 변경이
**지워질 시점이 없다**는 것이다. 아직 피해가 없는 이유는 로딩 때 카탈로그가 저작값을 채우는 한 곳
(`StatusContentCatalog.cs:36`) 말고는 프로덕션에서 `Rules.Set`을 부르는 코드가 없어서이고, 이 경고는 `CombatState.cs:45` 주석에도 있다. 테스트는 이미 이 누수를 밟아
`TestContent.Statuses()`가 호출마다 카탈로그를 새로 만든다(`Assets/Core/Tests/EditMode/TestContent.cs:41`).
`StatusRules`를 읽는 곳은 `CardExecutor.cs:111`과 `DeckCombatSession.cs:240`·`459`다.

**전투용 사본을 뜨는 방식은 안 된다** —
[카드 변형 설계](../specs/2026-07-30-card-mutation-and-runtime-content-design.md) §4.3이 이미 기각했다
(전투 중 발생한 런 지속 변경이 사본과 함께 사라진다). 그 설계가 카드에 대해 정해 둔
`Source + Permanent(런) + Combat(전투) → Effective`가 해법 후보이며(상태 수치에 그대로 쓸지는 카드·상태 변경
계획 단계 2.1에서 정한다), **카드에만 문서화돼 있고 구현은 없다**
(`CardMutation` 타입이 저장소에 없고 `OwnedCard`는 `Def`+`OwnerId`뿐이다, `Assets/Core/Cards/OwnedCard.cs:7`).
상태 수치·덱·캐릭터는 문서조차 없다.

### 15.2 저작값 읽는 통로가 상태마다 다르고 중앙에 쌓인다

상태의 런타임 행동(`StatusBehavior`)과 저작 스펙(`StatusSpec`)은 규칙 9대로 갈려 있어, 상태를 추가해도
클래스 하나와 등록 한 줄이면 된다. 그런데 그 저작값을 **읽는** 쪽은 `StatusContentCatalog`에 상태별
메서드로 쌓인다.

| 통로 | 담는 것 | 해당 상태 | 가변? |
|---|---|---|---|
| `Rules` (`StatusRuleSet`) | `multiplierPercent` | 취약·약화·손상 | 가변 |
| `GrowthPerTurnOf` (`StatusContentCatalog.cs:65`) | `growthPerTurn` | 독 | 읽기 전용 |
| `ExecutionOrderDeltaOf` (`StatusContentCatalog.cs:62`) | `executionOrderDelta` | 가속·둔화 | 읽기 전용 |
| 없음 | — | 방어·전염·독 잠복·독 안정·보상 무효 | — |

파라미터를 가진 상태를 추가할 때마다 메서드가 하나 늘고, 셋 중 하나만 가변이라 "유물이 독 성장량 +1"은
손댈 통로 자체가 없다. 원인은 방향이다. 행동은 `NewSpec()`으로 자기 스펙 타입을 아는데, 읽을 때
카탈로그를 거치느라 그 지식이 버려지고 카탈로그가 대신 알게 됐다. **카탈로그가 `StatusKey → StatusSpec`만
돌려주고 해석은 각 행동이 하면** 중앙이 자라지 않고, 수명별 층을 얹을 자리도 한 곳으로 모인다.

기본 캐릭터 설계(2026-10-06 진행 중)가 요새화·힘 같은 "이번 전투 동안" 상태를 더하려 하므로, 이 절은
그 구현 전에 다시 본다.

### 15.3 착수 전에 정할 것

**2026-10-08 현황.** 2번은 정해졌다 — 범위를 카드에 한정하지 않는다. 카드·상태 변경 계획이 상태 수치
(큰 계획 1), 중앙 상태 규칙(큰 계획 2), 카드(큰 계획 3)를 모두 다룬다. 1번은 아직 열려 있고 그 계획의
단계 2.1(상태)과 3.1(카드)에서 검토한다. 덱·캐릭터는 그 계획의 범위에 없다.

1. §4.3의 `Source + Permanent(런) + Combat(전투) → Effective` 모델을 상태 수치·덱·캐릭터에도
   **그대로 적용**할 것인가, 데이터 종류마다 다른 모양이 필요한가.
2. 계획 4(카드 변형)의 범위를 카드에 한정할 것인가, 층 전반으로 넓힐 것인가.
