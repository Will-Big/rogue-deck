# P0-B2 — `CardType` 제거와 효과 기반 카드 성질 합성 설계

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

- 작성일: 2026-07-19
- 상태: 설계 승인
- 원 백로그: [`2026-07-16-architecture-refactor-backlog.md`](../../plans/2026-07-16-architecture-refactor-backlog.md) §4.1
- 선행 완료: P0-B 열린 카드 저작 구조 (2026-07-19)

## 1. 목적

카드의 성질을 `Attack`, `Skill`, `Defense` 중 하나로 고정하는 `CardType`을 제거한다. 피해 카드 여부는
정의에 포함된 `EffectKeys.Damage` 효과로 판정하여 `Damage + ApplyStatus(Block)` 같은 복합 효과 카드도
피해 관련 조건과 강화의 대상이 되게 한다.

실행 카드와 조작 카드의 플레이 경로 구분은 기존 `CardCategory.Execution`과
`CardCategory.Intervention`만 담당한다. 이번 작업은 P1-A SO 단일 원본화 또는 P0-C 대상 선택 메타데이터와
섞지 않는 독립 스키마 마이그레이션이다.

## 2. 현황과 문제

- `CardDefinition`, `CardSpec`, `ZoneCardSpec`, `CardAsset`, 생성 코드가 모두 단일 `CardType`을 요구한다.
- `AdjacentCardIs`, `PreviousExecutedCardIs`, `BeforeNextEnemyAttack`이 `CardType.Attack`을 직접 비교한다.
- `GrantNextPlayerAttackDamageBonusHandler`도 다음 플레이어 카드의 `CardType.Attack` 여부를 비교한다.
- 저작 조건 enum과 한국어 설명이 `Attack`이라는 분류 이름을 노출한다.
- 조작 카드는 실행 효과가 없는데도 `CardType.Skill`을 강제로 가진다.
- Unity CardAsset에 직렬화된 `Type` 필드가 남아 있어 코드 필드만 지우면 SO 스키마와 생성 결과가 어긋난다.

단일 타입을 플래그 enum이나 태그 집합으로 바꾸면 복합 성질은 표현할 수 있지만, 효과 데이터와 별도의 중복
원본이 생긴다. 같은 효과 구성이 태그 누락에 따라 다르게 판정되는 문제도 남는다.

## 3. 검토한 접근법

### 3.1 채택: 범용 코어 질의 + 구체적인 저작 조건

코어는 `EffectKey`를 받는 범용 질의와 조건을 제공하고, 현재 저작 enum은 Damage 전용 이름을 유지한다.
효과 기반 규칙을 중복 없이 구현하면서도 인스펙터의 닫힌 조건 문법과 명확한 표시를 보존한다.

### 3.2 미채택: 저작 조건도 임의 EffectKey로 일반화

`ConditionSpec`에 `EffectKeyRef`를 두면 새 효과 조건을 데이터만으로 만들 수 있다. 그러나 조건을 작고 닫힌
조합형으로 유지한다는 기존 결정과 맞지 않고, 키별 의미·설명·유효성 검증을 인스펙터에 추가해야 한다. 실제
요구가 없는 범용성을 미리 도입하므로 채택하지 않는다.

### 3.3 미채택: 코어 조건도 Damage 전용 타입으로 제한

구현은 가장 단순하지만 향후 효과 존재 조건마다 `AdjacentDamageCard`, `AdjacentBlockCard` 같은 코어 타입과
평가 분기가 늘어난다. 공통 질의를 호출부마다 다시 구현할 가능성이 있어 채택하지 않는다.

## 4. 핵심 결정

### 4.1 효과 존재 판정

`CardDefinition`에 다음 타입 안전 질의를 둔다.

```csharp
public bool HasEffect(EffectKey key)
```

이 메서드만 카드의 정의상 효과 보유 여부를 판정한다. `Effects`를 호출부마다 직접 순회하거나
`EffectKey.Id` 문자열을 비교하지 않는다. 현재 의미는 최상위 `EffectData.Key`의 존재 여부이며, 조건부 효과도
정의에 포함되어 있으면 보유한 것으로 본다.

`HasEffect(EffectKeys.Damage)`는 카드가 실제 실행 중 0보다 큰 피해를 주었음을 뜻하지 않는다. 대상 부재,
조건 결과, 방어, 취소와 무관하게 카드 정의에 Damage 효과가 포함됐다는 뜻이다.

### 4.2 조건 모델

진영만 보는 조건과 특정 효과까지 보는 조건을 별도 타입으로 분리한다.

```csharp
public sealed record AdjacentCardIs(AdjacentDirection Direction, Side Side) : Condition;
public sealed record AdjacentCardHasEffect(
    AdjacentDirection Direction,
    Side Side,
    EffectKey EffectKey) : Condition;

public sealed record PreviousExecutedCardIs(Side Side) : Condition;
public sealed record PreviousExecutedCardHasEffect(
    Side Side,
    EffectKey EffectKey) : Condition;
```

- 인접 조건은 아직 실행 결과를 알 수 없는 미래 영역 정의를 검사한다.
- 이전 실행 조건은 취소된 카드를 건너뛰는 기존 `ResolutionContext.LastExecutedCard` 의미를 유지하되, 해당
  카드 정의의 효과 구성을 검사한다.
- 기존 `BeforeNextEnemyAttack`은 `BeforeNextEnemyDamageCard`로 이름을 바꾸고 앞선 적 카드 중
  `HasEffect(EffectKeys.Damage)`인 카드가 있는지 검사한다.
- 실제 실행 결과를 검사하는 조건은 이번 범위에 소비자가 없으므로 추가하지 않는다. 필요할 때
  `CardResolved.DamageDealt` 또는 동등한 실행 결과를 입력으로 받는 별도 조건으로 설계하며, 정의 기반
  `DamageCard`와 이름을 명확히 구분한다.

`ConditionEvaluator`의 닫힌 중앙 분기는 기존 설계대로 유지한다. 새 효과 핸들러를 등록하는 열린 확장 축과
조건 문법의 닫힌 조합 축을 혼동하지 않는다.

### 4.3 저작 조건과 설명

`ConditionKind`의 공격 관련 멤버를 정의 기반 의미가 드러나게 변경한다.

| 기존 | 변경 |
|---|---|
| `BeforeNextEnemyAttack` | `BeforeNextEnemyDamageCard` |
| `PrevExecutedIsPlayerAttack` | `PrevExecutedIsPlayerDamageCard` |
| `PrevExecutedIsEnemyAttack` | `PrevExecutedIsEnemyDamageCard` |
| `NextIsEnemyAttack` | `NextIsEnemyDamageCard` |

각 멤버는 `EffectKeys.Damage`를 전달하는 범용 코어 조건으로 변환한다. 현재 enum 순서는 유지하여 Unity에
직렬화된 정수 값의 의미를 보존하고, CardAsset 재생성으로 이름과 생성 코드를 동기화한다.

한국어 조건 설명은 `공격` 대신 정의 기반 의미인 `피해 카드`를 사용한다. 예를 들어
`PrevExecutedIsEnemyDamageCard`는 `직전에 실행한 카드가 적 피해 카드이면`으로 합성한다.

### 4.4 다음 피해 카드 강화

공격 타입을 전제로 한 효과 이름을 정의 기반 의미에 맞게 다음과 같이 변경한다.

- `EffectKeys.GrantNextPlayerAttackDamageBonus` → `EffectKeys.GrantNextPlayerDamageCardBonus`
- 문자열 키 `grant_next_player_attack_damage_bonus` → `grant_next_player_damage_card_bonus`
- `GrantNextPlayerAttackDamageBonusHandler` → `GrantNextPlayerDamageCardBonusHandler`
- `GrantNextAttackBonusSpec` → `GrantNextDamageCardBonusSpec`
- 대응 설명 핸들러·등록·테스트 이름도 동일한 용어로 변경

핸들러는 현재 카드 뒤의 첫 플레이어 카드 중 `HasEffect(EffectKeys.Damage)`를 만족하는 카드에 pending damage
bonus를 추가한다. Block만 가진 카드는 건너뛰고, Damage와 Block을 함께 가진 카드는 대상이 된다. 설명은
`다음 플레이어 피해 카드가 주는 피해 +N`처럼 정의 기반 선택임을 드러낸다.

키 문자열 변경은 제품의 저장 세이브 호환성 요구가 아직 없고 SO와 생성 코드를 같은 변경에서 마이그레이션하므로
별칭을 남기지 않는다. 미등록된 이전 키는 검증에서 실패하여 stale 데이터를 조용히 허용하지 않는다.

## 5. 스키마 마이그레이션

다음 필드와 타입을 제거한다.

- `CardType` enum 파일과 Unity `.meta`
- `CardDefinition.Type`
- `CardSpec.Type`
- `ZoneCardSpec.Type`
- `CardAsset.Type`
- 코드 생성기의 `Type = CardType.*` 출력과 seed/apply 복사
- 제품 코드와 테스트 fixture 생성자의 `CardType` 인자
- 모든 CardAsset YAML의 직렬화된 `Type` 줄

현재 콘텐츠 기준선은 기존 golden/등가 서명에서 `CardType` 항목만 제거한 값으로 먼저 RED 테스트에 고정한다.
카드 ID, 이름, 진영, 카테고리, 비용, 실행 순서, 효과 순서와 파라미터, 설명은 의도하지 않게 바꾸지 않는다.

SO 마이그레이션은 다음 순서로 검증한다.

1. `CardAsset.Type`과 기존 YAML `Type` 필드를 제거한다.
2. Unity batchmode에서 기존 카드 seed/생성 경로를 실행한다.
3. 체크인된 `GeneratedCards.cs`가 새 스키마로 재생성되는지 확인한다.
4. 같은 생성 명령을 한 번 더 실행한 뒤 git diff가 추가로 생기지 않는지 확인한다.
5. 예상하지 않은 Scene, Prefab 또는 설정 변경은 스테이징하지 않는다.

## 6. 데이터 흐름

```text
CardAsset (Type 없음)
    -> CardSpec (Type 없음)
    -> CardSpecMapper
    -> CardDefinition (Type 없음, Effects 보유)
       -> HasEffect(EffectKeys.Damage)
          |-- ConditionEvaluator
          `-- GrantNextPlayerDamageCardBonusHandler
```

실행/조작 분기는 계속 `CardCategory`만 읽는다. UI와 전투 코어가 카드 성질을 별도로 추론하거나 캐시하지 않는다.

## 7. 실패 처리와 불변식

- `HasEffect`에 전달된 `EffectKey`가 비어 있으면 잘못된 규칙 코드로 보고 명확한 예외를 던진다.
- `CardDefinition.Effects`의 null 허용 여부는 기존 생성 계약을 유지한다. 제품 경로는 빈 배열을 사용한다.
- Damage 효과의 payload나 수치가 잘못된 경우는 기존 효과/저작 검증이 담당한다. 효과 존재 질의는 실행 가능성
  검증을 중복하지 않는다.
- 알 수 없는 이전 효과 키에 대한 호환 fallback이나 raw string 비교를 추가하지 않는다.
- 모든 무작위·타임라인·대상 선택 동작은 변경하지 않는다.

## 8. TDD 및 검증 전략

### 8.1 RED 기준선

1. `CardDefinition.HasEffect`가 Damage 단일 카드와 Damage+Block 복합 카드에서 true, Block 단일 카드에서
   false임을 검증한다.
2. `AdjacentCardHasEffect`가 복합 피해 카드를 만족시키고 Block 전용 카드를 거부하는 테스트를 추가한다.
3. `PreviousExecutedCardHasEffect`가 취소된 카드를 건너뛰는 기존 의미를 유지하면서 복합 피해 카드를
   만족시키는 테스트를 추가한다.
4. `BeforeNextEnemyDamageCard`가 카드 타입 대신 Damage 효과 구성으로 판정하는 테스트를 추가한다.
5. 다음 피해 카드 강화가 Block 전용 카드를 건너뛰고 Damage+Block 카드에 보너스를 주는 테스트를 추가한다.
6. 콘텐츠 golden/등가 서명에서 Type만 제거한 예상값을 먼저 고정한다.

각 RED 테스트는 해당 새 API 또는 새 이름이 없어 예상한 컴파일 실패/테스트 실패를 보인 뒤 최소 구현으로
GREEN을 만든다. 대규모 생성자 마이그레이션은 첫 코어 RED/GREEN 뒤 기계적으로 수행하고 전체 회귀를 복구한다.

### 8.2 전체 검증

- 제품·저작·생성·테스트 코드의 `CardType` 검색 결과 없음 (`Assets/**/*.cs`, CardAsset YAML)
- 생성 코드 재생성 후 추가 diff 없음
- 전체 헤드리스 `dotnet test` 통과
- Unity batchmode EditMode 전체 테스트 통과
- 동일 시나리오의 실행 순서, 효과, 설명과 결정적 타임라인 회귀 통과
- 작업 후 `git status`에서 예상한 파일만 변경됨
- 머지 후 메인 체크아웃에서 사용자 Play 검증

## 9. 완료 조건

- [ ] 제품·저작·생성·테스트 코드에서 `CardType` 참조가 없다.
- [ ] `CardAsset`, `CardSpec`, `CardDefinition`, `ZoneCardSpec`에 `Type` 필드가 없다.
- [ ] SO 재생성 후 추가 diff가 없다.
- [ ] Damage 단일 카드와 Damage+Block 복합 카드가 피해 카드 조건을 만족한다.
- [ ] Block 단일 카드는 피해 카드 조건을 만족하지 않는다.
- [ ] 이전/인접/선행 조건과 다음 피해 카드 강화가 효과 구성으로 동작한다.
- [ ] 기존 카드 콘텐츠의 실행 순서·효과·설명에 의도하지 않은 변화가 없다.
- [ ] 전체 헤드리스와 Unity EditMode 테스트가 통과한다.
- [ ] 머지 후 사용자가 Play 검증을 완료한다.

## 10. 범위 밖

- 임의 카드 태그 또는 capability 레지스트리
- 실제 피해량(`CardResolved.DamageDealt`) 기반 조건
- 간접 피해를 Damage 카드로 간주하는 규칙
- P0-C 대상 선택 메타데이터와 Unity 입력 흐름 일반화
- P1-A ScriptableObject 단일 원본화
- 저장 세이브나 외부 데이터에 대한 이전 효과 키 호환 계층
