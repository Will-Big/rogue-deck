# Fate Weaver — 카드 설명 핸들러 레지스트리 설계

- 작성일: 2026-07-16
- 개정일: 2026-07-27
- 문서 유형: `architecture`
- 주 도메인: `card-description`
- 상태: `current` — 구현된 카드 설명 확장 구조
- 선행 설계: [기존 동적 카드 설명 설계](../archive/specs/2026-06-26-card-descriptions-design.md)
- 후속 표현 규칙: [위치 대상과 카드 텍스트 설계](2026-07-27-position-targeting-card-text-design.md)

## 1. 배경

현재 카드 설명은 `DescriptionComposer`가 `EffectKey`를 직접 비교하고,
`KoreanDescriptionVocabulary`가 상태·개입 액션 키를 다시 분기해 만든다. 이 구조에서는 새로운 효과나
개입 액션을 추가할 때 실행 핸들러뿐 아니라 중앙 설명 코드와 vocabulary 인터페이스까지 함께 수정해야 한다.

실제 누락 사례로 `EffectKeys.MoveFormation`은 실행 핸들러와 저작 매핑은 있지만 설명 분기가 없어
`[검증] 대형 이동` 카드에 의미 있는 설명이 표시되지 않는다. 알 수 없는 키가 빈 문자열로 처리되므로
누락도 조기에 발견되지 않는다.

## 2. 목표

1. 새 효과 설명은 **설명 핸들러 클래스 1개 + 키 등록**으로 확장한다.
2. 새 개입 액션 설명도 같은 패턴으로 확장한다.
3. 새 상태 이름은 상태 설명 등록 1개로 확장한다.
4. `DescriptionComposer`에서 효과·개입·상태별 중앙 분기를 제거한다.
5. 기존 효과를 조합한 새 카드는 설명 코드를 추가하지 않는다.
6. 설명은 계속 `EffectData`와 `InterventionActionData`에서 자동 생성한다. 저작 데이터에 설명 문자열 필드를 두지 않는다.
7. 미등록·중복 등록을 조기에 실패시킨다.
8. 모든 로직은 순수 C# Simulation 레이어에 두고 헤드리스 테스트로 검증한다.

## 3. 비목표

- 다국어 및 외부 로컬라이제이션 테이블
- `EffectSpec`/`CardSpecMapper` 저작 구조 변경
- 카드 플레이 대상 선택 메타데이터
- 전투 RNG 통합
- ScriptableObject 단일 원본화
- Unity UI 프리팹화

위 항목은 [확장성·하드코딩 후속 리팩토링 백로그](../plans/2026-07-16-architecture-refactor-backlog.md)에
별도 후속 작업으로 기록한다.

## 4. 핵심 결정

### 4.1 실행 핸들러와 설명 핸들러를 분리한다

실행 핸들러는 `FateWeaver.Core`에 남고 한국어 표현을 알지 않는다. 설명 핸들러는
`FateWeaver.Simulation.Descriptions`에 두어 코어 규칙과 표현 의존성을 분리한다.

실행 핸들러에 `Describe()`를 추가하는 방식은 등록 수는 줄지만 순수 코어가 한국어 문구를 소유하게 되므로
채택하지 않는다.

### 4.2 Composer는 문장 조립만 담당한다

`DescriptionComposer`의 책임은 다음으로 제한한다.

- 카드 카테고리에 따라 효과 또는 개입 액션 설명 경로 선택
- 여러 효과 문장 결합
- 기본값과 조건 성공값 문장 조립
- 마침표와 문장 사이 공백 처리

효과의 의미와 문구는 Composer가 알지 않는다.

### 4.3 한국어 전용으로 시작하되 공통 문법을 분리한다

이번 범위는 한국어만 지원한다. 효과 핸들러가 한국어 의미 문구를 소유하고, 대상 선택·조건절·상태 지속시간처럼
여러 핸들러가 공유하는 닫힌 문법은 `KoreanDescriptionGrammar`가 제공한다.

새 효과를 추가할 때 공통 문법이 아닌 효과별 메서드를 grammar 인터페이스에 추가하지 않는다. 따라서 기존
`IDescriptionVocabulary.Damage()`, `NullifyNextReward()` 같은 효과별 API는 제거 대상이다.

## 5. 구조

```text
CardDefinition
    |
    v
DescriptionComposer
    |-- execution ----> EffectDescriptionRegistry.Resolve(EffectKey)
    |                       |
    |                       +--> IEffectDescriptionHandler
    |
    +-- intervention -> InterventionDescriptionRegistry.Resolve(InterventionActionKey)
                            |
                            +--> IInterventionDescriptionHandler

ApplyStatusDescriptionHandler
    |
    +--> StatusDescriptionRegistry.Resolve(StatusKey)

모든 핸들러
    |
    +--> KoreanDescriptionGrammar (대상·조건·지속시간 공통 문법)
```

세 레지스트리와 공통 문법은 `KoreanDescriptionCatalog`가 묶는다. Unity의 `CardPresentation`은 기본 카탈로그를
주입받아 설명을 생성한다. 정적 전역 조회를 각 UI 클래스에 흩뿌리지 않고 composition root에서 동일 인스턴스를
공유한다.

## 6. 주요 인터페이스

아래 코드는 책임과 호출 형태를 설명하기 위한 설계 스케치다. 실제 구현 시 프로젝트의 C# 9 제약과 기존
네이밍을 따른다.

```csharp
public interface IEffectDescriptionHandler
{
    EffectKey Key { get; }
    string Describe(EffectData effect, int effectValue, DescriptionContext context);
}

public interface IInterventionDescriptionHandler
{
    InterventionActionKey Key { get; }
    string DisplayName { get; }
    string Describe(InterventionActionData action, DescriptionContext context);
}
```

`DescriptionContext`는 다음 공유 기능만 제공한다.

- `TargetPrefix(EffectData)`
- `Condition(Condition)`
- `StatusName(StatusKey)`
- `StatusTarget(StatusApplyTarget)`
- `LifetimeSuffix(StatusLifetime)`

`TargetPrefix`와 `StatusTarget`은 현재 구현된 설명 조합 경계다. 위치 대상 UI로 이행한 뒤에는 유닛의
진영과 위치 범위를 본문 접두사로 만들지 않는다. 설명 핸들러는 능력 문구만 만들고, 진영 문양과 위치
범위는 [위치 대상과 카드 텍스트 설계](2026-07-27-position-targeting-card-text-design.md)의 대상 칸과
능력 묶음이 담당한다. 레지스트리 확장 구조와 자동 설명 원칙은 그대로 유지한다.

`EffectDescriptionRegistry`, `InterventionDescriptionRegistry`, `StatusDescriptionRegistry`는 다음 계약을 가진다.

- `Register`는 null 구현, 비어 있는 키, 중복 키를 거부한다.
- `Resolve`는 미등록 키에서 `KeyNotFoundException`을 던진다.
- 등록된 키 목록 또는 `Contains`를 제공해 부팅 검증에 사용한다.
- 등록 후 런타임 카드 해석 중에는 변경하지 않는다.

## 7. 기본 등록

기존 중앙 분기를 다음 구현으로 이동한다.

### 효과 설명 핸들러

- `DamageDescriptionHandler`
- `ApplyStatusDescriptionHandler`
- `NullifyNextPlayerConditionRewardDescriptionHandler`
- `GrantNextPlayerAttackDamageBonusDescriptionHandler`
- `MoveFormationDescriptionHandler`

### 개입 액션 설명 핸들러

- `ChangeExecutionOrderDescriptionHandler`
- `SwapExecutionOrderDescriptionHandler`
- `LockDescriptionHandler`

### 상태 설명 등록

- `Block` → `방어`
- `Slow` → `둔화`
- `Haste` → `가속`
- `Stun` → `기절`
- `Vulnerable` → `취약`
- `RewardNullified` → `조건 보상 무효`

등록 목록은 조건문을 포함하지 않는 composition root다. 새로운 동작을 중앙 관리자가 구현하지 않고, 어떤
기능을 활성화할지만 명시한다.

## 8. 대형 이동 설명

`MoveFormationHandler`의 규칙과 동일하게 `EffectValue`의 부호를 해석한다.

- 음수 `-N`: `소유자를 대형 전방으로 N칸 이동`
- 양수 `+N`: `소유자를 대형 후방으로 N칸 이동`
- 0: `소유자의 대형 위치를 유지`

따라서 검증 카드의 `EffectValue = -1`은 다음과 같이 표시된다.

```text
소유자를 대형 전방으로 1칸 이동.
```

방향 판정은 화면 좌표가 아니라 코어 규칙과 동일한 진영 내부 인덱스 의미를 사용한다.

## 9. 데이터 흐름

1. `CardContentLoader`가 `Content/Cards/*.json`을 `CardSpec`으로 읽는다
   (계획 3b 이전에는 `CardAsset.ToSpec()`이었다).
2. `CardSpecMapper`가 `CardDefinition`과 `EffectData`를 생성한다.
3. `CardPresentation`이 `DescriptionComposer`에 카드 정의와 한국어 설명 카탈로그를 전달한다.
4. Composer가 효과 키로 설명 핸들러를 조회한다.
5. 핸들러는 전달받은 `EffectData`와 실제 기본/성공 수치를 사용해 마침표 없는 조각을 반환한다.
6. Composer가 조건절과 구두점을 붙여 최종 문자열을 만든다.
7. `CardView`는 완성된 설명 문자열만 표시한다.

기존 효과를 조합한 새 카드는 1~2단계 데이터만 달라지며 설명 코드나 등록은 변경하지 않는다.

## 10. 실패 처리와 검증

### 10.1 런타임 실패 원칙

- 알 수 없는 효과·개입·상태 키: 빈 문자열이나 키 문자열로 대체하지 않고 예외
- 설명 핸들러가 null/빈 조각 반환: 잘못된 핸들러 구현으로 간주해 예외
- 상태 적용 효과에 `StatusKey` 또는 `StatusLifetime` 누락: 잘못된 EffectData로 간주해 예외
- 중복 등록: 마지막 등록으로 덮어쓰지 않고 즉시 예외

카드에 `"."`만 표시되는 침묵 실패를 허용하지 않는다.

### 10.2 콘텐츠 부팅 검증

카드 카탈로그를 로드한 뒤 전투 시작 전에 모든 카드를 순회한다.

- 모든 실행 효과 키에 실행 핸들러와 설명 핸들러가 등록됐는가
- `ApplyStatus`가 참조하는 모든 상태 키에 상태 행동과 상태 설명이 등록됐는가
- 모든 개입 액션 키에 실행 핸들러와 설명 핸들러가 등록됐는가
- 카드 카테고리와 `Effects`/`InterventionAction` 조합이 유효한가

현재 범위에서는 설명 카탈로그의 검증 API와 기본 콘텐츠 회귀 테스트를 우선 구현한다. 전체 SO 로딩 부팅
파이프라인 통합은 후속 저작 구조/SO 단일 원본화 작업에서 완성한다.

## 11. 테스트 전략

헤드리스 `dotnet test`로 다음을 검증한다.

1. 각 레지스트리의 등록·조회·중복·미등록 실패
2. 기존 모든 카드 설명의 회귀
3. 조건부 효과가 기본값과 성공값 모두 같은 핸들러를 사용하는지
4. 복수 효과 문장 결합과 구두점
5. 대형 이동의 전방·후방·0 설명
6. `ApplyStatus`의 대상·상태 이름·지속시간 조합
7. 개입 액션 설명과 UI 표시 이름
8. 기본 카드 카탈로그에 설명 누락이 없는지
9. 설명 코드에서 `EffectKeys`/`InterventionActionKeys` 중앙 분기가 제거됐는지 구조 검사

Unity 레이어는 `CardPresentation`이 동일 카탈로그를 사용하고 저작 데이터의 설명 문자열을 읽지 않는지를 EditMode
테스트 또는 사용자 Play 검증으로 확인한다.

## 12. 마이그레이션 순서

1. 레지스트리와 실패 계약 테스트 작성
2. 기존 효과 설명을 개별 핸들러로 이동
3. 개입 액션 설명을 개별 핸들러로 이동
4. 상태 이름을 상태 설명 레지스트리로 이동
5. Composer를 레지스트리 조회 방식으로 교체
6. `CardPresentation`과 `PlaytestKoreanText`의 중복 조회를 공용 카탈로그로 통합
7. `MoveFormationDescriptionHandler`와 회귀 테스트 추가
8. 기본 콘텐츠 등록 누락 검증 추가
9. 전체 헤드리스 테스트와 Unity Play 검증

기존 분기와 신규 레지스트리를 장기간 병행하지 않는다. 마이그레이션 완료 시 중앙 분기와 사용되지 않는
효과별 vocabulary API를 같은 변경에서 제거한다.
