# P0-B — 열린 카드 저작 구조 설계

- 작성일: 2026-07-19
- 문서 유형: `architecture`
- 주 도메인: `card-authoring`
- 상태: `current` — 구현된 열린 카드 효과 저작 구조
- 원 백로그: [`../plans/2026-07-16-architecture-refactor-backlog.md`](../plans/2026-07-16-architecture-refactor-backlog.md) §4
- 선행 완료: P0-A RNG 단일화 (2026-07-18)
- **2026-09-18 후속:** [전투 실행 계약](2026-09-18-combat-execution-contract-design.md) 구현으로 효과별 `TargetSelector`·`StatusApplyTarget`은 런타임과 저작에서
  모두 사라졌다. 위치는 카드의 아군·적 축(`CardDefinition.AllyTarget`·`EnemyTarget`)과 효과의 진영(`EffectData.TargetFaction`)
  하나로 표현한다. 아래 해당 서술은 작성 당시 기준이다.
- **2026-10-17 후속:** 효과·조작 JSON의 판별자(`kind` → `key`), 효과 숫자 필드 이름, 평평한 페이로드 필드는
  [효과 JSON 페이로드 중첩 설계](2026-10-17-effect-json-payload-nesting-design.md)가 대체했다. 아래 해당 서술은 작성 당시 기준이다.

## 1. 목적

새 효과 저작 타입을 **중앙 enum/switch 수정 없이** 추가할 수 있게 저작 층을 연다. 코어 런타임은 이미
열린 구조(`EffectKey` + 핸들러 레지스트리)이므로, 병목인 저작 층(`EffectKind` 등 enum 5종 +
`CardSpecMapper` 중앙 switch)과 공용 모델(`EffectData`의 ApplyStatus 전용 필드)을 같은 원칙으로 정리한다.

## 2. 최초 설계 당시의 문제 (해결된 배경)

- `EffectSpec`은 평면 struct: 모든 효과가 10개 필드를 공유하고, 인스펙터에 무관한 필드가 전부 노출된다.
- `EffectKind`, `StatusKindRef`, `InterventionKind` enum이 닫힌 집합을 강제한다.
- `CardSpecMapper`의 switch가 enum → 코어 키/레코드 변환을 중앙 집중한다.
- `EffectData`가 `StatusKey/StatusLifetime/StatusTarget` 같은 ApplyStatus 전용 필드를 직접 포함한다.
- 새 효과 1개 추가 = enum 수정 + switch 2곳 수정 + 공용 모델 필드 추가 (+핸들러/설명).

## 3. 결정 사항

사용자와 합의된 두 가지 핵심 결정:

1. **저작 구조는 다형 spec 클래스** (백로그 §4 대안 1). 효과별 클래스가 자기 파라미터·매핑·검증을
   소유한다. 직렬화는 카드 JSON의 `kind` 판별자와 `EffectSpecJsonConverter`를 사용한다(§4.2의 대체 기록).
   최초 설계의 `[SerializeReference]`·Unity 인스펙터 저작은 폐기됐다. 리플렉션 자동 등록,
   raw string dictionary는 채택하지 않는다.
2. **코어 `EffectData`의 ApplyStatus 전용 필드도 이번에 payload로 이관한다.** 저작 층만 열면 새 효과가
   결국 공용 모델을 다시 키우기 때문이다.

## 4. 상세 설계

### 4.1 저작 모델 — 다형 EffectSpec

`FateWeaver.Core.Authoring`에 추상 `[Serializable]` 클래스 `EffectSpec`을 두고, 현재 5종 효과를
서브클래스로 옮긴다:

| 서브클래스 | 파라미터 | 대응 코어 키 |
|---|---|---|
| `DamageSpec` | Value, Selector | `EffectKeys.Damage` |
| `ApplyStatusSpec` | StatusKeyRef, Value, Lifetime, LifetimeCount, Target, Selector | `EffectKeys.ApplyStatus` |
| `GrantNextAttackBonusSpec` | Value | `EffectKeys.GrantNextPlayerAttackDamageBonus` |
| `NullifyNextRewardSpec` | (없음) | `EffectKeys.NullifyNextPlayerConditionReward` |
| `MoveFormationSpec` | Value (이동 거리, 음수=전방) | `EffectKeys.MoveFormation` |

Selector(`TargetSelectorRef`)는 코어가 실제로 `TargetSelector`를 읽는 효과(Damage, ApplyStatus)에만
둔다. 현재 mapper는 모든 효과에 selector를 적용하지만 저작된 콘텐츠는 전부 None이므로 등가성에 영향 없다.

각 서브클래스가 구현하는 것:

- `EffectKey Key { get; }` — 대응 코어 키
- `EffectData ToEffectData()` — 자기 파라미터를 코어 모델로 변환 (중앙 mapper 제거)
- `Validate(...)` — 자기 파라미터 검증 (예: 상태 키 등록 여부, 값 범위). 에디터 검증과
  부팅/테스트 검증이 같은 메서드를 호출한다.

공통 개념은 베이스에 유지한다:

- **조건은 닫힌 조합형 유지** (백로그 §10): `ConditionSpec`(기존 `ConditionKind` enum + ConditionN +
  SuccessEffectValue)을 베이스 클래스 소유로 두고, `ConditionKind → Condition` 변환 switch는 한 곳에
  유지한다. 조건이 열린 확장 축이 되는 시점에만 별도 설계한다.

닫힌 enum 참조의 개방:

- `StatusKindRef` enum 폐지 → 직렬화 가능한 `StatusKeyRef`(문자열 키 래퍼). 에디터/부팅 검증에서
  `StatusRegistry` 등록 여부를 확인해 오타를 잡는다.
- `InterventionKind` enum 폐지 → 검증되는 `InterventionKeyRef`(문자열 키 래퍼)로 대체. 개입 액션의
  파라미터는 현재 `{키, 값}`으로 균일하므로 다형 클래스는 만들지 않는다 (YAGNI). 고유 파라미터가
  필요한 개입 액션이 생기는 시점에 효과와 같은 다형 구조로 승격한다.
- `TargetSelectorRef`, `StatusLifetimeKind`, `StatusApplyTarget`은 닫힌 값 집합(백로그 §10)이므로 유지한다.

`CardSpecMapper`는 효과 switch가 사라지고, 카드 수준 조립(Execution/Intervention 분기 + 공통 필드)만
남는다. `CardSpec.Effects`는 `EffectSpec[]`(다형)이 된다.

### 4.2 AI 세션의 JSON 저작

카드·콘텐츠는 AI 세션에서 JSON을 직접 수정한다. 인스펙터 드로어·SO 저작 경로는 사용하지 않는다
([콘텐츠 저작 규칙](../../agents/content-authoring.md)).

효과의 `kind` 판별자는 `EffectSpecJsonConverter`가 명시적 `EffectSpecCatalog` 등록 목록으로 해석한다.
새 효과는 스펙·핸들러와 명시적 등록을 추가하고 콘텐츠 로더·레지스트리 검증을 헤드리스 테스트로 확인한다
([EffectSpecJsonConverter.cs](../../../Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs),
[EffectSpecCatalog.cs](../../../Assets/Core/Authoring/EffectSpecCatalog.cs)). 리플렉션 자동 등록은 하지 않는다.

### 4.3 코어 — EffectData payload

- `EffectData`는 공용 개념만 유지: `Key`, `EffectValue`, `Condition`, `SuccessEffectValue`,
  `TargetSelector`, 그리고 새 슬롯 `IEffectPayload Payload { get; init; }`.
- `StatusKey/StatusLifetime/StatusTarget` 필드를 제거하고 `ApplyStatusPayload(StatusKey, StatusLifetime,
  StatusApplyTarget)` 레코드(코어, `IEffectPayload` 구현)로 이관한다.
- 갱신 대상(코어에서 상태 필드를 읽는 곳 전수 확인 완료): `ApplyStatusHandler`, `PartyTargetRules`,
  설명 층(`BuiltInEffectDescriptionHandlers`, `KoreanDescriptionGrammar`, `DescriptionCatalogValidator`,
  `DescriptionContracts`), 코드 덱(`StarterDeck`, `WardenDeck`, `GoblinDeck`, `PartyPrototypeDeck`,
  `SampleMultiTurnScenarios`), 생성 코드, 테스트.
- `PartyTargetRules`는 `ApplyStatus + PartyMember` 직접 비교라는 P0-C의 문제 지점이기도 하다. 이번
  작업에서는 payload 접근으로 기계적으로만 바꾸고, 대상 선택 일반화는 P0-C에서 다룬다.
- **핸들러가 자기 payload를 검증한다**: 콘텐츠 검증(부팅/테스트)이 카드의 효과마다 핸들러를 resolve해
  payload 타입·필수 값 검사를 위임한다. 기존 `DescriptionCatalogValidator` 패턴을 따른다.

### 4.4 JSON 로딩

SO에서 C# 리터럴을 생성하는 경로는 폐기됐다. 콘텐츠 JSON을 `ContentBootstrap.Load`로 읽고,
카드 로더가 스펙을 검증한 뒤 코어 정의로 변환한다. 헤드리스와 Unity는 같은 JSON을 소비한다
([ContentBootstrap.cs](../../../Assets/Core/Authoring/ContentBootstrap.cs),
[CardContentLoader.cs](../../../Assets/Core/Authoring/CardContentLoader.cs)).

### 4.5 검증 흐름

| 시점 | 검사 | 실패 시 |
|---|---|---|
| JSON 역직렬화 | `kind` 판별자와 명시적 스펙 타입 등록 | 콘텐츠 로드 실패 |
| 부팅·헤드리스 테스트 | `AuthoringValidator`의 카드·효과·개입 파라미터 검증과 등록 대조 | 오류 목록, 부팅 실패 |

근거: [EffectSpecJsonConverter.cs](../../../Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs),
[CardContentLoader.cs](../../../Assets/Core/Authoring/CardContentLoader.cs),
[AuthoringValidator.cs](../../../Assets/Core/Authoring/AuthoringValidator.cs). 검증 명령은 `Tools/verify.sh`다.

### 4.6 샘플 신규 효과 (완료 기준 증명)

테스트 fixture로 샘플 효과 1개(예: `Heal`)를 추가한다 — 핸들러 + spec 클래스 + 설명 핸들러 + 등록만으로
실행·설명·저작·검증 경로가 전부 동작함을 헤드리스 테스트로 보인다. 중앙 enum/mapper 파일은 diff에
나타나지 않아야 한다. 제품 카드에는 사용하지 않는다.

## 5. 완료된 마이그레이션과 현재 확장 절차

최초 다형 스펙 전환과 후속 JSON 전환은 완료된 작업이다. SO 시딩·코드 생성 메뉴를 재실행하지 않는다.
새 효과를 확장할 때는 기존 스펙·컨버터·레지스트리 계약을 읽고 테스트로 실행·설명·저작·검증 경로를
확인한다. 카드 자체는 JSON으로 직접 저작한다([콘텐츠 저작 규칙](../../agents/content-authoring.md)).

## 6. 테스트 전략

- 전체 헤드리스 테스트 통과 유지 (결정론 테스트 포함).
- 등가 스냅샷: 마이그레이션 전후 전체 콘텐츠 `CardDefinition` 동일.
- 샘플 신규 효과: 추가 절차 데모 + 실행/설명/검증 경로 테스트.
- 검증 실패 케이스: 미등록 상태 키, 미등록 효과 spec, payload 타입 불일치가 검증에서 실패하는지.
- JSON의 컨버터·로더 실패 계약은 헤드리스로 검증한다. 표시 결과는 사용자 Play로 확인한다.

## 7. 완료 조건 (백로그 §4 대응)

- [ ] 샘플 신규 효과 추가 시 중앙 enum/mapper 수정 없음 (테스트로 증명)
- [ ] 신규 효과의 실행·설명·저작·검증 경로가 클래스/등록 단위로 국소화
- [ ] JSON 콘텐츠의 로딩·직렬화와 전투 행동 회귀 유지
- [ ] 헤드리스와 Unity가 같은 콘텐츠 JSON을 소비
- [ ] 잘못된 키·필드·파라미터가 콘텐츠 로더·부팅 검증에서 실패

## 8. 범위 밖

- 대상 선택 메타데이터·입력 흐름 (P0-C)
- SO 단일 원본화, 코드 덱 상수 제거 (P1-A)
- 조건(Condition)의 레지스트리화 — 닫힌 조합형 유지 (백로그 §10)
- 개입 액션의 다형 spec — 고유 파라미터가 생길 때 승격
