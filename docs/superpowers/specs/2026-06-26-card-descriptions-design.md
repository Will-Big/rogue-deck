# 효과-조합 카드 설명 시스템 설계 (Dynamic Card Descriptions)

> 브레인스토밍 산출물. 방향은 사용자와 합의됨(이 세션). 다음 단계: 사용자 스펙 검토 → writing-plans → 서브에이전트 자동 구현.

**목표:** 카드 설명을 *카드별 하드코딩 문자열*에서 ***효과(EffectData)별 조각을 조립하고 숫자는 데이터에서 치환*** 하는 방식으로 전환한다. 이로써 (a) 수치 튜닝 시 설명 자동 갱신, (b) 다국어 대비, (c) 미래의 동적 카드 변화(수치변화/능력추가/변신/대상변경)를 한 메커니즘으로 수용한다.

## 배경 (현재 문제)
- 설명은 `Assets/Unity/PlaytestKoreanText.cs`의 `CardDescription(string id)` **하드코딩 switch**에서 나옴(`case "slash": return "피해 4.";`).
- `CardAsset.Description` 필드는 존재하나 **죽어 있음**(ToSpec이 안 실음, 런타임 미사용) — 예전 `Art`와 동일 안티패턴.
- 숫자가 문자열에 박혀 있어 **튜닝 시 설명이 어긋남**. `CardDescriptionTests`(UnityEditMode)도 문자열 하드코딩.

## 산업 표준 (참고)
로컬 키 + 토큰 템플릿. Slay the Spire의 `!D!`/`!B!`/`!M!` 토큰(언어별 cards.json, 런타임 치환, 수정값 색상)이 우리와 거의 동일.

## 아키텍처
- **`DescriptionComposer` (순수 C#, Simulation, 헤드리스 테스트 가능)** — `CardDefinition`의 `Effects`(+ 개입 카드는 `InterventionAction`)를 순회하며 효과별 조각을 만들고 숫자 토큰을 그 데이터로 치환, 이어붙여 최종 설명 문자열 반환.
- **`IDescriptionVocabulary` (인터페이스)** — 로컬라이즈 템플릿 공급:
  - 효과 조각: `EffectKind`별 템플릿 (Damage / ApplyStatus / GrantNextAttackBonus / NullifyNextReward)
  - 조건 절: `ConditionKind`별 (FirstToTrigger, WithinNth, PrevIsEnemyAttack, PrevIsPlayerAttack, NextIsEnemyAttack, NoPrecedingPlayerCard, BeforeNextEnemyAttack)
  - 키워드(상태) 이름: `StatusKey`별 (방어/둔화/가속/기절/취약/조건보상무효)
  - 개입 템플릿: `InterventionActionKey`별 (ChangeExecutionOrder ±, SwapExecutionOrder, Lock)
- **`KoreanDescriptionVocabulary` (Simulation, 순수)** — 한국어 구현 1개. 다국어는 구현 교체로 확장. (참고: `GoblinDeck`이 이미 한글명을 Simulation에 두므로 일관)
- **호출부:** `CardPresentation.From/FromDefinition`이 `PlaytestKoreanText.CardDescription(def.Id)` 대신 `DescriptionComposer.Describe(def, koreanVocab)` 호출. 하드코딩 switch 제거.

## 토큰 → 데이터 매핑
- `{dmg}` = `EffectData.EffectValue` (Damage)
- `{dmg_success}` = `EffectData.SuccessEffectValue`
- `{mag}` = 상태 magnitude (= ApplyStatus의 `EffectValue`)
- `{turns}` = `StatusLifetime` count (Turns/UntilConsumed)
- `{amt}` = 개입 `InterventionActionData.EffectValue`
- 대상 = `StatusApplyTarget` (자신/적) → 로컬 텍스트

예시 조립:
- slash `Damage(4)` → `"피해 4."`
- quick_cut `Damage(2, FirstToTrigger→8)` → `"피해 2. 첫 발동이면 피해 8."`
- slow_hex `ApplyStatus(Slow, mag3, Turns2, 적)` → `"적 둔화 3 (2턴)."`
- pull_forward `Intervention(ChangeExecutionOrder, -1)` → `"한 카드의 실행 순서 -1."`

## 4대 동적 변화 대응 (효과-조합이 핵심인 이유)
- 수치 변화 → 토큰이 실효값 추종(추후 수정값 색상).
- 능력 영구 추가 → 효과 1개 늘면 조각 1개 자동 추가.
- 변신 → 효과들이 바뀌면 설명 자동 교체.
- 대상 변경 → 대상 토큰 반영.

(메커니즘 측 "소유 카드 영구 상태층"은 별개 후속. 데이터 모델 — 불변 레코드 `CardDefinition` + `EffectData` 리스트 + 인스턴스 — 은 `with` 복제로 이를 수용 가능하나, 그 기능 구현 시 추가.)

## 테스트 (헤드리스)
- 효과별 조각 단위: 각 EffectKind → 기대 조각.
- 숫자 토큰이 EffectValue/SuccessEffectValue/magnitude/turns 추종(값 바꾸면 출력 바뀜).
- 조건 → 절 결합("...이면 ...").
- 다중 효과 결합 순서/구분.
- 개입 카드(±, 교환, 고정).
- 가짜 vocab로 컴포저 로직 격리 + 한국어 vocab로 실제 출력 검증(기존 `CardDescriptionTests`를 헤드리스 조립 테스트로 재작성).

## 비목표 / 후속
- 실제 다국어 번역(구조만 준비, KR 1개로 시작).
- 수정값 색상(초록/빨강) — UI.
- 소유 카드 영구 상태층(강화/변신 메커니즘).
- 키워드 툴팁.
- 카드 *이름*은 범위 외(설명만). `PlaytestKoreanText.StatusName`과 vocab 키워드명은 추후 통합 검토.

## 파일 윤곽
- 신규: `Simulation/Descriptions/DescriptionComposer.cs`, `IDescriptionVocabulary.cs`, `KoreanDescriptionVocabulary.cs`
- 수정: `Unity/CardPresentation.cs` (컴포저 호출), `Unity/PlaytestKoreanText.cs` (CardDescription 제거)
- 테스트: `Tests/EditMode/DescriptionComposerTests.cs` (헤드리스); 기존 `Tests/UnityEditMode/CardDescriptionTests.cs` 정리/이관

## 자가검토
- 플레이스홀더 없음. 커버리지 = 현재 모든 효과/조건/상태/운명. 단일 구현 계획 규모로 적절.
- 모호점: 조건이 ApplyStatus에도 붙는 경우(cover) 조각+절 결합 규칙을 plan에서 명시할 것. 다중 효과 카드의 구분자(". ")도 plan에서 확정.
