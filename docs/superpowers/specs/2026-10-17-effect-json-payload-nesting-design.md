# 효과 JSON을 EffectData 모양으로 맞추고 페이로드를 중첩한다

사람 검토용 개요: [HTML](2026-10-17-effect-json-payload-nesting-design.html)

## 상세

### 상태와 범위

- 작성일: 2026-10-17 · 상태: `active` (설계 승인 대기. 구현이 끝나면 `current`로 바꾼다)
- 주 도메인: `card-authoring` · 대체 관계: [열린 카드 저작 구조](2026-07-19-open-card-authoring-design.md)의
  효과 JSON 형식(`kind` 판별자, 효과별 숫자 필드 이름, 평평한 페이로드 필드)을 이 문서가 대체한다.
  저작 층의 다형 스펙·명시적 카탈로그·스펙별 검증 구조는 그대로다.

사용자 결정(2026-10-17, 원문 그대로):

1. "consume_status 개선: amount 필드를 제거"
2. "json 과 effectdata 의 필드명 통일: effectdata 쪽으로 통일. 앞으로 저작 파일은 사람이 보지 않고
   ai 세션을 통해 작업하므로"
3. 범위: "범위 2(페이로드 중첩 포함)로 가자" — 효과 한 줄 전체(공통 칸 + `kind` + 페이로드 필드)를
   통일하고 페이로드를 JSON에서 중첩한다. 카드 수준 필드는 범위 밖이다.
4. "테스트를 추가할 필요는 없고" — 새 테스트는 쓰지 않는다. 기존 테스트는 컴파일·통과를 위해 고친다.

### 원칙

**P1 — 세기 숫자는 공통 칸, 종류 전용 선택값은 페이로드.** 효과가 "얼마나"를 나타내는 숫자 하나는
`EffectData.EffectValue`에 둔다. 조건 성공 수치 교체와 `scaling` 가산은 이 칸에만 적용되기 때문이다
(`EffectExecutor.cs:178-186`). 페이로드에는 그 효과 종류만 쓰는 선택값 — 무엇을(어떤 상태),
어떤 식으로(소비 방식, 피해 속성) — 만 둔다. 세기 숫자를 페이로드에 두지 않는다.
- 경계 사례: 세기 숫자가 없는 효과(`trigger_status`, `nullify_next_reward`)는 `effectValue`를 갖지 않는다.
  스펙에 필드가 없으므로 JSON에 적으면 `MissingMemberHandling.Error`(`ContentJson.cs:38`)가 거부한다.
- 이 원칙은 페이로드 도입 당시 주석 *"Magnitude rides on EffectValue"*(커밋 `37c7ccb` 이전
  `CardDefinition.cs`)와 `ApplyStatusPayload.cs:5-7`이 이미 말한 것이다. `consume_status`만 어겼다.

**P2 — 효과 JSON의 이름과 구조는 코어를 그대로 따른다.** 효과 한 줄의 JSON 필드 이름은
`EffectData`의 속성 이름을 camelCase로 쓴 것이고, `payload` 객체의 필드 이름은 그 효과의 코어
페이로드 레코드(`ApplyStatusPayload` 등)의 속성 이름을 camelCase로 쓴 것이다. 예외를 두지 않는다.
- 근거: 저작자는 AI 세션이다(사용자 결정 2). AI가 코어 레코드 정의만 읽고 JSON 모양을 알 수 있어야 한다.
- 경계 사례: 판별자는 `EffectData.Key`이므로 JSON 이름은 `key`다. 페이로드 레코드에도 `Key`
  (상태 키)가 있어 같은 층에 둘 수 없다 — 이것이 페이로드를 중첩하는 직접 이유다.

### 현행 (근거)

- JSON 이름 = 스펙 클래스 필드 이름의 camelCase: `ContentJson.cs:36` (`CamelCasePropertyNamesContractResolver`).
- 판별자 `kind`: `EffectSpecJsonConverter.cs:12` (`KindProperty = "kind"`), 읽을 때 제거 후 Populate(`:34-38`),
  쓸 때 맨 앞에 추가(`:53-54`).
- 공통 필드: `EffectSpec.cs:80-96` — `Id`, `TargetFaction`, `SuccessEffectValue`, `SkipOnBasic`, `Requires`, `ScaleBy`.
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

- `consume_status`의 개수를 읽는 곳: `ConsumeStatusHandler.cs:33`(실행), `:62`(검증),
  `BuiltInEffectDescriptionHandlers.cs:64`(설명, 넘겨받은 `value`(`:61`)를 무시함).
- 형식 버전: `CardSpec.CurrentFormat = 2`(`CardSpec.cs:20`). `cardFormat`이 없으면 v1→현재로 올리고
  (`CardFormatMigration.Upgrade`, `CardSpecJsonConverter.cs:42-46`), 다른 값이면 거부한다(`:47-51`).
  `Upgrade`는 끝에 `cardFormat = CurrentFormat`을 쓴다(`CardFormatMigration.cs:33`).
- 콘텐츠: `Assets/StreamingAssets/Content/Cards/*.json` 35장, 전부 `cardFormat: 2`. 효과 47줄.
  필드 사용 수: `kind` 47, `value` 19, `count` 23, `amount` 3, `status` 28, `mode` 3, `requires` 2,
  `scaleBy` 1, `successEffectValue` 4, `traits` 0. 상태·적·편성·캐릭터 JSON은 효과 줄이 없다.

### 새 형식 (cardFormat 3)

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

| v2 | v3 | 비고 |
|---|---|---|
| `kind` | `key` | 판별자 |
| `value` / `count` / `amount` | `effectValue` | 세기 숫자가 있는 효과만 |
| `requires` | `requirement` | 내부 필드(`sourceEffectId`, `minimumConsumed`) 그대로 |
| `scaleBy` | `scaling` | 내부 필드(`sourceEffectId`, `perConsumed`) 그대로 |
| `status` | `payload.key` | apply·consume·trigger |
| `mode` | `payload.mode` | consume |
| `traits` | `payload.traits` | damage. 비면 `payload` 자체를 쓰지 않는다 |
| `id`, `targetFaction`, `successEffectValue`, `skipOnBasic` | 그대로 | 이미 같다 |

효과별 `payload` 모양 (코어 레코드의 거울):

| 효과 `key` | `effectValue` | `payload` | 코어 레코드 |
|---|---|---|---|
| `damage` | 있음 | `{ "traits": [...] }` 또는 생략 | `DamagePayload(DamageTraits Traits)` |
| `apply_status` | 있음 | `{ "key": "poison" }` | `ApplyStatusPayload(StatusKey Key)` |
| `consume_status` | 있음 | `{ "key": "poison", "mode": "UpTo" }` | `ConsumeStatusPayload(StatusKey Key, ConsumptionMode Mode)` ← `Amount` 제거 |
| `trigger_status` | 없음 | `{ "key": "poison" }` | `TriggerStatusPayload(StatusKey Key)` |
| `grant_next_turn_fate`, `grant_next_player_damage_card_bonus`, `move_formation` | 있음 | 없음 | — |
| `nullify_next_player_condition_reward` | 없음 | 없음 | — |

범위 밖(바꾸지 않는다): 카드 수준 필드(`targets`, `intervention`, `startCondition` 등), 조작 카드의
`intervention.kind`·`delta`·`targetSide`·`requireAdjacent`, `startCondition.kind`. 이들은 `EffectData`가 아니다.

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
`scaling`을 소비 효과에 쓰지 않으므로 동작이 같다.

### 저작 층 변경 (`Assets/Core/Authoring`)

1. `EffectSpec.cs`: `Requires` → `Requirement`, `ScaleBy` → `Scaling` (타입 `EffectRequirementSpec`·
   `EffectScalingSpec` 그대로, `JsonProperty Order` 52·53 그대로). `ToEffectData()`(`:111-124`)의 대입도 맞춘다.
   **`EffectValue`는 기반 클래스에 두지 않는다** — 세기 숫자가 없는 스펙에 필드가 생기면
   `MissingMemberHandling.Error`가 하던 거부가 사라진다(P1 경계 사례).
2. 세기 숫자가 있는 스펙(`DamageSpec`, `ApplyStatusSpec`, `ConsumeStatusSpec`, `GrantNextTurnFateSpec`,
   `GrantNextDamageCardBonusSpec`, `MoveFormationSpec`)은 `Value`/`Count`/`Amount`를 지우고
   `[JsonProperty(Order = -18)] public int EffectValue;`를 선언한다. `Build()`는 `new EffectData(Key, EffectValue)`.
   필드 주석(예: `ApplyStatusSpec.cs:15-16`의 세기/지속 설명)은 새 필드로 옮긴다.
3. 페이로드가 있는 스펙은 재료 필드(`Status`, `Mode`, `Traits`)를 지우고 `[JsonProperty(Order = 40)] public
   XxxPayloadSpec Payload;`를 선언한다. 페이로드 스펙은 코어 레코드마다 하나, 같은 파일에 둔다:
   - `ApplyStatusPayloadSpec { public StatusKeyRef Key; }`
   - `ConsumeStatusPayloadSpec { public StatusKeyRef Key; [JsonProperty(DefaultValueHandling = Include)] public ConsumptionMode Mode; }`
     (`ConsumeStatusSpec.cs:16-19`의 "항상 쓴다" 이유 주석을 옮긴다)
   - `TriggerStatusPayloadSpec { public StatusKeyRef Key; }`
   - `DamagePayloadSpec { public DamageTrait[] Traits; }` — `DamageSpec`은 `Payload`가 null이거나 속성이 비면
     코어 페이로드를 만들지 않는다(현행 `:27`과 같음). `ShouldSerializePayload()`로 빈 페이로드를 쓰지 않는다.
4. 스펙 `Validate`: `Payload == null`이면 apply·consume·trigger는 "<key> requires a payload." 오류.
   `Status.IsEmpty`·`HasStatus` 검사는 `Payload.Key`로, `Amount < 1`은 `EffectValue < 1`로 옮긴다
   (`ConsumeStatusSpec.cs:41-44` → "consume_status effectValue must be at least 1."). damage는
   `DamageTraitRules.Validate(Payload?.Traits, ...)`.
5. `EffectSpecJsonConverter.cs`: `KindProperty` → `KeyProperty = "key"`, 오류 메시지의 "kind"도 "key"로.
   지역 변수·사전 이름(`FactoryByKind` 등)도 `Key`로 맞춘다.
6. `AuthoringValidator.cs:112-123`: `effect.Requires`/`ScaleBy` → `Requirement`/`Scaling`, 오류 문구의
   필드 이름 `"requires"`/`"scaleBy"` → `"requirement"`/`"scaling"`.
7. 그 밖에 `Requires`·`ScaleBy`·스펙 숫자 필드를 읽는 곳은 컴파일 오류로 찾는다(조사 시점 비테스트 코드에는
   위 6곳 외에 없음. `Assets/Unity`는 스펙을 참조하지 않는다).

### 형식 버전과 마이그레이션

1. `CardSpec.CurrentFormat = 3`.
2. `CardFormatMigration`에 v2→v3 단계를 더한다(`UpgradeV2ToV3(JObject card)`): 위 v2→v3 표를 기계적으로
   적용한다. 페이로드 이동 표(`apply_status`·`trigger_status`: `status`→`payload.key`; `consume_status`:
   `status`→`payload.key`, `mode`→`payload.mode`, `amount`→`effectValue`; `damage`: `traits`→`payload.traits`)는
   v2 형식의 일부라 새 효과가 늘어도 자라지 않는다(`CardFormatMigration.cs:8-12`의 기존 원칙과 같음).
3. 기존 v1 단계(`Upgrade`)는 끝에 `cardFormat = 2`를 쓰도록 고정하고(`:33`의 `CurrentFormat` 대신 상수 2),
   이어서 v2→v3를 적용한다.
4. `CardSpecJsonConverter.ReadJson`(`:42-51`): 없음 → v1→v2→v3, `2` → v2→v3, `3` → 그대로, 그 밖 → 거부.
5. 콘텐츠 35장을 v3로 다시 쓴다(`cardFormat: 3`). 변환 규칙은 2와 같다. 조작 카드 4장은 `cardFormat`만 바뀐다.

### 도구·문서

1. `Tools/graph/extract_card_graph.py`: `KNOWN_EFFECT_FIELDS`(`:21`)를 v3 필드 집합
   (`key`, `id`, `targetFaction`, `effectValue`, `successEffectValue`, `skipOnBasic`, `requirement`, `scaling`,
   `payload`)으로, 효과 종류는 `eff.get("key")`(`:122`), 상태 키는 `eff.get("payload", {}).get("key")`(`:135`)로 읽는다.
   `Tools/graph/test_extract_card_graph.py`의 인라인 효과 JSON(`:67-84`)도 v3로 맞춘다.
2. `docs/agents/content-authoring.md` 「AI 세션에서 JSON을 직접 저작한다」 절에 P2를 한 문단으로 더하고
   이 문서를 링크한다.
3. 구현 완료 커밋에서: `docs/superpowers/specs/2026-07-19-open-card-authoring-design.md` 머리말에
   "**2026-10-17 후속:**" 한 줄로 대체 관계를 적는다. `docs/superpowers/README.md`에서 이 문서 행의 상태를
   `active` → `current`로 바꾸고, 열린 카드 저작 행의 적용 범위 "(JSON `kind` 판별자와 컨버터)"를 이 문서로 넘긴다.
   (이 문서 행은 설계 커밋에서 `active`로 이미 더했다.)

### 테스트

- 새 테스트는 쓰지 않는다(사용자 결정 4). AGENTS.md 규칙 12("새 규칙 로직에는 헤드리스 테스트가 필수다")와
  긴장이 있다 — 소비 개수에 성공 수치·`scaling`이 적용되는 것은 새 동작이다. 사용자 결정을 따르고 여기 기록한다.
- 기존 테스트 수정: `ConsumeStatusPayload` 생성자(3인자→2인자), 스펙 객체 초기화(`Count =`·`Amount =`·
  `Value =`·`Status =`·`Requires =`·`ScaleBy =`), 인라인 JSON 문자열(`kind`·`count`·`value`·`amount`·`scaleBy` 등).
  조사 시점 후보 25개 파일(`Assets/Core/Tests/EditMode/`)이며 확정은 컴파일 오류와 `Tools/verify.sh`로 한다.
  인라인 JSON 중 **카드 전체**(`cardFormat: 2` 또는 형식 없음)는 카드 컨버터의 마이그레이션을 거치므로 계속
  읽힌다 — 기대값만 확인한다. **효과 한 줄만** `EffectSpecJsonConverter`로 직접 읽는 JSON은 마이그레이션을
  거치지 않으므로 v3로 고친다.
- `CardFormatContractTests`·`CardContentJsonTests`의 형식 번호·왕복 기대값을 3으로 맞춘다.

### 실행 순서

1. 코어 변경 1~5 → 저작 층 1~7 → 형식·마이그레이션 1~4 → 테스트 수정 → `Tools/verify.sh` 통과.
2. 콘텐츠 35장 v3 재작성 → `Tools/verify.sh` 통과(부팅 로드가 전 카드를 검증).
3. 도구·문서 1~3 → `cd Tools/graph && python3 -m unittest test_extract_card_graph` 통과
   (테스트는 `unittest`이고 `extract_card_graph`를 같은 폴더에서 import한다, `test_extract_card_graph.py:3-6`).
4. 커밋은 단계별로 나눈다(코어+저작+마이그레이션+테스트 / 콘텐츠 / 도구·문서). 커밋 메시지 규칙 27.

### 범위 밖과 후속

- 세기 숫자가 없는 효과에 `successEffectValue`·`scaling`을 적으면 여전히 조용히 무시된다(기반 클래스 필드라
  `MissingMemberHandling`이 막지 못함). 이 작업은 `effectValue`만 막는다. 검증 추가는 후속 과제다.
- 카드 수준·조작 카드·조건의 `kind`와 필드 이름.
- 상태·적·편성·캐릭터 JSON.
