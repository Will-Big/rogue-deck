# 효과·조작 JSON 페이로드 중첩 구현 계획

사람 검토용 개요: [HTML](2026-10-17-effect-json-payload-nesting.html)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

## 상세

**Goal:** 효과·조작 JSON의 이름과 구조를 코어 레코드와 같게 만들고(페이로드 중첩), 카드 형식 버전과 구형 변환을 없애며, `consume_status`의 개수를 `EffectValue`로 옮긴다.

**Architecture:** 코어(`Assets/Core/Effects`)는 `ConsumeStatusPayload`에서 `Amount`만 뺀다. 저작 층(`Assets/Core/Authoring`)은 스펙 필드를 코어 이름(`EffectValue`, `Payload`, `Requirement`, `Scaling`)으로 바꾸고, 코어 페이로드 레코드마다 거울 페이로드 스펙을 둔다. 판별자 JSON 이름은 `key`. 콘텐츠 35장은 일회성 파이썬 변환으로 다시 쓴다.

**Tech Stack:** C# 9(Unity 6, 헤드리스는 net6.0), Newtonsoft.Json(`ContentJson`), NUnit, Python 3(`unittest`, 변환 스크립트).

**Spec:** [`docs/superpowers/specs/2026-10-17-effect-json-payload-nesting-design.md`](../specs/2026-10-17-effect-json-payload-nesting-design.md) (개요 [HTML](../specs/2026-10-17-effect-json-payload-nesting-design.html)). 실행 근거는 그 `.md`의 `## 상세`와 이 계획이다. 둘이 어긋나면 멈추고 묻는다(AGENTS.md 규칙 29).

## Global Constraints

- 작업 위치: 워크트리 `/Users/ish/Git/rogue-deck/.claude/worktrees/korean-comments` (브랜치 `docs/korean-comments-cards-effects`). 메인 체크아웃의 브랜치를 바꾸지 않는다(규칙 15).
- **새 테스트를 쓰지 않는다**(사용자 결정 "테스트를 추가할 필요는 없고"). 기존 테스트는 컴파일·통과를 위해 고치거나, 폐지된 기능(형식 버전·구형 변환)만 검증하는 것은 지운다.
- 원칙 P1: 효과의 세기 숫자는 `EffectData.EffectValue`, 종류 전용 선택값만 페이로드. 숫자가 없는 효과 스펙에는 `EffectValue` 필드를 두지 않는다(기반 클래스에도 두지 않는다).
- 원칙 P2: 효과·조작 JSON 이름 = 코어 속성 이름의 camelCase. 판별자는 `key`. 시작 조건의 `kind`와 카드 수준 필드는 바꾸지 않는다.
- 원칙 P3: 카드 JSON은 형식 버전을 갖지 않는다. 구형 변환 코드를 남기지 않는다.
- JSON 필드 순서(`JsonProperty Order`): `EffectSpec` 기반 `Id` -20, `TargetFaction` -19, 파생 `EffectValue` **-18**, 기반 `SuccessEffectValue` 50, `SkipOnBasic` 51, `Requirement` 52, `Scaling` 53, 파생 `Payload` **60**. 판별자 `key`는 컨버터가 맨 앞에 붙인다.
- 검증 명령: `Tools/verify.sh`(전체, 약 10초+). 모든 태스크의 끝에서 통과해야 커밋한다. Stop 훅이 `Assets/Core` 변경 시 강제한다.
- 커밋 메시지: 한국어, `타입(범위): 제목`, 제목은 `-ㄴ다`로 끝난다(규칙 27). 끝에 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- 셸: 맨 `grep`이 실패할 수 있으니 `/usr/bin/grep`을 쓴다. 맨 `git stash` 금지.
- `$SCRATCH`: 실행 세션의 스크래치패드 디렉터리(시스템 안내에 경로가 있다). 변환 스크립트는 여기에만 두고 커밋하지 않는다.
- 콘텐츠 JSON 쓰기 형식: `json.dumps(obj, indent=2, ensure_ascii=False) + "\n"` — 현재 35장 모두 이 형식과 바이트가 같다(2026-10-17 확인). `CardContentJsonTests.Repository_cards_round_trip_byte_identically`(`:282`)가 이것을 잠근다.

## Review Focus

새 테스트를 쓰지 않으므로(사용자 결정), 각 항목은 **기존 테스트 또는 태스크 안의 수동 확인 단계**로 확인한다.

1. 옛 형식 카드 JSON(`"kind"`, `"count"`, `"cardFormat"`)을 넣으면 조용히 로드되지 않고 오류가 나야 한다 → Task 2·3·4의 수동 확인 단계(임시 파일로 부팅 로드), 기존 `CardContentJsonTests.Execution_card_rejects_intervention_keys`(`:252`)·`Swap_spec_rejects_a_parameter_it_does_not_own`(`:273`)가 같은 메커니즘(`MissingMemberHandling.Error`)을 고정한다.
2. 숫자가 없는 효과(`trigger_status`)에 `"effectValue"`를 적으면 거부돼야 한다 → Task 3 수동 확인 단계.
3. `consume_status`에 `effectValue`를 빠뜨리면(0으로 읽힘) "effectValue must be at least 1"로 거부돼야 한다 → Task 3 수동 확인 단계, 코어 쪽은 `ConsumeStatusHandler.ValidateData`.
4. 페이로드가 필요한 효과·조작에 `payload`가 없으면 `NullReferenceException`이 아니라 검증 오류여야 한다 → Task 3·4 수동 확인 단계(`Validate`가 `Build`/`ToPayload`보다 먼저 돈다: `CardContentLoader.cs:85-100`).
5. 다시 쓴 콘텐츠의 키 순서가 직렬화 결과와 같아야 한다 → `Repository_cards_round_trip_byte_identically`, `Every_card_preserves_its_canonical_serialized_definition`.

---

### Task 1: consume_status의 개수를 EffectValue로 옮긴다 (JSON 형식은 그대로)

이 태스크만으로 동작·형식이 닫힌다: JSON은 여전히 `"amount"`이고 스펙이 그것을 `EffectValue`로 옮긴다.

**Files:**
- Modify: `Assets/Core/Effects/ConsumeStatusPayload.cs:5-9`
- Modify: `Assets/Core/Effects/ConsumeStatusHandler.cs:33,62-65`
- Modify: `Assets/Core/Effects/IEffectPayload.cs:3-5`
- Modify: `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs:64`
- Modify: `Assets/Core/Authoring/Specs/ConsumeStatusSpec.cs:27-28`
- Modify(tests): `Assets/Core/Tests/EditMode/ConsumeStatusTests.cs:45-51`, `Assets/Core/Tests/EditMode/CombatLogTests.cs:519,559`

**Interfaces:**
- Produces: `public sealed record ConsumeStatusPayload(StatusKey Key, ConsumptionMode Mode) : IEffectPayload;` — 소비 개수는 `EffectData.EffectValue`.

- [ ] **Step 1: 코어 페이로드에서 Amount를 뺀다**

`ConsumeStatusPayload.cs` 5-9행을 다음으로 바꾼다:

```csharp
    /// <summary>consume_status 파라미터: 대상 적의 상태를 Mode 방식으로 제거한다. 몇 개를 제거할지는
    /// 세기 숫자라 EffectData.EffectValue에 있다 — 조건 성공 수치와 scaling이 그 칸에만 적용되기
    /// 때문이다. 실제 소비량은 이 효과의 결과(EffectResult.ConsumedAmount)가 되며, 뒤 효과가 효과 ID로
    /// 읽는다(전투 실행 계약 스펙 §5).</summary>
    public sealed record ConsumeStatusPayload(StatusKey Key, ConsumptionMode Mode) : IEffectPayload;
```

- [ ] **Step 2: 핸들러가 ctx.EffectValue를 읽게 한다**

`ConsumeStatusHandler.cs:33`:

```csharp
            var consumed = ConsumptionRule.Take(available, ctx.EffectValue, payload.Mode);
```

`ConsumeStatusHandler.cs:62-65`:

```csharp
            if (effect.EffectValue < 1)
            {
                yield return "consume_status EffectValue must be at least 1.";
            }
```

- [ ] **Step 3: 설명 핸들러가 넘겨받은 값을 쓰게 한다**

`BuiltInEffectDescriptionHandlers.cs:64`의 `("amount", payload.Amount)`를 `("amount", value)`로 바꾼다. 줄의 나머지는 그대로다. `"amount"`는 현지화 자리표시자 이름이라 유지한다.

- [ ] **Step 4: IEffectPayload 주석에 원칙 P1을 더한다**

`IEffectPayload.cs` 3-5행:

```csharp
    /// <summary>EffectData가 싣고 다니는 효과 종류별 매개변수 묶음. 공통 스칼라 수치 외에 매개변수가
    /// 필요한 효과 키는 저마다 자기 페이로드 레코드를 선언한다. 공통 모델에는 효과별 필드를
    /// 늘리지 않는다(AGENTS.md 규칙 9). 효과의 세기 숫자("얼마나")는 페이로드가 아니라
    /// EffectData.EffectValue에 둔다 — 조건 성공 수치 교체와 scaling 가산이 그 칸에만 적용된다.
    /// 페이로드에는 그 종류만 쓰는 선택값("무엇을, 어떤 식으로")만 둔다.</summary>
```

- [ ] **Step 5: 스펙이 개수를 EffectValue로 넣게 한다**

`ConsumeStatusSpec.cs:27-28`:

```csharp
        protected override EffectData Build()
            => new EffectData(Key, Amount) { Payload = new ConsumeStatusPayload(Status.ToKey(), Mode) };
```

- [ ] **Step 6: 테스트 생성 코드를 고친다**

`ConsumeStatusTests.cs:45-51`:

```csharp
        private static EffectData Consume(string id, int amount, ConsumptionMode mode)
            => new EffectData(EffectKeys.ConsumeStatus, amount)
            {
                Id = id,
                TargetFaction = CardTargetFaction.Enemy,
                Payload = new ConsumeStatusPayload(StatusKeys.Poison, mode)
            };
```

`CombatLogTests.cs:519`과 `:559`의 효과 생성:

```csharp
                new EffectData(EffectKeys.ConsumeStatus, 3) { TargetFaction = CardTargetFaction.Enemy, Payload = new ConsumeStatusPayload(StatusKeys.Poison, 0) }
```

(`0`은 기존 3번째 인자였던 `ConsumptionMode` 값이다. 기존 의미를 유지한다.)

- [ ] **Step 7: 남은 참조를 찾는다**

Run: `/usr/bin/grep -rn "ConsumeStatusPayload(" Assets/Core | /usr/bin/grep -v "record ConsumeStatusPayload"`
Expected: 모든 호출이 2인자다. 3인자가 남았으면 같은 방식으로 고친다.

- [ ] **Step 8: 검증**

Run: `Tools/verify.sh`
Expected: `모두 통과`. 실패하면 메시지의 파일을 고친다(동작은 바뀌지 않아야 한다 — 기존 소비 테스트가 그대로 통과해야 한다).

- [ ] **Step 9: 커밋**

```bash
git add Assets/Core/Effects/ConsumeStatusPayload.cs Assets/Core/Effects/ConsumeStatusHandler.cs Assets/Core/Effects/IEffectPayload.cs Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs Assets/Core/Authoring/Specs/ConsumeStatusSpec.cs Assets/Core/Tests/EditMode/ConsumeStatusTests.cs Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "refactor(core): consume_status의 개수를 페이로드에서 EffectValue로 옮긴다

세기 숫자는 EffectValue, 종류 전용 선택값은 페이로드라는 원칙(효과 JSON 설계 P1)에 맞춰
ConsumeStatusPayload에서 Amount를 뺀다. 핸들러·검증·설명이 EffectValue를 읽으므로 조건 성공
수치와 scaling이 소비 개수에 적용된다. JSON 형식은 아직 그대로다.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: 카드 형식 버전과 구형 변환을 지운다

**Files:**
- Delete: `Assets/Core/Authoring/Json/CardFormatMigration.cs`, `Assets/Core/Authoring/Json/CardFormatMigration.cs.meta`
- Modify: `Assets/Core/Authoring/CardSpec.cs:18-24`
- Modify: `Assets/Core/Authoring/Json/CardSpecJsonConverter.cs:12,16,42-51`
- Modify: `Assets/Core/Authoring/AuthoringValidator.cs:31-34`
- Modify: `Assets/StreamingAssets/Content/Cards/*.json` (35장, `"cardFormat"` 줄 제거)
- Modify(tests): `CardFormatContractTests.cs`, `CardContentJsonTests.cs:116-122,317`, `StatusDamageTraitsTests.cs:135`, 그 밖에 컴파일·실행 오류가 가리키는 파일

**Interfaces:**
- Produces: `CardSpec`에 `CurrentFormat`/`CardFormat`이 없다. `CardSpecJsonConverter`에 `FormatProperty`가 없다.

- [ ] **Step 1: 마이그레이션 파일을 지운다**

```bash
git rm Assets/Core/Authoring/Json/CardFormatMigration.cs Assets/Core/Authoring/Json/CardFormatMigration.cs.meta
```

- [ ] **Step 2: CardSpec의 형식 필드를 지운다**

`CardSpec.cs` 18-24행(`CurrentFormat` 상수의 summary부터 `public int CardFormat = CurrentFormat;`까지)을 지운다. 바로 위 클래스 summary(6-15행)는 그대로 둔다.

- [ ] **Step 3: 컨버터의 형식 분기를 지운다**

`CardSpecJsonConverter.cs`:
- 12행 `/// cardFormat이 없는 구형 파일은 여기서 CardFormatMigration이 현재 형식으로 올린다.</summary>`를 지우고, 11행 끝에 `</summary>`를 붙인다.
- 16행 `public const string FormatProperty = "cardFormat";`을 지운다.
- 42-51행(`var format = entry[FormatProperty];`부터 거부 `throw`의 `}`까지)을 지운다. `create()` 호출(53행)은 남긴다.

- [ ] **Step 4: 검증기의 형식 검사를 지운다**

`AuthoringValidator.cs` 31-34행(`if (spec.CardFormat != CardSpec.CurrentFormat)` 블록)을 지운다.

- [ ] **Step 5: 변환 스크립트를 스크래치패드에 만든다 (저장소에 커밋하지 않는다)**

세션 스크래치패드(예: `$SCRATCH/convert_cards.py`)에 다음을 저장한다. Task 3·4도 같은 스크립트를 쓴다.

```python
"""카드 JSON 일회성 변환. 사용: python3 convert_cards.py <Cards 폴더> <format|effects|interventions>"""
import glob
import json
import os
import sys

NUMBER_FIELD = {
    "damage": "value",
    "apply_status": "count",
    "consume_status": "amount",
    "grant_next_turn_fate": "value",
    "grant_next_player_damage_card_bonus": "value",
    "move_formation": "value",
}
PAYLOAD_FIELDS = {
    "apply_status": [("status", "key")],
    "trigger_status": [("status", "key")],
    "consume_status": [("status", "key"), ("mode", "mode")],
    "damage": [("traits", "traits")],
}
TAIL = [("successEffectValue", "successEffectValue"), ("skipOnBasic", "skipOnBasic"),
        ("requires", "requirement"), ("scaleBy", "scaling")]
INTERVENTION_PAYLOAD = {
    "change_execution_order": ["delta", "targetSide"],
    "swap_execution_order": ["targetSide", "requireAdjacent"],
    "lock": [],
}


def convert_effect(e, path):
    kind = e["kind"]
    number = NUMBER_FIELD.get(kind)
    payload_fields = PAYLOAD_FIELDS.get(kind, [])
    known = {"kind", "id", "targetFaction"} | {s for s, _ in TAIL} | {s for s, _ in payload_fields}
    if number:
        known.add(number)
    unknown = set(e) - known
    if unknown:
        raise SystemExit(f"{path}: {kind} 효과의 알 수 없는 필드 {sorted(unknown)}")
    out = {"key": kind}
    for field in ("id", "targetFaction"):
        if field in e:
            out[field] = e[field]
    if number and number in e:
        out["effectValue"] = e[number]
    for src, dst in TAIL:
        if src in e:
            out[dst] = e[src]
    payload = {dst: e[src] for src, dst in payload_fields if src in e}
    if payload:
        out["payload"] = payload
    return out


def convert_intervention(i, path):
    kind = i["kind"]
    fields = INTERVENTION_PAYLOAD[kind]
    unknown = set(i) - {"kind"} - set(fields)
    if unknown:
        raise SystemExit(f"{path}: {kind} 조작의 알 수 없는 필드 {sorted(unknown)}")
    out = {"key": kind}
    if fields:
        out["payload"] = {f: i[f] for f in fields if f in i}
    return out


def main(root, step):
    paths = sorted(glob.glob(os.path.join(root, "*.json")))
    for path in paths:
        with open(path, encoding="utf-8") as f:
            card = json.load(f)
        if step == "format":
            card.pop("cardFormat", None)
        elif step == "effects":
            if "effects" in card:
                card["effects"] = [convert_effect(e, path) for e in card["effects"]]
        elif step == "interventions":
            if "intervention" in card:
                card["intervention"] = convert_intervention(card["intervention"], path)
        else:
            raise SystemExit("step must be format|effects|interventions")
        with open(path, "w", encoding="utf-8") as f:
            f.write(json.dumps(card, indent=2, ensure_ascii=False) + "\n")
    print(f"{step}: {len(paths)} files")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
```

- [ ] **Step 6: 콘텐츠에서 cardFormat을 지운다**

Run: `python3 $SCRATCH/convert_cards.py Assets/StreamingAssets/Content/Cards format`
Expected: `format: 35 files`. 이어서 `/usr/bin/grep -l cardFormat Assets/StreamingAssets/Content/Cards/*.json` 결과가 비어 있다.

- [ ] **Step 7: 형식·구형 변환 전용 테스트를 지우고 나머지를 고친다**

- `CardFormatContractTests.cs`에서 다음 테스트 메서드를 통째로 지운다: `Rejects_an_unsupported_card_format`(`:134` 부근), `Legacy_card_upgrades_by_rule`(`:157`), `Legacy_card_loads_to_the_same_definition_as_its_upgraded_form`(`:182`), `Legacy_card_with_different_conditions_is_rejected_not_merged`(`:198`), `Legacy_explicit_party_member_target_is_rejected`(`:216` 부근), `Legacy_enemy_card_keeps_its_own_self_and_attacks_the_party`(`:228` 부근). 이 메서드들만 쓰던 private 헬퍼·상수도 지운다. 남는 테스트(`:47-133`)의 인라인 JSON에서 `"cardFormat": 2,` 줄을 지운다.
- `CardContentJsonTests.cs`: `CardFormatIsWrittenFirst`(`:116-122`)를 지우고, `Every_card_preserves_its_canonical_serialized_definition`의 `:317` 단언 한 줄을 지운다. 그 위 주석(`:302-303`)의 "구형 변환 테스트가 따로 있다"를 "로딩 검증 테스트가 따로 있다"로 고친다.
- `StatusDamageTraitsTests.cs:135`의 `""cardFormat"": 2,` 줄을 지운다.

- [ ] **Step 8: 컴파일·실행 오류로 남은 곳을 찾는다**

Run: `Tools/verify.sh`
Expected: 처음에는 실패할 수 있다. 원인별 처리:
- `CardFormat`/`CurrentFormat`/`CardFormatMigration` 컴파일 오류 → 그 참조를 지운다.
- 인라인 카드 JSON에 `cardFormat`이 남아 `Could not find member 'cardFormat'` → 그 줄을 지운다.
- 형식 없는 인라인 카드 JSON이 예전에는 구형 변환을 거쳐 통과하던 경우(예: 효과에 `id`가 없음, 효과별 `selector`/`condition` 같은 구형 필드) → 그 JSON을 현재 형식(효과 `id` 명시, 카드 `targets`·`startCondition`)으로 고친다. 후보: `DescriptionComposerTests.cs`, `AuthoringValidationTests.cs`, `CardContentLoaderTests.cs`(형식 없는 인라인 카드 JSON을 가진 파일).
다시 실행해 `모두 통과`가 나올 때까지 반복한다.

- [ ] **Step 9: 옛 형식이 거부되는지 수동으로 확인한다 (Review Focus 1)**

Run: `/usr/bin/grep -rn "cardFormat" Assets/Core Assets/StreamingAssets`
Expected: 결과 없음. (`cardFormat`이 있는 JSON은 이제 `MissingMemberHandling.Error`로 거부된다 — 스펙에 그 필드가 없다.)

- [ ] **Step 10: 커밋**

```bash
git add -A Assets/Core/Authoring Assets/Core/Tests Assets/StreamingAssets/Content/Cards
git commit -m "refactor(authoring): 카드 형식 버전과 구형 변환을 없앤다

카드 JSON은 항상 최신 형식 하나로만 둔다(효과 JSON 설계 P3). CardFormatMigration과
CardSpec.CardFormat·CurrentFormat, 컨버터의 형식 분기, 검증기의 형식 검사를 지우고 카드
35장에서 cardFormat을 뺀다. 형식 버전·구형 변환만 검증하던 테스트를 지운다.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: 효과 JSON을 EffectData 이름으로 바꾸고 페이로드를 중첩한다

**Files:**
- Modify: `Assets/Core/Authoring/EffectSpec.cs:70-124`
- Modify: `Assets/Core/Authoring/Specs/ApplyStatusSpec.cs`, `ConsumeStatusSpec.cs`, `TriggerStatusSpec.cs`, `DamageSpec.cs`, `GrantNextTurnFateSpec.cs`, `GrantNextDamageCardBonusSpec.cs`, `MoveFormationSpec.cs`
- Modify: `Assets/Core/Authoring/Json/EffectSpecJsonConverter.cs`
- Modify: `Assets/Core/Authoring/AuthoringValidator.cs:112-123`
- Modify: `Assets/StreamingAssets/Content/Cards/*.json` (실행 카드 31장의 `effects`)
- Modify(tests): 컴파일·실행 오류가 가리키는 파일(조사 시점 후보: `AuthoringValidationTests`, `CardContentJsonTests`, `CardContentLoaderTests`, `CardFormatContractTests`, `CardSpecMapperTests`, `DescriptionCatalogValidatorTests`, `DescriptionComposerTests`, `DescriptionRegistryTests`, `EnemyContentLoaderTests`, `GoblinParityTests`, `LocalizedDescriptionComposerTests`, `LocalizedTextRequestTests`, `NewEffectLocalityTests`, `NewEffectSpecTests`, `PartyDescriptionTests`, `SampleScenarioTests`, `SlowHasteStatusTests`, `StarterPoolDescriptionTests`, `StarterPoolPoisonTests`, `StatusDamageTraitsTests`, `StructuredCardDescriptionTests`, `DeckPoolCharacterLoaderTests`)

**Interfaces:**
- Consumes: Task 1의 `ConsumeStatusPayload(StatusKey Key, ConsumptionMode Mode)`.
- Produces:
  - `EffectSpec.Requirement : EffectRequirementSpec`, `EffectSpec.Scaling : EffectScalingSpec` (옛 `Requires`/`ScaleBy`)
  - 숫자 있는 스펙의 `public int EffectValue;`
  - `ApplyStatusPayloadSpec { StatusKeyRef Key; }`, `ConsumeStatusPayloadSpec { StatusKeyRef Key; ConsumptionMode Mode; }`, `TriggerStatusPayloadSpec { StatusKeyRef Key; }`, `DamagePayloadSpec { DamageTrait[] Traits; }`
  - `EffectSpecJsonConverter.KeyProperty = "key"` (옛 `KindProperty`)

- [ ] **Step 1: EffectSpec 기반 필드 이름을 바꾼다**

`EffectSpec.cs` 75행 주석의 `(requires·scaleBy)`를 `(requirement·scaling)`으로 바꾼다. 92-96행:

```csharp
        [JsonProperty(Order = 52)]
        public EffectRequirementSpec Requirement;

        [JsonProperty(Order = 53)]
        public EffectScalingSpec Scaling;
```

105행 주석의 `requires·scaleBy는`을 `requirement·scaling은`으로 바꾼다. 118-123행:

```csharp
                Requirement = Requirement == null
                    ? null
                    : new EffectResultRequirement(Requirement.SourceEffectId, Requirement.MinimumConsumed),
                Scaling = Scaling == null
                    ? null
                    : new EffectResultScaling(Scaling.SourceEffectId, Scaling.PerConsumed)
```

(`with` 초기화자 왼쪽은 `EffectData`의 속성, 오른쪽은 이 스펙의 필드다. 이름이 같아도 C#은 구분한다.)

- [ ] **Step 2: ApplyStatusSpec**

`ApplyStatusSpec.cs` 전체를 다음으로 바꾼다:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>카드 위치 규칙이 고른 대상에게 상태를 건다. 어느 진영인지는 TargetFaction이, 어느 위치인지는
    /// 카드의 그 진영 축이 정한다(스펙 §4). 적 카드가 파티에 거는 상태처럼 아직 쓰는 콘텐츠가 없는 조합은
    /// 로딩에서 거부한다(ValidateTarget).</summary>
    public sealed class ApplyStatusSpec : EffectSpec
    {
        /// <summary>이 카드가 거는 양. 뜻은 상태가 정한다 — 수명이 Permanent·ThisTurn이면 세기,
        /// Turns·UntilConsumed면 지속.</summary>
        [JsonProperty(Order = -18)]
        public int EffectValue;

        [JsonProperty(Order = 60)]
        public ApplyStatusPayloadSpec Payload;

        public override EffectKey Key => EffectKeys.ApplyStatus;

        public override bool IsTargeted => true;

        protected override EffectData Build()
            => new EffectData(Key, EffectValue) { Payload = new ApplyStatusPayload(Payload.Key.ToKey()) };

        /// <summary>저작 콘텐츠 존재 여부는 검사하지 않는다. StatusContentLoader가 "등록된 모든
        /// 상태에 저작이 있다"를 요구하고 부팅이 상태를 카드보다 먼저 읽으므로, 여기 도달한
        /// 시점에는 HasStatus가 곧 저작 존재다 — 가드를 두면 같은 불변식을 두 곳에서 지키게 된다.</summary>
        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "apply_status requires a payload.";
                yield break;
            }

            if (Payload.Key.IsEmpty)
            {
                yield return "apply_status payload requires a status key.";
            }
            else if (!context.HasStatus(Payload.Key.ToKey()))
            {
                yield return "Unknown status key '" + Payload.Key.Id + "'.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
        {
            if (target.Range == CardTargetRange.Self)
            {
                if (target.Faction != OwnFaction(cardSide))
                {
                    yield return "apply_status Self must be on the card's own side (" + OwnFaction(cardSide) + ").";
                }

                yield break;
            }

            if (cardSide == Side.Enemy)
            {
                yield return "apply_status on an enemy card supports only Self for now.";
            }
        }
    }

    /// <summary>ApplyStatusPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class ApplyStatusPayloadSpec
    {
        public StatusKeyRef Key;
    }
}
```

- [ ] **Step 3: ConsumeStatusSpec**

`ConsumeStatusSpec.cs` 전체를 다음으로 바꾼다:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>적 진영 위치 규칙이 고른 적의 상태를 EffectValue만큼 Mode 방식으로 소비한다(스펙 §5).
    /// 실제 소비량은 이 효과의 결과가 되고, 뒤 효과가 requirement·scaling으로 이 효과의 id를 가리켜 읽는다.</summary>
    public sealed class ConsumeStatusSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        [JsonProperty(Order = 60)]
        public ConsumeStatusPayloadSpec Payload;

        public override EffectKey Key => EffectKeys.ConsumeStatus;

        public override bool IsTargeted => true;

        public override bool ProducesConsumption => true;

        protected override EffectData Build()
            => new EffectData(Key, EffectValue)
            {
                Payload = new ConsumeStatusPayload(Payload.Key.ToKey(), Payload.Mode)
            };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (EffectValue < 1)
            {
                yield return "consume_status effectValue must be at least 1.";
            }

            if (Payload == null)
            {
                yield return "consume_status requires a payload.";
                yield break;
            }

            if (Payload.Key.IsEmpty)
            {
                yield return "consume_status payload requires a status key.";
            }
            else if (!context.HasStatus(Payload.Key.ToKey()))
            {
                yield return "Unknown status key '" + Payload.Key.Id + "'.";
            }

            if (!Enum.IsDefined(typeof(ConsumptionMode), Payload.Mode))
            {
                yield return "consume_status mode must be UpTo or Exact.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, CardTargetFaction.Enemy);
    }

    /// <summary>ConsumeStatusPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class ConsumeStatusPayloadSpec
    {
        public StatusKeyRef Key;

        /// <summary>UpTo가 enum 기본값이라 생략되면 조용히 UpTo가 된다. 소비 방식은 카드의 뜻을 가르므로
        /// 항상 쓴다.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public ConsumptionMode Mode;
    }
}
```

- [ ] **Step 4: TriggerStatusSpec**

`TriggerStatusSpec.cs` 전체를 다음으로 바꾼다:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 적의 상태 틱을 즉시 발동시키고 이번 턴 종료 발동을 막는다 (조기 발병). 어떤
    /// 마커로 막는지는 상태 자신의 behavior가 안다(StatusBehavior.SuppressThisTurn) — 카드는 더 이상
    /// 마커 키를 저작하지 않는다. 세기 숫자가 없으므로 EffectValue 필드를 두지 않는다(효과 JSON 설계 P1).</summary>
    public sealed class TriggerStatusSpec : EffectSpec
    {
        [JsonProperty(Order = 60)]
        public TriggerStatusPayloadSpec Payload;

        public override EffectKey Key => EffectKeys.TriggerStatus;

        public override bool IsTargeted => true;

        protected override EffectData Build()
            => new EffectData(Key, 0) { Payload = new TriggerStatusPayload(Payload.Key.ToKey()) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "trigger_status requires a payload.";
                yield break;
            }

            if (Payload.Key.IsEmpty || !context.HasStatus(Payload.Key.ToKey()))
            {
                yield return "trigger_status spec requires a known status key.";
            }
        }

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, CardTargetFaction.Enemy);
    }

    /// <summary>TriggerStatusPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class TriggerStatusPayloadSpec
    {
        public StatusKeyRef Key;
    }
}
```

- [ ] **Step 5: DamageSpec**

`DamageSpec.cs` 전체를 다음으로 바꾼다:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>상대 진영의 카드 위치 규칙이 고른 대상에게 고정 피해를 준다. Payload.Traits는 이 피해의
    /// 속성들이다(관통·배율 무시, 여러 개 가능 — 계획 D10). 생략하면 보통 피해다.</summary>
    public sealed class DamageSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        [JsonProperty(Order = 60)]
        public DamagePayloadSpec Payload;

        public override EffectKey Key => EffectKeys.Damage;

        public override bool IsTargeted => true;

        /// <summary>속성이 없으면 payload 자체를 쓰지 않는다 — 편집 도구와 같은 규칙.</summary>
        public bool ShouldSerializePayload() => Payload?.Traits != null && Payload.Traits.Length > 0;

        protected override EffectData Build()
        {
            var traits = DamageTraits.Of(Payload?.Traits);
            return new EffectData(Key, EffectValue)
            {
                Payload = traits.Equals(DamageTraits.Normal) ? null : new DamagePayload(traits)
            };
        }

        public override IEnumerable<string> Validate(AuthoringContext context)
            => DamageTraitRules.Validate(Payload?.Traits, "damage traits");

        public override IEnumerable<string> ValidateTarget(Side cardSide, CardTargetKey target)
            => RequirePositional(target, OpposingFaction(cardSide));
    }

    /// <summary>DamagePayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class DamagePayloadSpec
    {
        public DamageTrait[] Traits;
    }
}
```

`DamageTraits.Of(IEnumerable<DamageTrait>)`가 null을 받는지 확인한다(현행 `DamageSpec.cs:24`가 null일 수 있는 `Traits`를 그대로 넘기므로 받는다). `DamageTraitRules.Validate`도 현행과 같이 null을 받는다.

- [ ] **Step 6: 숫자만 있는 스펙 세 개**

`GrantNextTurnFateSpec.cs`:

```csharp
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>다음 플레이어 사용 턴에 운명력 EffectValue를 준다 (증류).</summary>
    public sealed class GrantNextTurnFateSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        public override EffectKey Key => EffectKeys.GrantNextTurnFate;

        protected override EffectData Build() => new EffectData(Key, EffectValue);
    }
}
```

`GrantNextDamageCardBonusSpec.cs`:

```csharp
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using Newtonsoft.Json;

namespace FateWeaver.Core.Authoring
{
    /// <summary>다음 플레이어 피해 카드에 피해 보너스 EffectValue를 준다.</summary>
    public sealed class GrantNextDamageCardBonusSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        public override EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        protected override EffectData Build() => new EffectData(Key, EffectValue);
    }
}
```

`MoveFormationSpec.cs` 7-16행:

```csharp
    /// <summary>대형을 EffectValue만큼 옮긴다(음수 = 앞으로). 대상은 카드 쪽 진영의 Self다.</summary>
    public sealed class MoveFormationSpec : EffectSpec
    {
        [JsonProperty(Order = -18)]
        public int EffectValue;

        public override EffectKey Key => EffectKeys.MoveFormation;

        public override bool IsTargeted => true;

        protected override EffectData Build() => new EffectData(Key, EffectValue);
```

그리고 `MoveFormationSpec.cs` 상단 using에 `using Newtonsoft.Json;`을 더한다. 사용하지 않게 된 `using FateWeaver.Core.Cards;`는 그 파일에서 여전히 쓰이면 남긴다(`Side`, `CardTargetKey`).

- [ ] **Step 7: 효과 컨버터 판별자를 key로 바꾼다**

`EffectSpecJsonConverter.cs`에서:
- `public const string KindProperty = "kind";` → `public const string KeyProperty = "key";`
- `FactoryByKind` → `FactoryByKey`, `KindByType` → `KeyByType`, `BuildKinds` → `BuildKeys`, 지역 변수 `kind` → `key`
- 메시지: `"Effect entry requires a '" + KeyProperty + "' property."`, `"Unknown effect key '" + key + "'."`, `"Duplicate effect spec key '" + key + "' in EffectSpecCatalog."`
- 클래스 summary의 "판별자는 스펙이 이미 갖고 있는 EffectKey.Id"는 그대로 두고, 끝에 "JSON 이름은 key다(효과 JSON 설계 P2)."를 더한다.

Run: `/usr/bin/grep -rn "EffectSpecJsonConverter.KindProperty" Assets`
Expected: 결과가 있으면 `KeyProperty`로 바꾼다.

- [ ] **Step 8: 검증기의 참조 이름을 바꾼다**

`AuthoringValidator.cs:112-123`:

```csharp
                ValidateReference(effects, indexById, i, "requirement", effect.Requirement?.SourceEffectId, at, errors);
                ValidateReference(effects, indexById, i, "scaling", effect.Scaling?.SourceEffectId, at, errors);

                if (effect.Requirement != null && effect.Requirement.MinimumConsumed < 1)
                {
                    errors.Add(at + "requirement.minimumConsumed must be at least 1.");
                }

                if (effect.Scaling != null && effect.Scaling.PerConsumed == 0)
                {
                    errors.Add(at + "scaling.perConsumed must not be 0.");
                }
```

- [ ] **Step 9: 콘텐츠 효과를 새 형식으로 다시 쓴다**

Run: `python3 $SCRATCH/convert_cards.py Assets/StreamingAssets/Content/Cards effects`
Expected: `effects: 35 files`. 알 수 없는 필드로 멈추면 그 필드를 설계 문서의 표와 대조해 스크립트에 추가하지 말고 멈춰서 보고한다(설계에 없는 필드다).

Run: `/usr/bin/grep -c '"kind"' Assets/StreamingAssets/Content/Cards/*.json | /usr/bin/grep -v ":0"`
Expected: 남는 `"kind"`는 `startCondition`(4장)과 `intervention`(4장)뿐이다.

- [ ] **Step 10: 테스트를 고친다**

Run: `Tools/verify.sh`
Expected: 컴파일 오류·실패가 난다. 원인별 처리(동작 기대값은 바꾸지 않는다):
- 스펙 객체 초기화: `new ApplyStatusSpec { Status = X, Count = n }` → `new ApplyStatusSpec { EffectValue = n, Payload = new ApplyStatusPayloadSpec { Key = X } }`. 같은 규칙으로 `ConsumeStatusSpec { Status, Amount, Mode }` → `{ EffectValue = Amount, Payload = new ConsumeStatusPayloadSpec { Key = Status, Mode = Mode } }`, `TriggerStatusSpec { Status }` → `{ Payload = new TriggerStatusPayloadSpec { Key = Status } }`, `DamageSpec { Value, Traits }` → `{ EffectValue = Value, Payload = new DamagePayloadSpec { Traits = Traits } }`(Traits가 없으면 Payload 생략), 그 밖의 `Value =` → `EffectValue =`, `Requires =` → `Requirement =`, `ScaleBy =` → `Scaling =`.
- 인라인 효과 JSON: 위 변환 스크립트의 규칙(`kind`→`key`, 숫자→`effectValue`, `requires`→`requirement`, `scaleBy`→`scaling`, `status`/`mode`/`traits`→`payload`)을 손으로 적용한다. 판별자 순서를 단언하는 테스트(예: `CardContentJsonTests.WritesTheEffectIdAndFactionRightAfterTheKind` `:92`)는 기대 문자열의 `kind`를 `key`로 바꾸고, 메서드 이름의 `Kind`를 `Key`로 바꾼다. `RejectsUnknownEffectKindByName`(`:124`)도 기대 메시지를 "Unknown effect key"로 맞춘다.
- `ConsumptionModeIsAlwaysWritten`(`:104`)은 `"mode"`가 이제 `payload` 안에 있다 — 기대 JSON 경로를 `payload.mode`로 맞춘다.
다시 실행해 `모두 통과`가 나올 때까지 반복한다.

- [ ] **Step 11: Review Focus 2·3·4를 수동으로 확인한다**

`Assets/StreamingAssets/Content/Cards/early_onset.json`(trigger_status)의 효과에 `"effectValue": 1`을 임시로 넣고 `Tools/verify.sh --quick`을 실행한다.
Expected: 부팅 로드 테스트가 `effectValue` 멤버 없음(`Could not find member`)으로 실패한다. 확인 후 `git checkout -- Assets/StreamingAssets/Content/Cards/early_onset.json`.

`toxic_reclaim.json`의 `consume_status` 효과에서 `"effectValue"` 줄을 임시로 지우고 `Tools/verify.sh --quick`.
Expected: "consume_status effectValue must be at least 1."로 실패. 확인 후 `git checkout -- Assets/StreamingAssets/Content/Cards/toxic_reclaim.json`.

같은 파일의 `apply_status` 효과에서 `"payload"`를 임시로 지우고 `Tools/verify.sh --quick`.
Expected: "apply_status requires a payload."로 실패(NullReferenceException이 아니다). 확인 후 되돌린다.

- [ ] **Step 12: 커밋**

```bash
git add -A Assets/Core/Authoring Assets/Core/Tests Assets/StreamingAssets/Content/Cards
git commit -m "refactor(authoring): 효과 JSON 이름을 EffectData에 맞추고 페이로드를 중첩한다

효과 JSON의 판별자를 key로, 숫자 필드(value·count·amount)를 effectValue로, requires·scaleBy를
requirement·scaling으로 바꾸고, 종류 전용 값을 코어 페이로드 레코드의 거울인 payload 객체로
묶는다(효과 JSON 설계 P2). 실행 카드 31장의 효과를 새 형식으로 다시 쓴다.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: 조작 JSON을 key와 payload로 바꾼다

**Files:**
- Modify: `Assets/Core/Authoring/Specs/ChangeExecutionOrderSpec.cs`, `Assets/Core/Authoring/Specs/SwapExecutionOrderSpec.cs`
- Modify: `Assets/Core/Authoring/Json/InterventionSpecJsonConverter.cs`
- Modify: `Assets/StreamingAssets/Content/Cards/*.json` (조작 카드 4장의 `intervention`)
- Modify(tests): 컴파일·실행 오류가 가리키는 파일(후보: `CardContentJsonTests`, `CardSpecMapperTests`, `InterventionSpecCatalogTests`, `DescriptionComposerTests`)

**Interfaces:**
- Produces: `ChangeExecutionOrderPayloadSpec { int Delta; InterventionTargetSideRef TargetSide; }`, `SwapExecutionOrderPayloadSpec { InterventionTargetSideRef TargetSide; bool RequireAdjacent; }`, `InterventionSpecJsonConverter.KeyProperty = "key"`. 코어 `ChangeExecutionOrderPayload`/`SwapExecutionOrderPayload`는 바뀌지 않는다.

- [ ] **Step 1: ChangeExecutionOrderSpec**

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 하나의 실행 순서를 Payload.Delta만큼 옮긴다. Payload.TargetSide로 진영을 제한한다
    /// (재촉=Player, 유예=Enemy, Any=제한 없음). 조작 봉투에는 공통 숫자 칸이 없으므로 Delta는
    /// 페이로드에 둔다(효과 JSON 설계 P1 경계 사례).</summary>
    public sealed class ChangeExecutionOrderSpec : InterventionSpec
    {
        public ChangeExecutionOrderPayloadSpec Payload;

        public override InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new ChangeExecutionOrderPayload(Payload.Delta, ToTargetSide(Payload.TargetSide));

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "change_execution_order requires a payload.";
            }
        }
    }

    /// <summary>ChangeExecutionOrderPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class ChangeExecutionOrderPayloadSpec
    {
        public int Delta;
        public InterventionTargetSideRef TargetSide;
    }
}
```

- [ ] **Step 2: SwapExecutionOrderSpec**

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Core.Authoring
{
    /// <summary>대상 카드 두 장의 실행 순서를 맞바꾼다. Payload.RequireAdjacent가 true면 둘이 해결
    /// 순서상 인접해야 한다(엇갈림).</summary>
    public sealed class SwapExecutionOrderSpec : InterventionSpec
    {
        public SwapExecutionOrderPayloadSpec Payload;

        public override InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public override IInterventionPayload ToPayload()
            => new SwapExecutionOrderPayload(ToTargetSide(Payload.TargetSide), Payload.RequireAdjacent);

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Payload == null)
            {
                yield return "swap_execution_order requires a payload.";
            }
        }
    }

    /// <summary>SwapExecutionOrderPayload의 저작 거울. 필드 이름은 코어 레코드를 따른다(효과 JSON 설계 P2).</summary>
    public sealed class SwapExecutionOrderPayloadSpec
    {
        public InterventionTargetSideRef TargetSide;
        public bool RequireAdjacent;
    }
}
```

- [ ] **Step 3: 조작 컨버터 판별자를 key로 바꾼다**

`InterventionSpecJsonConverter.cs`에서 Task 3 Step 7과 같은 규칙으로 바꾼다: `KindProperty` → `KeyProperty = "key"`, `FactoryByKind` → `FactoryByKey`, `KindByType` → `KeyByType`, `BuildKinds` → `BuildKeys`, 지역 변수 `kind` → `key`, 메시지 `"Intervention entry requires a '" + KeyProperty + "' property."`, `"Unknown intervention key '" + key + "'."`, `"Duplicate intervention spec key '" + key + "' in InterventionSpecCatalog."`. summary 끝에 "JSON 이름은 key다(효과 JSON 설계 P2)."를 더한다.

- [ ] **Step 4: 콘텐츠 조작을 새 형식으로 다시 쓴다**

Run: `python3 $SCRATCH/convert_cards.py Assets/StreamingAssets/Content/Cards interventions`
Expected: `interventions: 35 files`.

Run: `/usr/bin/grep -c '"kind"' Assets/StreamingAssets/Content/Cards/*.json | /usr/bin/grep -v ":0"`
Expected: 남는 `"kind"`는 `startCondition`을 가진 4장뿐이다.

- [ ] **Step 5: 테스트를 고친다**

Run: `Tools/verify.sh`
Expected: 처음엔 실패할 수 있다. `new ChangeExecutionOrderSpec { Delta = d, TargetSide = s }` → `new ChangeExecutionOrderSpec { Payload = new ChangeExecutionOrderPayloadSpec { Delta = d, TargetSide = s } }`, `new SwapExecutionOrderSpec { TargetSide = s, RequireAdjacent = a }` → `{ Payload = new SwapExecutionOrderPayloadSpec { TargetSide = s, RequireAdjacent = a } }`. 인라인 조작 JSON은 `{"kind": k, ...필드}` → `{"key": k, "payload": {...필드}}`(lock은 `{"key": "lock"}`). `CardContentJsonTests.Intervention_kind_picks_the_concrete_intervention_spec`(`:260`)는 이름의 `kind`를 `key`로 바꾼다. `Swap_spec_rejects_a_parameter_it_does_not_own`(`:273`)은 남의 필드(`delta`)를 `payload` 안에 넣어 같은 거부를 확인하도록 고친다. 다시 실행해 `모두 통과`.

- [ ] **Step 6: Review Focus 4(조작)를 수동으로 확인한다**

`crossover.json`의 `intervention`에서 `"payload"`를 임시로 지우고 `Tools/verify.sh --quick`.
Expected: "swap_execution_order requires a payload."로 실패. 확인 후 `git checkout -- Assets/StreamingAssets/Content/Cards/crossover.json`.

- [ ] **Step 7: 커밋**

```bash
git add -A Assets/Core/Authoring Assets/Core/Tests Assets/StreamingAssets/Content/Cards
git commit -m "refactor(authoring): 조작 JSON을 key와 payload로 바꾼다

조작 카드의 판별자를 key로 바꾸고 액션별 값을 코어 페이로드 레코드의 거울인 payload 객체로
묶는다(효과 JSON 설계 P2). 조작 카드 4장을 새 형식으로 다시 쓴다.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: 카드 그래프 도구와 문서를 맞춘다

**Files:**
- Modify: `Tools/graph/extract_card_graph.py:21,118-140`
- Modify: `Tools/graph/test_extract_card_graph.py:60-90,160-170`
- Modify: `docs/agents/content-authoring.md` 「AI 세션에서 JSON을 직접 저작한다」 절
- Modify: `docs/superpowers/specs/2026-07-19-open-card-authoring-design.md` 머리말
- Modify: `docs/superpowers/specs/2026-09-18-combat-execution-contract-design.md` 머리말
- Modify: `docs/superpowers/specs/2026-10-17-effect-json-payload-nesting-design.md` 상태 줄
- Modify: `docs/superpowers/README.md` (이 설계 행 `current`, 이 계획 행 제거·보관, 열린 카드 저작 행 적용 범위)
- Move: `docs/superpowers/plans/2026-10-17-effect-json-payload-nesting.{md,html}` → `docs/superpowers/.archive/plans/`

- [ ] **Step 1: 그래프 추출기를 새 형식으로 바꾼다**

`extract_card_graph.py:21`:

```python
KNOWN_EFFECT_FIELDS = {"key", "id", "targetFaction", "effectValue", "successEffectValue", "skipOnBasic", "requirement", "scaling", "payload"}
```

`:122-125`의 `kind = eff.get("kind")` 블록을 다음으로 바꾼다(변수 이름 `kind`와 이후 `used_kinds`·`effect_kind:` 노드 이름은 그래프 스키마라 그대로 둔다):

```python
            kind = eff.get("key")
            if kind is None:
                warn(f"{rel}: key 없는 효과")
                continue
```

`:126-127`의 경고 문구 `EffectKeys에 없는 kind`를 `EffectKeys에 없는 key`로 바꾼다. `:129-131`의 효과 단위 `condition` 경고 블록은 지운다(효과 단위 `condition`은 현재 형식에 없다 — 조건은 카드의 `startCondition`). `:135`를 다음으로 바꾼다:

```python
                skey = (eff.get("payload") or {}).get("key")
```

`:137`의 경고 문구 `status 필드가 없다`를 `payload.key가 없다`로 바꾼다.

- [ ] **Step 2: 추출기 테스트를 새 형식으로 바꾼다**

`test_extract_card_graph.py`의 `write_content`에서 카드 픽스처를 다음으로 바꾼다(`new_fields`·`cond_status` 카드는 지운다 — 폐지된 효과 단위 필드만 시험하던 것):

```python
    (root / "Cards" / "venom.json").write_text(json.dumps({
        "id": "venom", "name": "맹독", "effects": [
            {"key": "damage", "id": "e0", "targetFaction": "Enemy", "effectValue": 2},
            {"key": "apply_status", "id": "e1", "targetFaction": "Enemy", "effectValue": 1,
             "payload": {"key": "poison"}},
        ], "tags": ["독"]}), encoding="utf-8")
    (root / "Cards" / "odd.json").write_text(json.dumps({
        "id": "odd", "name": "이상한", "effects": [
            {"key": "apply_status", "payload": {"key": "ghost"}},
            {"key": "damage", "mystery_field": 1},
        ]}), encoding="utf-8")
```

그리고 `test_condition_maxAmount_damageBonusPerConsumed_필드는_미인식_경고가_없다`와 `test_condition이_상태를_참조하면_경고한다` 두 메서드를 지운다.

Run: `cd Tools/graph && python3 -m unittest test_extract_card_graph`
Expected: `OK`.

- [ ] **Step 3: 저작 규칙 문서에 P2·P3을 더한다**

`docs/agents/content-authoring.md` 「AI 세션에서 JSON을 직접 저작한다」 절의 번호 목록 바로 뒤에 다음 문단을 더한다:

```markdown
효과·조작 JSON의 필드 이름과 구조는 코어 레코드를 그대로 따른다 — 효과 한 줄은 `EffectData`, 조작은
`InterventionActionData`, `payload` 객체는 그 종류의 코어 페이로드 레코드의 속성 이름을 camelCase로 쓴다.
판별자는 `key`이고, 세기 숫자는 `effectValue`, 종류 전용 선택값만 `payload`에 둔다. 카드 JSON은 형식
버전을 갖지 않는다: 형식을 바꾸면 그 커밋에서 모든 카드를 함께 고친다. 시작 조건의 `kind`는 예외다
(닫힌 조건 이름). 근거: [효과 JSON 페이로드 중첩 설계](../superpowers/specs/2026-10-17-effect-json-payload-nesting-design.md).
```

- [ ] **Step 4: 대체된 설계 문서 머리말에 후속 줄을 더한다**

`2026-07-19-open-card-authoring-design.md` 머리말의 기존 `**2026-09-18 후속:**` 항목 바로 아래:

```markdown
- **2026-10-17 후속:** 효과·조작 JSON의 판별자(`kind` → `key`), 효과 숫자 필드 이름, 평평한 페이로드 필드는
  [효과 JSON 페이로드 중첩 설계](2026-10-17-effect-json-payload-nesting-design.md)가 대체했다. 아래 해당 서술은 작성 당시 기준이다.
```

`2026-09-18-combat-execution-contract-design.md` 머리말(첫 목록) 끝:

```markdown
- **2026-10-17 후속:** 카드 스키마 버전과 로딩 경계 구형 변환(아래 93-94행)은
  [효과 JSON 페이로드 중첩 설계](2026-10-17-effect-json-payload-nesting-design.md) P3이 폐지했다. 카드 JSON은 형식 버전 없이 항상 최신 형식이다.
```

- [ ] **Step 5: 색인과 상태를 갱신하고 계획을 보관한다 (규칙 20)**

- 설계 `.md`의 상태 줄을 `` `current` (2026-10-17 구현 완료) ``로 바꾼다. `.html`의 `badge` 줄을 `2026-10-17 · 구현 완료 · 도메인: 카드 저작`으로 바꾼다.
- `docs/superpowers/README.md`: 이 설계 행 상태를 `current`로, 적용 범위 끝의 "설계 승인 대기"를 지우고 다음 사용 시점을 "새 효과·조작 저작 타입 추가"로 바꾼다. 열린 카드 저작 행 적용 범위의 "(JSON `kind` 판별자와 컨버터)"를 "(JSON 형식은 효과 JSON 페이로드 중첩 설계)"로 바꾼다. 「진행 중인 계획과 로드맵」 표에서 이 계획 행을 지운다. 개정일을 그날로 맞춘다.
- `git mv docs/superpowers/plans/2026-10-17-effect-json-payload-nesting.md docs/superpowers/.archive/plans/` 와 `.html`도 같이 옮긴다.

- [ ] **Step 6: 최종 검증과 커밋**

Run: `Tools/verify.sh`
Expected: `모두 통과`.

```bash
git add -A Tools/graph docs
git commit -m "docs(authoring): 효과 JSON 새 형식에 맞춰 그래프 도구와 문서를 갱신한다

카드 그래프 추출기와 테스트를 key·effectValue·payload 형식으로 바꾸고, 저작 규칙 문서에
코어 이름 통일(P2)과 형식 버전 없음(P3)을 적는다. 대체된 두 설계 문서에 후속 줄을 달고,
설계를 current로 올리며 구현 계획을 보관한다.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## 완료 후

- master 머지는 사용자 승인 후에만 한다(규칙 19). 머지 전 `Tools/verify.sh` 전체 통과를 다시 확인한다.
- Unity 에디터는 이 변경을 화면으로 확인할 것이 없다(스펙·콘텐츠 형식 변경. Unity 레이어는 스펙을 참조하지 않는다). 사용자가 Play로 카드 설명·전투가 그대로인지 한 번 확인하면 충분하다.
