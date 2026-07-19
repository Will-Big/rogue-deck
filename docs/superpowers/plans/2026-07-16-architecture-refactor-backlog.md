# Fate Weaver — 확장성·하드코딩 후속 리팩토링 백로그

- 작성일: 2026-07-16
- 상태: 후속 설계/구현 대기
- 현재 범위에서 분리된 작업: 저작 구조, 효과 기반 카드 성질 합성, 대상 선택 메타데이터, RNG 통합,
  SO 단일 원본화, Unity 프리팹화

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

- 상태: **완료 (2026-07-18)** — 구현 기록: [`2026-07-18-p0a-rng-unification.md`](2026-07-18-p0a-rng-unification.md)

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

- 상태: **완료 (2026-07-19)** — 구현 기록: [`2026-07-19-p0b-implementation-record.md`](2026-07-19-p0b-implementation-record.md)
- 최종 검증: SO 재생성 diff 없음, 헤드리스 307/307, Unity EditMode 356/356, 사용자 Play 확인

### 문제

`EffectKind`, `StatusKindRef`, `InterventionKind` enum과 `CardSpecMapper` 중앙 switch가 코어의 타입 안전 키 기반 열린
확장을 다시 닫힌 집합으로 만든다. `EffectData`도 ApplyStatus 전용 필드를 직접 포함해 새로운 파라미터 형태가
추가될 때 공용 모델이 계속 커진다.

### 목표 구조

- 새 효과 저작 타입은 기존 enum/switch를 수정하지 않고 추가
- Unity 저작 데이터와 순수 C# export가 같은 스키마를 사용
- 효과별 파라미터는 이름과 타입이 보존되며 핸들러가 자기 파라미터를 검증
- 생성 C# 또는 다른 헤드리스 export는 SO에서만 생성
- 잘못된 키·필드·파라미터는 에디터/부팅 검증에서 실패
- 조건은 설계대로 닫힌 조합형을 유지하되 효과/상태/개입 액션은 열린 확장으로 유지

### 설계 시 비교할 대안

1. Unity `[SerializeReference]` 기반 효과별 authoring spec + 순수 DTO export
2. 타입 안전 키 + 검증된 이름 있는 parameter bag
3. 효과 정의 ScriptableObject + 순수 생성 코드 descriptor

리플렉션 자동 등록이나 raw string dictionary만으로 타입 안전성을 포기하는 방식은 채택하지 않는다.

### 완료 조건

- 샘플 신규 효과를 추가할 때 중앙 enum/mapper 수정 없음
- 신규 효과의 실행·설명·저작·검증 경로가 클래스/등록 단위로 국소화됨
- 기존 시작덱·적덱·파티 검증 카드의 export 등가성 유지
- 생성 파일과 런타임 SO가 동일 `CardDefinition`을 생성

## 4.1 P0-B2 — `CardType` 제거와 효과 기반 카드 성질 합성

- 상태: **후속 설계/구현 대기**
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
- SO의 직렬화된 `Type` 필드와 `GeneratedCards.cs`를 함께 갱신하고 재생성 diff를 검증
- 새 외부 패키지, 리플렉션 자동 등록, raw string 효과 판정은 도입하지 않음

### 완료 조건

- 제품·저작·생성·테스트 코드에서 `CardType` 참조가 없음
- `CardAsset`과 생성된 `CardSpec`에 `Type` 필드가 없고 SO 재생성 후 diff가 없음
- `Damage`만 가진 카드와 `Damage + Block` 복합 카드가 모두 피해 카드 조건을 만족
- `Block`만 가진 카드는 피해 카드 조건을 만족하지 않음
- 이전/인접 카드 조건과 다음 피해 카드 강화가 타입 대신 효과 구성으로 동작하는 헤드리스 회귀 테스트 통과
- 기존 카드 콘텐츠의 실행 순서·효과·설명 결과에서 의도하지 않은 변화가 없음
- 전체 헤드리스 테스트와 Unity EditMode 테스트 통과, 머지 후 사용자 Play 검증

## 5. P0-C — 대상 선택 메타데이터와 입력 흐름 일반화

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

### 문제

현재는 코드 정의로 SO를 seed하고, SO에서 다시 C#을 생성하며, 일부 적 카드 규칙은 코드가 원본이다. 카드 이름과
아트는 별도의 ID switch/`Resources.Load` fallback에도 중복되어 있다.

### 목표 구조

- 제품 카드·적·덱·캐릭터 데이터의 진실의 원천은 SO
- 헤드리스 시뮬레이션은 SO에서 생성된 순수 export만 소비
- `StarterDeck`, `GoblinDeck`, `WardenDeck`, 파티 검증 데이터의 제품용 코드 상수 제거 또는 테스트 fixture로 격리
- 카드 표시 이름과 아트는 `CardAsset` 참조에서 공급
- ID→이름/경로 switch와 magic resource path 제거
- 생성물에 원본 asset ID/hash를 기록해 stale export 검증

### 선행 조건

- P0-B 열린 저작 구조 완료
- P0-B2 효과 기반 카드 성질 합성 완료
- P0-C 대상 선택 메타데이터와 입력 흐름 일반화 완료
- 설명 레지스트리와 콘텐츠 등록 검증 완료

### 완료 조건

- 제품 카드 수치 변경은 SO 한 곳만 수정
- export 미생성/불일치가 CI 또는 에디터 검증에서 실패
- 런타임 제품 경로에서 코드 카드 카탈로그 fallback 없음
- `PlaytestCardArt`와 카드 ID 기반 `PlaytestKoreanText.CardName` 제거 또는 fixture 전용 격리

## 7. P1-B — Unity 프리팹·직렬화 참조 구조로 전환

### 문제

`BattleUiKit`, `UnitView`, `RailCardView`, `PileView`, `ExecutionRailView`가 런타임에 GameObject와 컴포넌트를
조립한다. 폰트와 아이콘은 `Resources.Load` 경로에 의존하며 `CardAsset`/`DeckAsset`은 public 직렬화 필드를
사용한다.

### 목표 구조

- 재사용 UI 요소를 prefab asset으로 저장
- 컨트롤러는 `[SerializeField] private` prefab/reference를 인스펙터에서 주입받음
- 런타임은 prefab 인스턴스 생성과 데이터 바인딩만 수행
- 폰트, 아이콘, 아트는 직렬화된 asset reference로 공급
- 에디터 builder는 필요하면 prefab/scene을 생성하는 마이그레이션 도구로만 유지
- `CardAsset`, `DeckAsset` 필드를 `[SerializeField] private` + 읽기 전용 프로퍼티로 캡슐화
- 사용되지 않는 `CardAsset.Description` 제거

### 선행 조건

- P1-A SO 단일 원본화의 asset ownership 확정

### 완료 조건

- 제품 런타임 경로에 `new GameObject`, `AddComponent`, 문자열 `Resources.Load` 없음
- 주요 UI prefab 누락 시 명확한 부팅/검증 오류
- 기존 전투 화면의 카드·유닛·레일·더미 UI Play 검증 통과
- Unity 직렬화 마이그레이션에서 기존 asset 데이터 보존

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
- `TargetSelector`, `StatusLifetimeKind`, `StatusApplyTarget`: 닫힌 값 집합의 문법/변환 분기 유지
- `Outcome`, `ConditionTier`: 표현용 닫힌 enum 분기 유지

`CardType`은 복합 효과 카드에서 열린 조합 축임이 확인되어 P0-B2에서 제거한다. 나머지 항목이 실제로 열린 콘텐츠
확장 축으로 바뀔 때만 별도 설계를 거쳐 레지스트리화한다.

## 11. 작업 분리 원칙

- 각 P0/P1 항목은 별도 brainstorming/spec/implementation plan을 가진다.
- 한 변경에서 두 개 이상의 대규모 데이터 마이그레이션을 섞지 않는다.
- 각 작업은 기존 콘텐츠의 동작 등가성 테스트를 먼저 확보한다.
- Unity asset 변경은 순수 headless 검증과 사용자 Play 검증을 분리해 기록한다.
- 외부 패키지나 reflection 기반 자동 등록은 사전 승인 없이 도입하지 않는다.
