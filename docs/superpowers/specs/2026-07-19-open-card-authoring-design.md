# P0-B — 열린 카드 저작 구조 설계

- 작성일: 2026-07-19
- 문서 유형: `architecture`
- 주 도메인: `card-authoring`
- 상태: `current` — 구현된 열린 카드 효과 저작 구조
- 원 백로그: [`../plans/2026-07-16-architecture-refactor-backlog.md`](../plans/2026-07-16-architecture-refactor-backlog.md) §4
- 선행 완료: P0-A RNG 단일화 (2026-07-18)

## 1. 목적

새 효과 저작 타입을 **중앙 enum/switch 수정 없이** 추가할 수 있게 저작 층을 연다. 코어 런타임은 이미
열린 구조(`EffectKey` + 핸들러 레지스트리)이므로, 병목인 저작 층(`EffectKind` 등 enum 5종 +
`CardSpecMapper` 중앙 switch)과 공용 모델(`EffectData`의 ApplyStatus 전용 필드)을 같은 원칙으로 정리한다.

## 2. 현황 (문제)

- `EffectSpec`은 평면 struct: 모든 효과가 10개 필드를 공유하고, 인스펙터에 무관한 필드가 전부 노출된다.
- `EffectKind`, `StatusKindRef`, `InterventionKind` enum이 닫힌 집합을 강제한다.
- `CardSpecMapper`의 switch가 enum → 코어 키/레코드 변환을 중앙 집중한다.
- `EffectData`가 `StatusKey/StatusLifetime/StatusTarget` 같은 ApplyStatus 전용 필드를 직접 포함한다.
- 새 효과 1개 추가 = enum 수정 + switch 2곳 수정 + 공용 모델 필드 추가 (+핸들러/설명).

## 3. 결정 사항

사용자와 합의된 두 가지 핵심 결정:

1. **저작 구조는 다형 spec 클래스** (백로그 §4 대안 1). `[SerializeReference]` 기반, 효과별 클래스가
   자기 파라미터·매핑·검증을 소유한다. 리플렉션 자동 등록, raw string dictionary는 채택하지 않는다.
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

### 4.2 Unity 층

- ~~`CardAsset.Effects`를 `[SerializeReference] EffectSpec[]`로 전환한다.~~ **대체됨
  (계획 3b)** — 효과는 이제 카드 JSON의 `effects[]` 배열이고, 다형성은 `"kind"` 판별자와
  `EffectSpecJsonConverter`가 처리한다. Unity 인스펙터 저작 경로 자체가 없어졌다.
- 에디터 전용 **서브클래스 선택 드로어 1개**를 추가한다: Effects 리스트의 + / 타입 변경 시 등록된
  spec 타입 목록(한글 표시명)을 드롭다운으로 제공하고, 선택된 타입의 필드만 그린다.
- 드롭다운 후보는 리플렉션 스캔이 아니라 **명시적 spec 타입 등록 목록**에서 온다(사전 승인 없는
  리플렉션 자동 등록 금지 원칙). 등록 목록은 부팅 검증에서 코어 레지스트리와 대조한다.

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

### 4.4 코드 생성

- `CardCodeGenerator`가 다형 리터럴을 생성한다: `new DamageSpec { Value = 4, Condition = ... }`.
  Unity SO와 헤드리스 export가 같은 스키마(같은 클래스)를 사용한다는 원칙은 그대로다.
- 손저작 spec(`StarterDeckSpecs`, `PartyPrototypeDeckSpecs`)도 새 모델로 이전한다.

### 4.5 검증 흐름

| 시점 | 검사 | 실패 시 |
|---|---|---|
| ~~에디터 (CardAsset 인스펙터/저장)~~ **없어짐 (계획 3b)** | spec.Validate — 키 등록 여부, 파라미터 값 | ~~인스펙터 에러 표시~~ |
| ~~코드 생성 시~~ **없어짐 (계획 3b)** | 전체 카드 walk + Validate | ~~생성 중단, 에러 로그~~ |
| 부팅/헤드리스 테스트 | spec 타입 등록 목록 ↔ 코어 레지스트리 대조, payload 타입 검사 | 예외 (부팅 실패) |

### 4.6 샘플 신규 효과 (완료 기준 증명)

테스트 fixture로 샘플 효과 1개(예: `Heal`)를 추가한다 — 핸들러 + spec 클래스 + 설명 핸들러 + 등록만으로
실행·설명·저작·검증 경로가 전부 동작함을 헤드리스 테스트로 보인다. 중앙 enum/mapper 파일은 diff에
나타나지 않아야 한다. 제품 카드에는 사용하지 않는다.

## 5. 마이그레이션·등가성 전략 (백로그 §11)

1. **구조 변경 전에 등가 스냅샷 테스트를 먼저 확보한다**: 현재 `CardSpecMapper` 경로로 만든 전체
   콘텐츠(시작덱·적덱·파티 검증덱)의 `CardDefinition`을 서명(문자열 스냅샷)으로 고정.
2. 새 모델로 전환 후 같은 서명이 나오는지 검증 (기존 `StarterDeckSpecEquivalenceTests` 확장).
3. Unity 자산: struct→`[SerializeReference]` 직렬화 단절은 `Seed Starter/Enemy Card Assets` 재실행으로
   해소한다 (Art 보존 로직 기존 존재). 헤드리스 검증과 사용자 Play 검증을 분리해 기록한다.
4. 이번 변경에 다른 대규모 마이그레이션(SO 단일 원본화 등)을 섞지 않는다. P1-A는 이 저작 모델 확정
   후 별도 진행.

## 6. 테스트 전략

- 기존 260개 헤드리스 테스트 전부 통과 유지 (P0-A 결정론 테스트 포함).
- 등가 스냅샷: 마이그레이션 전후 전체 콘텐츠 `CardDefinition` 동일.
- 샘플 신규 효과: 추가 절차 데모 + 실행/설명/검증 경로 테스트.
- 검증 실패 케이스: 미등록 상태 키, 미등록 효과 spec, payload 타입 불일치가 검증에서 실패하는지.
- Unity 드로어는 헤드리스로 검증 불가 → 에디터 수동 확인 항목으로 분리.

## 7. 완료 조건 (백로그 §4 대응)

- [ ] 샘플 신규 효과 추가 시 중앙 enum/mapper 수정 없음 (테스트로 증명)
- [ ] 신규 효과의 실행·설명·저작·검증 경로가 클래스/등록 단위로 국소화
- [ ] 기존 시작덱·적덱·파티 검증 카드의 export 등가성 유지 (스냅샷 테스트)
- [ ] 생성 파일과 런타임 SO가 동일 `CardDefinition` 생성
- [ ] 잘못된 키·필드·파라미터가 에디터/부팅 검증에서 실패

## 8. 범위 밖

- 대상 선택 메타데이터·입력 흐름 (P0-C)
- SO 단일 원본화, 코드 덱 상수 제거 (P1-A)
- 조건(Condition)의 레지스트리화 — 닫힌 조합형 유지 (백로그 §10)
- 개입 액션의 다형 spec — 고유 파라미터가 생길 때 승격
