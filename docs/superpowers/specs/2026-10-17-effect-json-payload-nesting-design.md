# 효과·조작 JSON을 코어 모양으로 맞추고 페이로드를 중첩한다

사람 검토용 개요: [HTML](2026-10-17-effect-json-payload-nesting-design.html)

## 상세

### 상태와 범위

- 작성일: 2026-10-17 · 상태: `current` (2026-10-17 구현 완료. 구현 계획은 보관됨)
- 주 도메인: `card-authoring`
- 대체 관계:
  - [열린 카드 저작 구조](2026-07-19-open-card-authoring-design.md)의 효과·조작 JSON 형식(`kind` 판별자,
    효과별 숫자 필드 이름, 평평한 페이로드 필드)을 이 문서가 대체한다. 다형 스펙·명시적 카탈로그·스펙별
    검증 구조는 그대로다.
  - [전투 실행 계약](2026-09-18-combat-execution-contract-design.md):93-94의 카드 스키마 버전과 로딩 경계
    구형 변환을 이 문서가 폐지한다.

사용자 결정(2026-10-17, 원문 그대로):

1. "consume_status 개선: amount 필드를 제거"
2. "json 과 effectdata 의 필드명 통일: effectdata 쪽으로 통일. 앞으로 저작 파일은 사람이 보지 않고
   ai 세션을 통해 작업하므로"
3. "범위 2(페이로드 중첩 포함)로 가자"
4. "테스트를 추가할 필요는 없고"
5. "마이그레이션 관련 로직도 지우고, 카드의 버전 관리도 없애고 항상 모든 카드의 버전을 최신 버전으로
   관리하고 싶어"
6. "갱신안대로 설계 문서(.html+.md)를 고쳐줘: 마이그레이션·형식 버전 제거, 조작 카드 key+payload 포함,
   시작 조건 kind 유지."

### 원칙

**P1 — 세기 숫자는 공통 칸, 종류 전용 선택값은 페이로드.** 효과가 "얼마나"를 나타내는 숫자 하나는
`EffectData.EffectValue`에 둔다. 조건 성공 수치 교체와 `scaling` 가산은 이 칸에만 적용되기 때문이다
(`EffectExecutor.cs:178-186`). 페이로드에는 그 효과 종류만 쓰는 선택값 — 무엇을(어떤 상태),
어떤 식으로(소비 방식, 피해 속성) — 만 둔다. 세기 숫자를 페이로드에 두지 않는다.
- 경계 사례: 세기 숫자가 없는 효과(`trigger_status`, `nullify_next_player_condition_reward`)는
  `effectValue`를 갖지 않는다. 스펙에 필드가 없으므로 JSON에 적으면 `MissingMemberHandling.Error`
  (`ContentJson.cs:38`)가 거부한다.
- 경계 사례: 조작 카드의 봉투(`InterventionActionData`)에는 공통 숫자 칸이 없다(`InterventionActionData.cs:9-13`).
  그래서 P1은 조작 카드에 적용되지 않고, 실행순서 이동 칸 수(`Delta`)는 페이로드에 남는다.
- 이 원칙은 페이로드 도입 당시 주석 *"Magnitude rides on EffectValue"*(커밋 `37c7ccb` 이전
  `CardDefinition.cs`)와 `ApplyStatusPayload.cs:5-7`이 이미 말한 것이다. `consume_status`만 어겼다.

**P2 — 효과·조작 JSON의 이름과 구조는 코어를 그대로 따른다.** 효과 한 줄의 필드 이름은 `EffectData`의
속성 이름을, 조작 액션의 필드 이름은 `InterventionActionData`의 속성 이름을 camelCase로 쓴 것이다.
`payload` 객체의 필드 이름은 그 종류의 코어 페이로드 레코드 속성 이름을 camelCase로 쓴 것이다.
- 근거: 저작자는 AI 세션이다(사용자 결정 2). AI가 코어 레코드 정의만 읽고 JSON 모양을 알 수 있어야 한다.
- 경계 사례: 판별자는 코어의 `Key`이므로 JSON 이름은 `key`다. 상태 페이로드에도 `Key`(상태 키)가
  있어 같은 층에 둘 수 없다 — 이것이 페이로드를 중첩하는 직접 이유다.
- 경계 사례: 값의 어휘는 저작 열거형을 따른다. 조작 대상 진영은 코어에서 `Side?`(null = 제한 없음)이지만
  JSON 값은 `InterventionTargetSideRef`의 `"Any"`/`"Player"`/`"Enemy"`다. 이름(`targetSide`)만 코어를 따른다.
- 적용하지 않는 곳: **시작 조건의 `kind`**. 이것은 코어의 `Key`가 아니라 미리 정한 조건 이름(닫힌 enum
  `ConditionKind`)이고, switch 한 곳에서 코어 조건 객체로 바뀐다(`EffectSpec.cs:21-41`). 코어
  `Condition`에는 대응할 `Key`가 없다. 이 닫힌 구조는 기존 결정이다(열린 카드 저작 설계 §4.1, 백로그 §10).
  카드 수준 필드(`id`, `name`, `side`, `category`, `energyCost`, `baseExecutionOrder`, `targets`,
  `intervention`, `startCondition`, `grade`, `tags`)도 이번 범위 밖이다.

**P3 — 카드 JSON은 형식 버전을 갖지 않는다.** 저장소의 모든 카드는 항상 최신 형식으로 쓴다. 형식이
바뀌면 그 변경 커밋에서 모든 카드를 함께 고친다. 로딩 경계의 구형 변환을 두지 않는다.
- 근거: 사용자 결정 5. 옛 형식은 조용히 통과하지 못한다 — 옛 필드 이름은 `MissingMemberHandling.Error`가,
  `kind`로 적힌 효과·조작은 컨버터의 `key` 필수 검사가 거부한다.

### 현행 (근거)

- JSON 이름 = 스펙 클래스 필드 이름의 camelCase: `ContentJson.cs:36`.
- 효과 판별자 `kind`: `EffectSpecJsonConverter.cs:12` (`KindProperty`), 읽을 때 제거 후 Populate(`:34-38`),
  쓸 때 맨 앞에 추가(`:53-54`). 조작도 같은 모양: `InterventionSpecJsonConverter.cs:13,23-35,55`.
- 효과 공통 필드: `EffectSpec.cs:80-96` — `Id`, `TargetFaction`, `SuccessEffectValue`, `SkipOnBasic`, `Requires`, `ScaleBy`.
- 효과별 필드와 `Build()`:

| 스펙 | 숫자 필드 | 페이로드 재료 | `Build()` |
|---|---|---|---|
| `DamageSpec` | `Value` | `Traits` | `DamageSpec.cs:22-29` (속성 Normal이면 페이로드 null) |
| `ApplyStatusSpec` | `Count` | `Status` | `ApplyStatusSpec.cs:23-24` |
| `ConsumeStatusSpec` | `Amount` → **페이로드로 감** | `Status`, `Mode` | `ConsumeStatusSpec.cs:27-28` (`EffectValue` = 0) |
| `TriggerStatusSpec` | 없음 | `Status` | `TriggerStatusSpec.cs:18-19` (`EffectValue` = 0) |
| `GrantNextTurnFateSpec` | `Value` | 없음 | `GrantNextTurnFateSpec.cs:13` |
| `GrantNextDamageCardBonusSpec` | `Value` | 없음 | `GrantNextDamageCardBonusSpec.cs:13` |
| `MoveFormationSpec` | `Value` | 없음 | `MoveFormationSpec.cs:16` |
| `NullifyNextRewardSpec` | 없음 | 없음 | `NullifyNextRewardSpec.cs:11` |

- 조작 스펙: `ChangeExecutionOrderSpec { Delta, TargetSide }`(`ChangeExecutionOrderSpec.cs:9-15`),
  `SwapExecutionOrderSpec { TargetSide, RequireAdjacent }`(`SwapExecutionOrderSpec.cs:9-15`),
  `LockSpec`(페이로드 없음, `LockSpec.cs:11`). 기반 `InterventionSpec.cs:11-34`.
- `consume_status`의 개수를 읽는 곳: `ConsumeStatusHandler.cs:33`(실행), `:62`(검증),
  `BuiltInEffectDescriptionHandlers.cs:64`(설명. 넘겨받은 `value`(`:61`)를 무시함).
- 형식 버전: `CardSpec.cs:18-24`(`CurrentFormat = 2`, `CardFormat` 필드), `CardSpecJsonConverter.cs:12,16,42-51`
  (없으면 `CardFormatMigration.Upgrade`, 다른 값이면 거부), `AuthoringValidator.cs:31-33`(형식 번호 검증),
  `CardFormatMigration.cs`(265줄, v1→v2).
- 콘텐츠: `Assets/StreamingAssets/Content/Cards/*.json` 35장(실행 31, 조작 4), 전부 `cardFormat: 2`. 효과 47줄.
  효과 필드 사용 수: `kind` 47, `value` 19, `count` 23, `amount` 3, `status` 28, `mode` 3, `requires` 2,
  `scaleBy` 1, `successEffectValue` 4, `traits` 0. 조작 필드: `kind` 4, `delta` 3, `targetSide` 3,
  `requireAdjacent` 1. 상태·적·편성·캐릭터 JSON은 효과·조작 줄이 없다.

### 새 형식

효과 한 줄:

```json
{
  "key": "<EffectKey.Id>",
  "id": "e0",
  "targetFaction": "Enemy",
  "effectValue": 3,
  "successEffectValue": 5,
  "skipOnBasic": true,
  "requirement": { "sourceEffectId": "e0", "minimumConsumed": 1 },
  "scaling": { "sourceEffectId": "e0", "perConsumed": 2 },
  "payload": { ... }
}
```

| 지금 | 새 형식 | 비고 |
|---|---|---|
| `kind` | `key` | 판별자 |
| `value` / `count` / `amount` | `effectValue` | 세기 숫자가 있는 효과만 |
| `requires` | `requirement` | 내부 필드(`sourceEffectId`, `minimumConsumed`) 그대로 |
| `scaleBy` | `scaling` | 내부 필드(`sourceEffectId`, `perConsumed`) 그대로 |
| `status` | `payload.key` | apply·consume·trigger |
| `mode` | `payload.mode` | consume |
| `traits` | `payload.traits` | damage. 속성이 없으면 `payload`를 생략한다(속성 없는 `payload`는 로딩에서 거부, 2026-10-17 후속) |
| `id`, `targetFaction`, `successEffectValue`, `skipOnBasic` | 그대로 | 이미 같다 |

효과별 `payload` (코어 레코드의 거울):

| 효과 `key` | `effectValue` | `payload` | 코어 레코드 |
|---|---|---|---|
| `damage` | 있음 | `{ "traits": [...] }` 또는 생략 | `DamagePayload(DamageTraits Traits)` |
| `apply_status` | 있음 | `{ "key": "poison" }` | `ApplyStatusPayload(StatusKey Key)` |
| `consume_status` | 있음 | `{ "key": "poison", "mode": "UpTo" }` | `ConsumeStatusPayload(StatusKey Key, ConsumptionMode Mode)` ← `Amount` 제거 |
| `trigger_status` | 없음 | `{ "key": "poison" }` | `TriggerStatusPayload(StatusKey Key)` |
| `grant_next_turn_fate`, `grant_next_player_damage_card_bonus`, `move_formation` | 있음 | 없음 | — |
| `nullify_next_player_condition_reward` | 없음 | 없음 | — |

조작 액션(카드의 `intervention` 객체 안):

| 조작 `key` | 지금 | 새 형식 | 코어 레코드 |
|---|---|---|---|
| `change_execution_order` | `{ "kind": …, "delta": 1, "targetSide": "Player" }` | `{ "key": …, "payload": { "delta": 1, "targetSide": "Player" } }` | `ChangeExecutionOrderPayload(int Delta, Side? TargetSide)` |
| `swap_execution_order` | `{ "kind": …, "requireAdjacent": true }` | `{ "key": …, "payload": { "requireAdjacent": true } }` | `SwapExecutionOrderPayload(Side? TargetSide, bool RequireAdjacent)` |
| `lock` | `{ "kind": "lock" }` | `{ "key": "lock" }` | 페이로드 없음 |

카드 최상위의 `"cardFormat"`은 지운다(P3). 그 밖의 카드 수준 필드와 `startCondition`(`kind`, `n`)은 그대로다.

### 코어 변경 (`Assets/Core/Effects`, `Simulation/Descriptions`)

1. `ConsumeStatusPayload.cs`: `record ConsumeStatusPayload(StatusKey Key, ConsumptionMode Mode)`. 문서 주석의
   개수 언급을 "개수는 `EffectData.EffectValue`"로 고친다.
2. `ConsumeStatusHandler.cs:33`: `ConsumptionRule.Take(available, ctx.EffectValue, payload.Mode)`.
   `ConsumeFrom`은 이미 `ctx`를 받는다(`:29`).
3. `ConsumeStatusHandler.cs:62` 검증: `payload.Amount < 1` → `effect.EffectValue < 1`
   (메시지 "consume_status EffectValue must be at least 1.").
4. `BuiltInEffectDescriptionHandlers.cs:64`: `("amount", payload.Amount)` → `("amount", value)`.
   `"amount"`는 현지화 템플릿의 자리표시자 이름이므로 그대로 둔다.
5. `IEffectPayload.cs` 문서 주석에 P1을 한 문장으로 더한다.

결과: 조건 성공 수치와 `scaling`이 소비 개수에 적용된다. 현재 카드 3장(`consume_status`)은 성공 수치·
`scaling`을 소비 효과에 쓰지 않으므로 동작이 같다. 조작 카드 코어(`Assets/Core/Intervention`)는 바뀌지 않는다.

### 저작 층 변경 (`Assets/Core/Authoring`)

효과:

1. `EffectSpec.cs`: `Requires` → `Requirement`, `ScaleBy` → `Scaling` (타입 `EffectRequirementSpec`·
   `EffectScalingSpec` 그대로, `JsonProperty Order` 52·53 그대로). `ToEffectData()`(`:111-124`)의 대입도 맞춘다.
   **`EffectValue`는 기반 클래스에 두지 않는다** — 세기 숫자가 없는 스펙에 필드가 생기면
   `MissingMemberHandling.Error`가 하던 거부가 사라진다(P1 경계 사례).
2. 세기 숫자가 있는 스펙(`DamageSpec`, `ApplyStatusSpec`, `ConsumeStatusSpec`, `GrantNextTurnFateSpec`,
   `GrantNextDamageCardBonusSpec`, `MoveFormationSpec`)은 `Value`/`Count`/`Amount`를 지우고
   `[JsonProperty(Order = -18)] public int EffectValue;`를 선언한다. `Build()`는 `new EffectData(Key, EffectValue)`.
   필드 주석(예: `ApplyStatusSpec.cs:15-16`의 세기/지속 설명)은 새 필드로 옮긴다.
3. 페이로드가 있는 효과 스펙은 재료 필드(`Status`, `Mode`, `Traits`)를 지우고 `[JsonProperty(Order = 60)] public
   XxxPayloadSpec Payload;`를 선언한다. 페이로드 스펙은 코어 레코드마다 하나, 그 스펙과 같은 파일에 둔다:
   - `ApplyStatusPayloadSpec { public StatusKeyRef Key; }`
   - `ConsumeStatusPayloadSpec { public StatusKeyRef Key; [JsonProperty(DefaultValueHandling = Include)] public ConsumptionMode Mode; }`
     (`ConsumeStatusSpec.cs:16-19`의 "항상 쓴다" 이유 주석을 옮긴다)
   - `TriggerStatusPayloadSpec { public StatusKeyRef Key; }`
   - `DamagePayloadSpec { public DamageTrait[] Traits; }` — `DamageSpec`은 `Payload`가 null이거나 속성이 비면
     코어 페이로드를 만들지 않는다(현행 `:27`과 같음). **2026-10-17 후속:** 속성 없는 `payload`(`{}`, 빈 `traits`)는
     `Validate`가 "damage payload needs traits; omit payload for normal damage."로 거부한다. 보통 피해를 쓰는 형태를
     "payload 생략" 하나로 고정해 왕복 바이트가 달라지지 않게 한다. 그래서 `ShouldSerializePayload()`는 두지 않는다.
4. 효과 스펙 `Validate`: `Payload == null`이면 apply·consume·trigger는 "<key> requires a payload." 오류.
   `Status.IsEmpty`·`HasStatus` 검사는 `Payload.Key`로, `Amount < 1`은 `EffectValue < 1`로 옮긴다
   (`ConsumeStatusSpec.cs:41-44` → "consume_status effectValue must be at least 1."). damage는
   `DamageTraitRules.Validate(Payload?.Traits, ...)`.
5. `EffectSpecJsonConverter.cs`: `KindProperty` → `KeyProperty = "key"`, 오류 메시지의 "kind"도 "key"로.
   지역 변수·사전 이름(`FactoryByKind` 등)도 `Key`로 맞춘다.
6. `AuthoringValidator.cs:112-123`: `effect.Requires`/`ScaleBy` → `Requirement`/`Scaling`, 오류 문구의
   필드 이름 `"requires"`/`"scaleBy"` → `"requirement"`/`"scaling"`.

조작:

7. `ChangeExecutionOrderSpec`·`SwapExecutionOrderSpec`은 필드(`Delta`, `TargetSide`, `RequireAdjacent`)를 지우고
   `public XxxPayloadSpec Payload;`를 선언한다. 같은 파일에 페이로드 스펙을 둔다:
   - `ChangeExecutionOrderPayloadSpec { public int Delta; public InterventionTargetSideRef TargetSide; }`
   - `SwapExecutionOrderPayloadSpec { public InterventionTargetSideRef TargetSide; public bool RequireAdjacent; }`
   두 스펙의 `Validate`는 `Payload == null`이면 "<key> requires a payload."로 거부한다. `ToPayload()`는 검증을
   통과한 스펙만 받으므로(`CardSpecMapper.cs:11`) `Payload`가 있다고 보고 코어 레코드를 만든다.
   `LockSpec`은 그대로다(필드 없음).
8. `InterventionSpecJsonConverter.cs`: `KindProperty` → `KeyProperty = "key"`, 오류 메시지·지역 변수도 맞춘다.

공통:

9. 그 밖에 바뀐 필드를 읽는 곳은 컴파일 오류로 찾는다(조사 시점 비테스트 코드에는 위 외에 없음.
   `Assets/Unity`는 스펙을 참조하지 않는다).

### 형식 버전과 마이그레이션 제거 (P3)

1. `Assets/Core/Authoring/Json/CardFormatMigration.cs`와 `.meta`를 지운다.
2. `CardSpec.cs:18-24`: `CurrentFormat` 상수와 `CardFormat` 필드를 지운다. 이 필드에 걸린 `JsonProperty Order`
   설명 주석이 있으면 남은 필드 기준으로 고친다.
3. `CardSpecJsonConverter.cs`: `FormatProperty`(`:16`)와 형식 분기(`:42-51`)를 지우고, 클래스 주석(`:12`)의
   마이그레이션 언급을 지운다.
4. `AuthoringValidator.cs:31-33`: 형식 번호 검증을 지운다.
5. 콘텐츠 35장에서 `"cardFormat"`을 지우고, 효과·조작 줄을 새 형식으로 다시 쓴다. 변환은 위 "새 형식" 표를
   그대로 적용한다(일회성 작업. 변환 스크립트를 쓰면 저장소에 남기지 않는다).

### 도구·문서

1. `Tools/graph/extract_card_graph.py`: `KNOWN_EFFECT_FIELDS`(`:21`)를 새 효과 필드 집합
   (`key`, `id`, `targetFaction`, `effectValue`, `successEffectValue`, `skipOnBasic`, `requirement`, `scaling`,
   `payload`)으로, 효과 종류는 `eff.get("key")`(`:122`), 상태 키는 `eff.get("payload", {}).get("key")`(`:135`)로 읽는다.
   `Tools/graph/test_extract_card_graph.py`의 인라인 효과 JSON(`:67-84`)도 새 형식으로 맞춘다.
2. `docs/agents/content-authoring.md` 「AI 세션에서 JSON을 직접 저작한다」 절에 P2·P3을 한 문단으로 더하고
   이 문서를 링크한다.
3. 구현 완료 커밋에서:
   - `2026-07-19-open-card-authoring-design.md` 머리말에 "**2026-10-17 후속:**" 한 줄로 효과·조작 JSON 형식의
     대체를 적는다.
   - `2026-09-18-combat-execution-contract-design.md` 머리말에 "**2026-10-17 후속:**" 한 줄로 카드 스키마 버전과
     구형 변환의 폐지를 적는다.
   - `docs/superpowers/README.md`에서 이 문서 행의 상태를 `active` → `current`로 바꾸고, 열린 카드 저작 행의
     적용 범위 "(JSON `kind` 판별자와 컨버터)"를 이 문서로 넘긴다. (이 문서 행은 설계 커밋에서 `active`로 이미 더했다.)

### 테스트

- 새 테스트는 쓰지 않는다(사용자 결정 4). AGENTS.md 규칙 12("새 규칙 로직에는 헤드리스 테스트가 필수다")와
  긴장이 있다 — 소비 개수에 성공 수치·`scaling`이 적용되는 것은 새 동작이다. 사용자 결정을 따르고 여기 기록한다.
- **지우는 테스트:** 형식 버전·구형 변환 자체를 검증하는 테스트.
  - `CardFormatContractTests.cs`: `Rejects_an_unsupported_card_format`(`:135`), `Legacy_card_upgrades_by_rule`(`:158`),
    `Legacy_card_loads_to_the_same_definition_as_its_upgraded_form`(`:183`),
    `Legacy_card_with_different_conditions_is_rejected_not_merged`(`:199`),
    `Legacy_explicit_party_member_target_is_rejected`(`:216` 부근), `Legacy_enemy_card_keeps_its_own_self_and_attacks_the_party`(`:228` 부근).
    같은 파일의 결과 참조·대상 축 검증 테스트(`:47-133`)는 새 형식 JSON으로 고쳐 유지한다.
  - `CardContentJsonTests.cs`: `CardFormatIsWrittenFirst`(`:116`)를 지우고, `:317`의 형식 번호 단언을 지운다.
- **고치는 테스트:** `ConsumeStatusPayload` 생성자(3인자→2인자), 스펙 객체 초기화(`Count =`·`Amount =`·
  `Value =`·`Status =`·`Mode =`·`Traits =`·`Requires =`·`ScaleBy =`·`Delta =`·`TargetSide =`·`RequireAdjacent =`),
  인라인 JSON 문자열(`cardFormat`·`kind`·`count`·`value`·`amount`·`status`·`scaleBy` 등 — 예: `StatusDamageTraitsTests.cs:135`).
  조사 시점 후보 25개 파일(`Assets/Core/Tests/EditMode/`)이며 확정은 컴파일 오류와 `Tools/verify.sh`로 한다.

### 실행 순서

1. 코어 변경 1~5 → 저작 층 1~9 → 형식·마이그레이션 제거 1~4 → 테스트 수정·삭제 → `Tools/verify.sh` 통과.
2. 콘텐츠 35장 새 형식 재작성(형식 제거 5) → `Tools/verify.sh` 통과(부팅 로드가 전 카드를 검증).
3. 도구·문서 1~3 → `cd Tools/graph && python3 -m unittest test_extract_card_graph` 통과
   (테스트는 `unittest`이고 `extract_card_graph`를 같은 폴더에서 import한다, `test_extract_card_graph.py:3-6`).
4. 커밋은 단계별로 나눈다(코어+저작+형식 제거+테스트 / 콘텐츠 / 도구·문서). 커밋 메시지 규칙 27.
   1단계 커밋 시점에는 콘텐츠가 옛 형식이라 부팅 로드 테스트가 실패하므로, 1·2단계를 한 커밋으로 묶어도 된다.

### 범위 밖과 후속

- 세기 숫자가 없는 효과에 `successEffectValue`·`scaling`을 적으면 여전히 조용히 무시된다(기반 클래스 필드라
  `MissingMemberHandling`이 막지 못함). 이 작업은 `effectValue`만 막는다. 검증 추가는 후속 과제다.
- 시작 조건의 `kind`(P2 적용하지 않는 곳)와 카드 수준 필드 이름.
- 상태·적·편성·캐릭터 JSON.
