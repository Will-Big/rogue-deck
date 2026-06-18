# Fate Weaver 전투 코어 — 밸런스 검증 프로토타입 설계 (스펙)

- 작성일: 2026-06-18
- 원천 문서: [`Fate_Weaver_card_balance_principles_v2.md`](../../../Fate_Weaver_card_balance_principles_v2.md) (카드 밸런스/설계 원칙)
- 상태: 설계 확정, 구현 계획서 작성 대기

---

## 1. 목적과 범위

### 목적
Fate Weaver의 핵심 차별점 — **행동 카드는 자동 발동되지만 불완전하고, 운명 카드로 미래 영역의 순서(주도력)를 조작해 완성시킨다** — 가 실제로 성립하는지를 **코드로 반복·자동 검증**한다. 원천 문서가 수기로 진행하던 "3턴 시뮬레이션"을 자동화된 회귀 스위트로 바꾸는 것이 1차 목표다.

### 이번 범위 (밸런스 검증 프로토타입)
- 순수 C# 전투 도메인 코어 (규칙·턴 해석·조건·효과·상태)
- N턴 시뮬레이션/테스트 하니스 (무조작 ↔ 조작 비교 중심)
- 원천 문서 11장의 수정 카드 + 8장의 1·2·3턴 시나리오를 첫 콘텐츠로 인코딩

### 범위 밖 (향후)
- Unity UI/씬/연출/아트
- 런·맵·전투 간 진행·덱 구성 메타 (단, 영구 성장/운명력 증감은 **데이터 모델 차원에서 미리 대비**)

### 핵심 원칙 (원천 문서에서 가져온 불변식)
구현·밸런스가 반드시 지켜야 하는 4가지:
1. **무조작 시 손해가 명확한가**
2. **조작 성공 시 결과가 크게 바뀌는가**
3. **행동 카드 콤보가 자동 완성되지 않는가**
4. **방해가 비피해 카드에도 의미 있는가**

> 주의: 랜덤성이 있는 턴제이므로 **모든 턴이 유의미할 필요는 없다.** 위 불변식은 보편 강제가 아니라, 원칙을 보여주려 설계한 특정 시나리오에서만 임계값 기반으로 검사한다(§9).

---

## 2. 아키텍처 개요

### 계층 분리 (A안)
규칙(모델)을 Unity와 분리하되, 그 바깥은 전부 Unity 네이티브 기능으로 채운다. "Unity를 안 쓴다"가 아니라 "규칙 레이어만 순수하게 둔다".

```
Unity 레이어 (코어 참조 · Unity 기능 전면 활용)
  · 콘텐츠 저작   : ScriptableObject 카드 → 로드 시 코어 데이터로 변환
  · 표현·제어     : MonoBehaviour — 매 턴 코어 호출, 결과 수신
  · UI            : uGUI / UI Toolkit — 미래 영역·핸드·운명 카드
  · 연출·입력     : DOTween/Timeline · Input System
        │  입력: 카드/운명 플레이              ▲ 출력: 해석 타임라인(이벤트)
        ▼                                     │
도메인 코어 (순수 C# · UnityEngine 미참조 · 테스트로 즉시 검증)
  · 카드·조건·효과 데이터  · 턴 해석기  · 미래 영역/주도력  · 시뮬레이션 러너
        ▲
Unity Test Framework (EditMode) — 코어를 헤드리스로 구동, 문서 3턴 시나리오 = 자동 단언
```

### 통합 패턴 (가장 중요)
코어는 "무슨 일이 일어났는가"를 **결정론적 이벤트 시퀀스(해석 타임라인)로만 출력**한다.
- **시뮬레이션/테스트**: 시퀀스를 즉시 읽어 결과만 단언 → 밀리초 단위 수천 회 반복.
- **실제 게임**: 같은 시퀀스를 애니메이션과 함께 재생.

→ 밸런스 검증 로직과 게임 연출이 **같은 코어를 공유**한다. 지금 만드는 프로토타입이 그대로 최종 게임의 두뇌가 된다.

---

## 3. 모듈 경계 (asmdef)

의존은 **한 방향(바깥 → 코어)으로만** 흐른다.

| 어셈블리 | 참조 | 내용 |
|---|---|---|
| `FateWeaver.Core` | (BCL만, `noEngineReferences:true`) | Cards / Combat / Fate / Status / Events |
| `FateWeaver.Simulation` | Core | ScenarioDefinition·Runner·Result, 리포트 |
| `FateWeaver.Tests.EditMode` | Core, Simulation, NUnit | 설계 불변식 단언 |
| `FateWeaver.Unity` *(향후)* | Core | MonoBehaviour, SO, UI, 연출 |

`noEngineReferences:true` 가 핵심: 코어 어셈블리에서는 `using UnityEngine`이 **컴파일 자체가 안 된다.** 규칙/표현 분리가 약속이 아니라 빌드 규칙으로 강제된다.

---

## 4. 도메인 데이터 모델

### 4.1 3계층 모델 (영구 성장 대비)
덱빌딩의 영구 성장을 담으려면 Definition과 런타임 사이에 한 계층이 더 필요하다.

```
1. CardDefinition       불변 원본 템플릿 (콘텐츠 DB · 모두 공유)
2. OwnedCard            플레이어가 덱에 소유한 한 장 ★영구 성장이 여기★
                          def + permanentMods(수치 성장) + structuralMods(효과/조건 추가·교체)
                          → 런/세이브 데이터에 영속
3. ActionCardInstance   이번 전투·턴의 런타임 (initiative, locked, temporaryMods, target …)
```

영구 성장 = 공유 Definition을 변형하는 게 아니라 **OwnedCard에 영구 modifier를 쌓는 것**.

### 4.2 카드 타입 — enum / 태그
`CardType`(Attack/Skill/Defense)은 **행동 없는 분류 라벨**이다(다른 카드의 조건이 *질의*만 함). 상속은 오분류. 확장이 필요하면 단일 enum보다 **태그 집합**(`[Flags]` 또는 `HashSet<CardTag>`)으로 — 한 카드가 Attack이면서 Holy일 수 있게.

### 4.3 조건 — 데이터 변형 (sealed record)
조건은 잘 조합되는 작고 닫힌 집합이라 **데이터 변형**으로 둔다. 행동을 타입 안에 넣지 않고 **중앙 `ConditionEvaluator`**가 평가한다(직렬화·검사·결정론 위해).

```csharp
abstract record Condition;
record FirstToTrigger        : Condition;
record WithinNth(int N)       : Condition;
record BeforeNextEnemyAttack  : Condition;
record AdjacentCardIs(Dir Dir, Side Side, CardType Type) : Condition;
record SameTarget             : Condition;

ConditionTier Evaluate(Condition c, ActionCardInstance card, ResolutionContext ctx);
// ConditionTier = 실패 | 기본 | 성공  (원천 문서 0~30 / 30~50 / 100%)
```

### 4.4 효과 · 운명 액션 — 키 기반 핸들러 레지스트리
`Effect`(행동 카드가 하는 일)와 `FateAction`(운명 카드가 미래 영역에 하는 일)은 **매우 다양하고 서로 겹치지 않는 고유 능력**이 폭발하는 영역이다. 중앙 switch는 god-function이 되므로 **탈중앙 핸들러 레지스트리**를 쓴다.

- 카드 *콘텐츠* = `키 + 파라미터 + 대상` (데이터, 직렬화·검사 가능)
- *행동* = 키로 찾은 핸들러(코드, 임의로 bespoke 가능)
- 새 능력 = **핸들러 클래스 1개 + 키 등록**, 중앙 코드 무수정 (OCP)
- 공통 프리미티브는 범용 핸들러(ChangeInitiative 등), 일회성 능력은 전용 핸들러

```csharp
interface IFateActionHandler {
    FateActionKey Key { get; }
    bool   CanApply(FateActionContext ctx);   // 실행 없이 검증 (대상 유효? locked?)
    void   Apply(FateActionContext ctx);      // 미래 영역 조작 + 이벤트 방출
    string Describe(ResolvedParams p);        // 실행 없이 툴팁/UI 문자열
}
// IEffectHandler 도 동형.
```

문서 4장의 운명 카드 → FateAction 핸들러 매핑: ChangeInitiative(±N), Swap, SwapInitiative, Lock, Nullify, Reorder, ForceConditionSuccess, Reveal, DrawFate …

### 4.5 타입 안전 키 + 상수 카탈로그 + 부팅 검증
키는 raw string의 개방성을 유지하되 stringly-typed 문제를 없애기 위해 **타입 안전 래퍼**로 통일한다.

```csharp
readonly record struct StatusKey(string Id);       // FateActionKey, EffectKey 동형
static class StatusKeys {                            // 알려진 키 = 상수 (자동완성·오타 방지)
    public static readonly StatusKey Stun       = new("stun");
    public static readonly StatusKey Vulnerable = new("vulnerable");
}
```
- 빌트인 참조는 상수 사용 → enum의 타입 안전성 회수
- 데이터/모드 콘텐츠는 `new StatusKey("custom_x")`로 여전히 확장 가능 (enum이면 막힘)
- **부팅 시** "참조된 모든 키에 핸들러 등록됐나" 검증 → 누락 키 = 부팅 에러(런타임 침묵 실패 방지)
- *대안*: 어떤 키 공간이 영원히 코드-only·닫힘이라 확정되면 그 부분만 enum으로 단순화 가능. (기본은 통일된 타입 안전 키.)

### 4.6 파라미터 해결 (성장 반영)
파라미터는 저작 시점 고정값이 아니라 **사용 시점에 해결**한다.

```
ResolvedParams Resolve(baseParams, permanentMods, temporaryMods)
   // 연산 우선순위: Override > Multiply > Add, 결정론적 순서
```
- modifier는 **필드를 이름으로 지정**(`("delta", Add, +1)`)하므로 파라미터는 *이름 있는 타입 필드*여야 한다.
- `Apply`/`Describe` 둘 다 `Resolve` 결과를 읽음 → **툴팁과 동작이 절대 어긋나지 않음**, 전투 중 변경도 lazy하게 반영.

### 4.7 상태 (런타임)
```csharp
CombatState {
    int playerHp; Enemy[] enemies;                 // enemy: hp, statuses …
    int fateEnergy; int fateEnergyPerTurn;          // §8 — 변수
    List<FateCardInstance> fateHand;
    FutureZone zone; int rngSeed;
}
FutureZone {                                        // 순서 있는 ActionCardInstance 목록
    IReadOnlyList<ActionCardInstance> ResolutionOrder();  // 주도력 오름차순, 안정 정렬
    void ChangeInitiative / Swap / Lock / Nullify(...);
}
```

---

## 5. 상태 이상 시스템 (Scope + Hook)

상태 이상은 §4.4와 같은 **키 레지스트리 패턴**에 두 축을 명시해 구현한다.

- **Scope (부착 대상)**: `Entity`(캐릭터) | `CardInstance`(미래 영역의 카드) | `Zone`
- **Hook (개입 시점)**: 주는 피해 / 받는 피해 / 카드 발동 직전 / 턴 종료 …

```csharp
interface IStatusBehavior {
    StatusKey   Key   { get; }
    StatusScope Scope { get; }
    int  ModifyOutgoingDamage(int dmg, StatusContext c) => dmg;   // 약화·힘 (공격자)
    int  ModifyIncomingDamage(int dmg, StatusContext c) => dmg;   // 취약·방어 (피격자)
    bool InterceptCardResolve(StatusContext c) => false;          // 기절 → true=무력화
    void OnTurnEnd(StatusContext c) {}                            // 지속시간·도트
    string Describe(StatusInstance s);
}
interface IStatusHolder { StatusBag Statuses { get; } }           // Entity·ActionCardInstance가 구현
```

해석 파이프라인은 수치를 하드코딩하지 않고 **각 훅 지점에서 관련 holder의 상태를 fold**한다:

```
ApplyDamage(source, target, baseDmg):
    dmg = fold(source.Statuses.ModifyOutgoingDamage)   // 약화/힘
    dmg = fold(target.Statuses.ModifyIncomingDamage)   // 취약/방어
    적용 + 이벤트
```

예시 매핑:
- **기절** = `Scope=CardInstance`, `InterceptCardResolve → true` (해당 카드 발동 스킵)
- **취약** = `Scope=Entity`, `ModifyIncomingDamage → dmg*1.5` (출처 무관·일관 적용 — "다음 카드 +50%"보다 견고)
- **적 방해**(②의 reward_nullified 등)는 별도 메커니즘이 아니라 **카드 스코프 상태로 통일**

확장성 판정:
- **status 집합은 완전 개방** — 새 상태 = 새 클래스 + 키 등록
- **hook 집합은 중앙·소수이고 천천히 자란다** — 정말 새 개입 시점이 필요할 때만 인터페이스에 훅 1개 + 파이프라인 질의 1줄 추가(국소 변경). 실제 카드게임 엔진(StS Power, 하스스톤 오라)과 동형.
- 주의: ① 여러 수정자 fold 순서(가산→곱연산)를 정해 결정론 유지(ParamModifier 우선순위 공유). ② 광역("이번 턴 내 카드 +2")은 카드마다 도장 찍지 말고 Entity/Zone 스코프 **오라 + 필터**로.

---

## 6. 턴 해석 흐름

```
Phase 0  턴 시작 (BeginTurn)
  · 행동 카드 → 미래 영역 배치 (게임: 행동 덱 / 시뮬: 시나리오 주입)
  · 적 의도 배치, baseInitiative 부여
  · 운명력 = fateEnergyPerTurn 리셋, 운명 카드 드로우
  · 이벤트: TurnStarted + 미래 영역 스냅샷

Phase 1  조작 (운명 카드 플레이) — 여기서만 주도력/순서가 바뀜
  · FateCard 플레이 → 비용 검사(cost ≤ 운명력) → 차감 → FateAction 적용 (locked 거부)
  · 이벤트: FatePlayed + 갱신 스냅샷

Phase 2  해석 (EndTurn) — TurnResolver.Resolve
  1. resolved[] = 미래 영역을 주도력 오름차순(안정)으로 "동결"
  2. ResolutionContext 생성 (index·인접·순서 질의, 보류 상태 보관)
  3. for i in resolved[]:
       a. 이 카드에 걸린 카드 스코프 상태 적용 (예: 기절·reward_nullified)
       b. 각 Effect 실행: Conditional(cond,then,else) → tier 평가
          · reward_nullified면 강제 실패/기본 분기
          · 피해는 ApplyDamage 파이프라인(§5) 경유
          · 적 방해 → "다음 미해석 플레이어 카드"에 카드 스코프 상태 부여
       c. 이벤트: CardResolved (카드, tier, 피해, 대상, HP 변화)
  4. 정리: 조건 실패한 표식 소멸, 방어/임시 상태 정리
  5. 승패 검사 → 이벤트: TurnEnded(+결과)

Phase 3  전투 지속 시 Phase 0 반복
```

핵심 결정:
1. **해석 순서는 EndTurn에 "동결"** — 운명 카드는 Phase 1에서만 순서를 바꾼다.
2. **순차·상태 기반 해석** — "다음 카드 방해", "적보다 먼저", "N번째 이내"가 자연스럽게 구현.
3. **모든 것이 이벤트를 남긴다** — `ResolutionEvent` 타임라인이 코어의 유일한 출력.
4. **결정론** — `(초기 미래 영역 + 운명 플레이 스크립트 + rngSeed)`가 같으면 결과가 항상 동일.

---

## 7. 시뮬레이션·테스트 하니스

### 7.1 Compare 중심 API
원천 문서가 거의 모든 평가를 **무조작 ↔ 조작 비교**로 하므로, 중심 API는 비교 실행이다.

```csharp
ScenarioResult   Run(ScenarioDefinition s);
ComparisonResult Compare(ScenarioDefinition s);   // (A) 운명 플레이 제거=무조작, (B) 스크립트=조작
```

### 7.2 시나리오 정의 (데이터)
```csharp
ScenarioDefinition { string name; int playerHp; EnemySpec[] enemies; TurnScript[] turns; }
TurnScript {
    int fateEnergy;              // 턴별 운명력 (변수, §8)
    ZonePlacement[] zone;        // cardDefId, Side, initiative, target
    FatePlay[] fatePlays;        // 순서 있는 조작 스크립트
}
```
테스트용은 fluent 빌더, 디자이너용은 향후 JSON.

### 7.3 결과 모델
```csharp
ScenarioResult {
    List<TurnSummary> turns; CombatState finalState;
    Outcome outcome;                          // Win/Lose/Ongoing + 남은 HP
    List<ResolutionEvent> timeline;
}
TurnSummary {
    int turnIndex; int playerHpDelta, enemyHpDelta;
    Dictionary<CardId, ConditionTier> conditionResults; int fateEnergySpent;
}
```

### 7.4 첫 콘텐츠 세트
- 문서 11장 카드: 손목 베기, 표식 새기기, 연쇄 베기, 반격 자세(후보 A)
- 운명 카드: 주도력 ±2, 인접 위치 교환, 고정, 주도력 6↑ 무효화
- 문서 8장의 1·2·3턴 구조를 시나리오로 인코딩. 특히 8.2의 **"옛 카드는 자동 콤보 완성(나쁨)"을 회귀 가드**로 남겨 수정안이 막는지 증명.

### 7.5 부가 기능
- **리포트 모드**(테스트 아님): 턴별 피해·조건 tier·무조작↔조작 Δ를 마크다운/콘솔로 출력 → 디자이너 육안 점검.
- **결정론 테스트**: 같은 시나리오+시드 → 타임라인 완전 일치 단언.

---

## 8. 운명력 경제 (변수)

운명력은 **3 고정이 아니다.** 현재는 3에 초점을 두지만, 게임 진행에 따라 증가·감소할 수 있다.

- `fateEnergyPerTurn`은 **런 상태의 변수**. 시나리오에서 턴마다 설정 가능.
- 런 중 성장/감소는 **§4.6 modifier 메커니즘을 재사용**(영구 성장과 동일 구조).
- 어디에도 "3"을 하드코딩하지 않는다.

---

## 9. 설계 불변식과 테스트 철학

**랜덤성이 있는 턴제에서 모든 턴이 유의미할 필요는 없다.** 따라서:
- 하니스 기본은 **하드 단언이 아니라 리포트(관찰)**.
- 하드 단언은 **원칙을 보여주려 설계한 특정 시나리오에만 opt-in**, **임계값·허용오차 기반**.
- 스위트는 "모든 턴이 의미 있어야 함"을 요구하지 않는다.

필수 불변식 단언(해당 시나리오 한정):

| 불변식 | 단언 |
|---|---|
| ① 무조작 시 손해가 명확한가 | `무조작.손해 − 조작.손해 ≥ 임계` |
| ② 조작 성공 시 크게 바뀌는가 | `(조작.playerHp − 무조작.playerHp) ≥ Δ임계` |
| ③ 콤보 자동완성 안 되는가 | `무조작에서 강콤보 조건 tier ≠ 성공` (8.2 회귀 가드) |
| ④ 방해가 비피해 카드에도 의미 | `손목베기 有/無로 표식 등 비피해 카드 tier가 달라짐` |

(과거 ⑤ "운명력 3으로 매번 완벽 해결 안 됨"은 운명력이 변수가 되어 보편 불변식이 될 수 없으므로 제거. ⑥ "무엇을 포기할지"는 자동 단언 대상 아님 — 리포트로 관찰.)

---

## 10. 마일스톤

각 단계는 UI 없이 EditMode 테스트로 독립 검증된다.

| 단계 | 내용 | 검증 기준 |
|---|---|---|
| **M0 골격** | asmdef 3개(Core=`noEngineReferences`, Simulation, Tests), 폴더/네임스페이스 골격 | 테스트 러너 green; 코어에서 `using UnityEngine` 시 컴파일 실패 |
| **M1 코어 루프** | Side/CardType, CardDefinition, OwnedCard, ActionCardInstance, FutureZone, CombatState(변수 운명력), TurnResolver(기본 효과)+이벤트 | 고정 미래 영역이 올바른 순서로 해석·피해 적용·타임라인 일치 |
| **M2 조건** | Condition 데이터 변형 + Evaluator, 실패/기본/성공 3단계 | 위치별 tier가 기대대로 |
| **M3 운명·조작** | 타입안전 키+레지스트리+부팅 검증, FateAction 핸들러, 변수 운명력 경제, 조작 단계 | 운명 플레이가 미래 영역 변경, 비용 게이팅, locked 거부 |
| **M4 상태 이상** | IStatusHolder(Entity+Card), IStatusBehavior(Scope+Hook), StatusKey 레지스트리, ApplyDamage 파이프라인; 기절/취약/약화/reward_nullified | 기절=무력화, 취약=피해↑, 손목베기=비피해 카드 방해 |
| **M5 하니스·콘텐츠** | ScenarioDefinition/Runner/Result, Compare, fluent 빌더, 리포트, 결정론 테스트; 문서 11장 카드 + 8장 시나리오 | 문서 시나리오 재현, ③ 회귀 가드 통과, 리포트 가독 |
| **M6 확장 seam 정리** | 확장 지점 문서화, "카드/상태/운명 액션 추가하는 법" 가이드 | 향후 Unity 레이어가 얹힐 경계 확정 |

---

## 11. 확장 seam (요약)

| 확장 대상 | 방법 | 중앙 코드 수정 |
|---|---|---|
| 새 운명 액션 / 효과 | `IFateActionHandler`/`IEffectHandler` 구현 + 키 등록 | 없음 |
| 새 상태 이상 | `IStatusBehavior` 구현 + StatusKey 등록 | 없음 |
| 새 조건 | `Condition` 레코드 추가 + Evaluator 분기 | Evaluator(소수·중앙) |
| 새 개입 시점(훅) | 인터페이스에 훅 1개 + 파이프라인 질의 1줄 | 국소·드묾 |
| 카드 영구 성장 | OwnedCard에 modifier/구조 delta 추가 | 없음(데이터) |

---

## 12. 결정 기록 (왜 이렇게)

- **순수 C# 코어**: 헤드리스 테스트 속도·결정론·규칙/표현 분리 강제. 비용은 경계에서 SO↔DTO 변환 보일러플레이트.
- **조건=데이터, 효과/운명액션=핸들러 레지스트리**: 조건은 닫힌 조합형, 효과/액션은 열린 bespoke. 직렬화·검사·OCP를 위해 차등.
- **키=타입 안전 래퍼**(enum 아님): 닫힌 집합을 강제하지 않으면서 타입 안전성 확보. 데이터/모드 확장 여지 보존.
- **상태=Scope+Hook**: 카드·캐릭터 양쪽을 한 시스템으로. 방해 메커니즘 흡수.
- **운명력 변수화**: 진행에 따른 증감 대비.
- **약한 단언**: 랜덤 턴제에서 모든 턴 유의미 강제는 과제약.
