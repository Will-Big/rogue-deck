# 잠금 입문 적 "간수(Warden)" 설계 (Lock Tutorial Enemy)

> 브레인스토밍 산출물(사용자 승인 2026-06-27). 다음 단계: 사용자 스펙 검토 → writing-plans → 서브에이전트 구현.

**목표:** 잠금(고정) 기믹을 *쉬운 난이도로* 가르치는 신규 적 "간수"를 추가한다. 매 턴 무작위로 자기 카드 1장이 잠기고(실행 순서 완전 면역), 카탈로그의 절반이 "위치 조건" 카드라, 운에 따라 그 조건부 카드가 잠기면 플레이어가 개입 카드로 무력화하지 못하는 긴장이 생긴다.

## 배경 / 전제 (현재 코드)
- **`IEnemyTurnPolicy` 시밍**(`Simulation/Enemies/`)이 적의 턴 행동을 캡슐화. 기존 구현 `RandomMovesetPolicy`. 간수는 새 정책 2개를 조합.
- **`CardDefinition.StartsLocked`** 가 있고, `DeckCombatSession.BeginTurn`이 적 카드 진입 시 `inst.IsLocked = enemyCard.StartsLocked`로 굽는다. 운명 핸들러는 이미 잠긴 카드의 재배치를 거부.
- **tie-break: 이미 플레이어 우선** — `FutureZone.ResolutionOrder()` = `OrderBy(ExecutionOrder).ThenBy(player<enemy)`. 동률 시 플레이어가 먼저 발동(엔진 변경 불필요).
- **설명은 효과-조합 컴포저(Thread A, 병합됨)** 가 자동 생성 — 간수 카드는 EffectData만 작성하면 설명이 나온다.

## 컴포넌트

### 1. `ShuffleBagPolicy` (신규, 재사용 클래스 · `IEnemyTurnPolicy`)
- 생성자: `(IReadOnlyList<CardDefinition> deck, int drawPerTurn, int seed)`.
- 동작: 덱을 시드로 셔플 → 매 턴 `CardsForTurn`이 **비복원으로 `drawPerTurn`장 드로우**. 남은 카드가 `drawPerTurn` 미만이면 **전체 덱을 재셔플 후 드로우**(부분 드로우 없음).
- **상태형**(드로우 진행 상태 보유) — `IEnemyTurnPolicy` 계약상 턴당 1회·오름차순 호출이라 결정적. 시드 같으면 동일 시퀀스.
- **대부분의 적이 재사용**(자기 덱·드로우수만 주입). 간수는 `drawPerTurn = 2`.

### 2. `SelfLockPolicy` (신규, 데코레이터 · `IEnemyTurnPolicy`)
- 생성자: `(IEnemyTurnPolicy inner, int seed)`.
- `CardsForTurn`: `inner`가 준 카드 중 **무작위 1장**을 `card with { StartsLocked = true }`로 복제해 잠금, 나머지는 그대로. 시드 결정적. 빈 목록이면 무동작.
- 간수 정책 = `new SelfLockPolicy(new ShuffleBagPolicy(WardenDeck.Deck(), 2, seed), seed)`.
- **세션/Enemy 변경 없음**(기존 StartsLocked bake가 처리).

### 3. 잠금 = 실행 순서 완전 면역 (`DeckCombatSession` 작은 변경)
- 현재 `BeginTurn` 적 루프: `inst.ExecutionOrder = StatusExecutionOrder.ExecutionOrderFor(...); inst.IsLocked = enemyCard.StartsLocked;`
- 변경: **IsLocked를 먼저 설정하고, 잠겼으면 fold를 건너뜀**:
  ```csharp
  inst.IsLocked = enemyCard.StartsLocked;
  if (!inst.IsLocked)
      inst.ExecutionOrder = StatusExecutionOrder.ExecutionOrderFor(inst.ExecutionOrder, enemyBag, _statuses);
  ```
- 효과: 잠긴 카드는 둔화/가속(실행 순서 fold)을 받지 않음. 개입 카드는 이미 거부. → **잠금 = 운명·둔화·가속 전부 무효.**
- 기존 동작 미세 변경(잠긴 카드+둔화 조합) — 기존 테스트 불변, 신규 면역 테스트 추가.

### 4. 신규 조건 `NoFollowingCardOfSide(Side)` (Core)
- `NoPrecedingCardOfSide`의 *앞/뒤 거울*. Success = 해석 순서상 **이 카드 뒤에 해당 Side 카드가 없을 때**.
- 추가 지점: `Core/Conditions/Condition.cs`(레코드), `ConditionEvaluator.cs`(평가: `index`보다 큰 i 중 해당 Side 있으면 Basic, 없으면 Success), `Authoring/EffectSpec.cs`(`ConditionKind.NoFollowingEnemyCard` 또는 일반화), `Authoring/CardSpecMapper.cs`(매핑).
- 올려치기/버티기가 쓰는 "이전 수행한 적 카드 없으면"은 기존 `NoPrecedingCardOfSide(Enemy)` 재사용.

### 5. `WardenDeck` (Simulation, `GoblinDeck` 패턴)
- `EnemyId = "warden"`, `StartingHp = 20`.
- 카드 정의(아래 표) + `Deck()`(6장 리스트) + `Policy(seed)`(= SelfLockPolicy(ShuffleBagPolicy(Deck, 2, seed), seed)).

**덱 (6장):**
| 카드 | id | 종류 | 효과 |
|---|---|---|---|
| 휘두르기 ×2 | `warden_swing` | 일반 공격 | 피해 3, 실행 순서 5 |
| 내려치기 | `warden_smash` | 특수 공격 | 피해 2, 실행 순서 5, 이후 수행한 적 카드 없으면 피해 7 (`NoFollowingCardOfSide(Enemy)`) |
| 올려치기 | `warden_uppercut` | 특수 공격 | 피해 2, 실행 순서 4, 이전 수행한 적 카드 없으면 피해 7 (`NoPrecedingCardOfSide(Enemy)`) |
| 막기 | `warden_block` | 일반 방어 | 방어 3(자신), 실행 순서 4 |
| 버티기 | `warden_brace` | 특수 방어 | 방어 3(자신), 실행 순서 4, 이전 수행한 적 카드 없으면 방어 6 (`NoPrecedingCardOfSide(Enemy)`) |

(수치 튜닝 가능. 모두 적 측. 조건부는 `EffectData.Conditional`/조건+SuccessEffectValue.)

### 6. 설명 문구 (Thread A 보캐뷸러리 커플링)
- `KoreanDescriptionVocabulary`에 `NoFollowingCardOfSide` 절 추가: "이후 수행한 {side} 카드 없으면".
- **순서-상대 조건 키워드를 "이전/이후 수행한"으로 통일**: 기존 `NoPrecedingCardOfSide` 절("앞에/이전")을 "이전 수행한"으로 확정. (이유: 동률 플레이어-우선이라 *위치*가 아니라 *수행 순서*가 정확.) `AdjacentCardIs`("바로 앞/뒤")의 "바로 이전/이후 수행한" 통일은 선택적 — 영향받는 기존 카드 설명 테스트가 늘어나므로 plan에서 포함 여부 확정.

## 테스트 (헤드리스)
- `ShuffleBagPolicy`: 한 바퀴(6장/2 = 3턴)에 각 카드가 정확히 자기 수만큼 등장, 재셔플 후 반복, 시드 결정성(같음/다름), drawPerTurn 미만 잔여 시 재셔플.
- `SelfLockPolicy`: 매 턴 정확히 1장만 잠김(StartsLocked), 나머지 비잠금, 시드 결정성, 빈 목록 무동작.
- `NoFollowingCardOfSide`: 평가기 — 뒤에 해당 측 카드 있음→Basic, 없음→Success(공개 `ConditionEvaluator`로).
- 잠금 면역: `DeckCombatSession`에서 잠긴 적 카드가 둔화 적용에도 실행 순서 불변(+ 운명 거부는 기존).
- `WardenDeck`: HP·덱 구성·카드 수치/조건 계약.
- 통합: 간수 조건부 카드가 위치에 따라 기본/성공 피해를 내는지(예: 내려치기 단독=7, 뒤에 적 카드 있으면 2).
- 설명: 신규 절 "이후 수행한 적 카드 없으면 피해 7" 조립 + 기존 절 "이전 수행한" 갱신.

## 비목표 / 후속
- **플레이어-잠금(구속, 텔레그래프) = V2.**
- 간수 **아트/CardAsset/DeckAsset**(Unity 콘텐츠) — 사용자가 `CardSO/Enemies/Warden/`에서 수작업 중. 헤드리스 측만 본 스펙 범위.
- 플레이테스트 컨트롤러에 간수 **인카운터 투입**(고블린 vs 간수 선택) — 별도.
- `AdjacentCardIs` 문구 통일(선택) — plan에서 결정.

## 파일 윤곽
- 신규: `Simulation/Enemies/ShuffleBagPolicy.cs`, `Simulation/Enemies/SelfLockPolicy.cs`, `Simulation/WardenDeck.cs`, `Core/Conditions/` 조건 추가, 테스트 3~4종.
- 수정: `Core/Conditions/Condition.cs`+`ConditionEvaluator.cs`, `Authoring/EffectSpec.cs`+`CardSpecMapper.cs`, `Simulation/DeckCombatSession.cs`(면역), `Unity/.../KoreanDescriptionVocabulary.cs`(문구) + 영향 설명 테스트.

## 자가검토
- **플레이스홀더:** 없음. 카드/조건/정책 전부 구체값.
- **일관성:** 잠금=면역(컴포넌트3)과 SelfLockPolicy(2)가 StartsLocked로 연결, 일관. tie-break 플레이어-우선 확인됨 → "이전/이후 수행한" 의미 정합.
- **스코프:** 단일 구현 계획 규모. `AdjacentCardIs` 문구 통일만 plan에서 포함/제외 결정.
- **모호점:** ShuffleBag 잔여<draw 시 "전체 재셔플 후 드로우"로 명시(부분 드로우 없음). 신규 `ConditionKind` 명명은 plan에서 확정(`NoFollowingEnemyCard` 제안).
