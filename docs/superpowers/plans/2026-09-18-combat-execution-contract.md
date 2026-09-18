# 전투 실행 계약 구현 계획

> **For agentic workers: REQUIRED SUB-SKILL:** Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 효과마다 위치를 다시 선택하고 직접 반응을 해결하며 카드 종료에 승패를 판정하는 전투 실행 계약을 구현한다.

**Architecture:** 실행선과 실행 이력, 카드 실행과 효과 처리, 전투 사건과 직접 반응, 상태 만료 정책을 분리한다. 기존 레지스트리·JSON 변환 경계는 유지한다.

**Tech Stack:** 순수 C# 9, Newtonsoft.Json, NUnit 헤드리스 테스트, 기존 Node 카드 저작 도구 테스트.

**Spec:** [전투 실행·반응·콘텐츠 계약](../specs/2026-09-18-combat-execution-contract-design.md)

## Global Constraints

- 코어는 UnityEngine을 참조하지 않는다.
- 무작위는 CombatState의 RNG를 사용한다.
- 외부 패키지를 추가하지 않는다.
- 다중 적 정책·소유자 연결은 별도 작업 범위다.
- 구현은 전용 워크트리에서 수행한다.
- 각 단계는 `Tools/verify.sh --quick`, 최종은 `Tools/verify.sh`로 검증한다.

사람 검수용 [HTML 개요](2026-09-18-combat-execution-contract.html).
설계: [승인된 전투 실행·반응·콘텐츠 계약](../specs/2026-09-18-combat-execution-contract-design.md).
상태: active. **사용자는 이 세션에서 계획만 작성하도록 지시했다. 이 문서는 코드 구현 완료나 착수 승인이 아니다.**
실행 담당자는 별도 구현 요청을 받은 뒤 `superpowers:executing-plans`로 아래 작업을 순차 수행한다.
개정: 2026-09-18 — 계획 검토에서 사용자가 내린 결정(아래 「검토 반영 결정」 D1~D8)을 반영했다.
T0 신설, T2를 T2a·T2b로 분리, 조건 레지스트리 제외, 카드 형식 필드 이름 변경, Unity 배치 검증 추가.

## 상세

### 검토 반영 결정 (2026-09-18 사용자 결정)

아래 결정은 이 계획의 다른 절보다 우선한다. 각 작업 절은 이 결정에 맞춰 고쳐 두었다.

| ID | 결정 | 반영 작업 |
|---|---|---|
| D1 | **독은 관통을 유지하고 취약(피해 배율)을 적용하지 않는다.** 공통 피해 경로로 옮겨도 현재 동작(`PoisonBehavior.cs:32`가 방어·배율 없이 HP를 깎음)과 결과가 같아야 한다 | T4 |
| D2 | **`condensed_burst`는 결과 참조를 확장해 옮긴다.** 소비량에 비례하는 수치를 앞 소비 효과의 결과로 표현한다. `damageBonusPerConsumed`와 소비 경로의 `AddPendingDamageBonus` 호출은 제거한다. `_pendingDamageBonus` 자체는 `GrantNextPlayerDamageCardBonusHandler.cs:20`이 쓰므로 남긴다 | T2a |
| D3 | **Unity 레이어를 배치로 검증한다.** `Tools/verify.sh`는 `Assets/Core`만 컴파일한다(`Tools/verify.sh:36`). T1·T3·T6·T7 끝에 [EditMode 배치 실행](../../agents/unity-batch-runs.md)을 워크트리 대상으로 돌려 결과 XML의 `failed=0`을 확인한다 | T1·T3·T6·T7 |
| D4 | **조건 레지스트리는 이번 범위에서 뺀다.** 스펙 §3에 없고, 저작 쪽 `ConditionSpec.ToCondition` switch는 주석으로 의도가 명시된 결정이다(`EffectSpec.cs:20`). 필요하면 대기열의 별도 작업으로 다룬다 | T2a |
| D5 | **카드 JSON의 형식 버전 필드는 `cardFormat`으로 부른다.** `schemaVersion`은 편집 도구가 자기 저장 형식(`index.html:513`의 `SCHEMA_VERSION = 7`)에 이미 쓴다 | T2a·T2b·T7 |
| D6 | **편집 도구는 새 형식의 편집까지 지원한다(범위 B).** 편집 도구에는 카드 설명 생성 기능이 없으므로 그 부분은 제외한다. 게임 쪽 설명(`DescriptionComposer`)은 T2a가 맡는다 | T2b |
| D7 | **턴 시점의 상태 처리(턴 시작·턴 종료)는 Primary 기원이다.** 독 틱 사망 → 전염 발동이라는 현재 동작(`TurnResolver.cs` 349–374)을 유지한다 | T4·T6 |
| D8 | **치명타 버티기(`SurviveCharges`/`DeathsDoor`)를 지금 전부 제거한다.** 나중에 다른 방식으로 다시 도입할 예정이며, 이번 계획에서 보존하지 않는다 | T0 |

방어 만료 시점을 "다음 턴 준비"로 옮기는 T6 변경은 **동작을 바꾸지 않는다** — 지금도 만료가 턴 종료
틱 뒤에 일어난다. 목적은 상태 처리 시점을 명시적으로 구분하는 것이며, 결과가 같은 것은 의도다.

### 목표·아키텍처·기술

목표: 위치를 매 효과마다 선택하고 효과 직후 직접 반응을 처리하되, 카드 종료에만 승패를 판정하는
일관된 실행 계약을 구현한다. 카드 시작 조건과 소비 결과 의존은 분리한다.
기존 레지스트리·효과 처리기·JSON 변환 경계를 유지한다. 조정자의 책임을 실행 순서로 한정하고
실행선·효과 결과·사망 정리·반응·만료를 각각 독립 계약으로 둔다.
기술: 기존 순수 C#, C# 9, Newtonsoft.Json, NUnit 헤드리스 하니스, 기존 Node 카드 도구 테스트.
기준: `d7f2298fd4f33a22973e1e9dba65fb30b3bd72ef`. 아래 코드 블록은 **향후 추가할 API/테스트의 계획**이며 현재 존재한다고 주장하지 않는다.

### 전역 제약

- 코어는 UnityEngine 미참조. 무작위는 CombatState의 RNG만 사용한다.
- 외부 패키지 추가 없음. 확장은 등록 기반. 중앙 조정자에 새 규칙 책임을 얹지 않는다.
- 코드 작업은 전용 워크트리에서 수행한다. 메인 Assets/Packages/ProjectSettings 수정 금지.
- 다중 적 정책·소유자 연결은 사용자의 별도 작업이므로 수정 범위에서 제외한다.
- 런·유산·세이브·표현 연출·영구 변형 체계의 구현도 범위 밖이다.
- 조작 카드의 사용 조건/비용·드로우 경제·시드 순서를 불필요하게 변경하지 않는다.
- 소멸 및 아직 없는 전역 관찰자 능력의 콘텐츠 추가는 하지 않는다. 확장 인터페이스는 닫지 않는다.
- 각 단계 Tools/verify.sh --quick, 최종 Tools/verify.sh 전체 통과. JSON/편집 도구 변경은 노트북 왕복도 필수.
- `Tools/verify.sh`는 Unity 레이어(`Assets/Unity`, `Assets/Tests`)를 컴파일하지 않는다. D3의 배치 검증을 생략하지 않는다.
- HTML/상세 불일치 또는 이 계획과 별도 다중 적 변경 충돌은 임의로 해석하지 않고 해당 경계를 보고한다.
- 커밋 제목·본문은 한국어. master 머지는 별도 사용자 승인 필요. 이 계획은 머지 승인이 아니다.

### 현재 구조의 근거

| 관찰 | 파일 |
|---|---|
| 카드 대상 스냅샷, 효과별 조건 평가, 마지막에 턴 승패 | `Assets/Core/Combat/TurnResolver.cs:26`, `:66`, `:87` |
| 조회마다 정렬하는 실행선 | `Assets/Core/Combat/FutureZone.cs:20` |
| 효과가 카드 전체를 취소할 수 있음 | `Assets/Core/Effects/IEffectHandler.cs:37` |
| 효과 결과 대신 카드 누적 소비를 기록 | `Assets/Core/Effects/ConsumeStatusHandler.cs` |
| 상태를 교체하면 맨 뒤에 삽입 | `Assets/Core/Status/StatusBag.cs:14` |
| 피해 배율과 흡수의 구분 | `Assets/Core/Status/StatusDamageFold.cs:10` |
| 사망 능력인 전염의 기존 훅 | `Assets/Core/Status/ContagionBehavior.cs:13` |
| 콘텐츠 → 정의 변환 경계 | `Assets/Core/Authoring/CardSpecMapper.cs:12` |
| 스키마 테스트가 불일치 시 파일을 쓰고 실패 | `Assets/Core/Tests/EditMode/AuthoringSchemaExportTests.cs:23` |
| 카드 설명이 효과별 조건을 읽음 | `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs:47` |

### 작업 의존 관계와 완료 기준

T0 → T1 → T2a → T2b → T3 → T4 → T5 → T6 → T7 순서로 실행한다. T2a·T2b에서 정의·소비·콘텐츠·도구를
전환해 다음 작업들이 한 가지 계약만 소비하도록 한다. T2a 커밋과 T2b 커밋 사이에는 편집 도구가 새 형식
카드를 읽기 전용으로 띄운다. 이 상태로 머지하지 않는다. 각 작업은 아래 실패 테스트와 수용 행렬을
통과한 상태로 커밋한다. 중간 커밋은 완성된 전체 게임 규칙을 뜻하지 않는다.
신규 C# 파일의 Unity `.meta`는 저장소 패턴대로 포함한다. 코드 변경 시 새 파일의 원본과 meta를 함께 검토한다.

### T0. 치명타 버티기 제거 (D8)

완료(2026-09-18, `f013518`).

기준에서 확인한 사용처(구현 착수 시 다시 grep한다):
- 규칙: `Assets/Core/Combat/PartyMember.cs:22`(`SurviveCharges`), `:45`(치명타를 충전으로 버티고 `DamageOutcome.DeathsDoor` 반환),
  `Assets/Core/Combat/TurnResolver.cs:181–209`(스냅샷 비교로 `DeathsDoorSurvived` 발생)
- 이벤트·로그: `Assets/Core/Events/ResolutionEvent.cs`의 `DeathsDoorSurvived`, `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs:131`
- 데이터 경로: `Assets/StreamingAssets/Content/Characters/member_a.json:7`·`member_b.json:7`의 `surviveCharges`,
  `Authoring/Characters/CharacterSpec.cs:18`·`CharacterContentLoader.cs:108·119`·`CharacterContentCatalog.cs:18·34`,
  `Simulation/Run/RunMember.cs:15`·`RunSetup.cs:24`·`CombatNode.cs:100`, `Simulation/PartyMemberLoadout.cs:14`,
  `Simulation/DeckCombatSession.cs:132·478`
- 테스트: `PartyMemberTests`, `DeckPoolCharacterLoaderTests`, `RunSetupTests`, `PartyDeckCombatSessionTests`,
  `DeckPoolCharacterContentTests`, `CombatLogTests`, `CardCancellationTests`
- Unity 레이어와 편집 도구에는 참조가 없다(기준 시점 grep).

- [x] 치명타가 곧바로 사망시키는 실패 테스트를 먼저 쓴다(파티원 HP 3, 피해 5 → 사망, `DeathsDoorSurvived` 없음).
- [x] 필드·이벤트·로그 문구·로더 검증·캐릭터 JSON 키를 함께 제거한다. `DamageOutcome`에서 `DeathsDoor`를 뺀다.
  JSON 키만 남기면 로더가 모르는 키로 거부할 수 있으므로 데이터와 코드를 같은 커밋에서 지운다.
- [x] 치명타 버티기만 검증하던 테스트는 지운다. 다른 규칙을 검증하면서 충전을 곁들인 테스트는 충전을 빼고 기대값을 다시 확인한다.
- [x] [전투 노드 한 사이클](../specs/2026-09-15-combat-node-cycle-design.md)과
  [백로그](2026-07-16-architecture-refactor-backlog.md)의 `SurviveCharges` 언급에 "2026-09-18 제거" 표시를 단다.
- [x] `Tools/verify.sh --quick` 통과 후 커밋: `refactor(core): 치명타 버티기 충전을 제거한다`.

### T1. 현재 자리를 보존하는 실행선과 실행 이력

완료(2026-09-18). 구현 중 결정: 주인 사망으로 빠진 카드는 `CardCancelled(OwnerDied)` 대신 새 이벤트
`CardRemoved`로 기록한다(사용자 결정). `CardCancelled`는 레일 강조와 재생 비트 시작으로 해석되므로
(`RailCardHighlightPresenter.cs:43`, `TimelineBeatPlanner.cs:73`) 제거 시점에 쓰면 차례가 오지 않은 카드가
강조된다. `CardCancellationReason.OwnerDied`는 삭제했다. 개입 핸들러는 실행선에 없는 카드를 `CanApply`에서 거부한다.
`GoblinParityTests`의 고정 서명은 이 이벤트 교체만큼 갱신했다.
교환 후 삽입은 구현 뒤 사용자 결정으로 바꿨다(2026-09-18): 교환된 카드는 서로의 자리 진영까지 물려받고,
새 카드는 교환을 모르는 것처럼 자리를 찾는다(스펙 §6). 아래 체크리스트의 "마지막 플레이어 뒤" 항목은 이 결정으로 대체됐다.

수정:
- `Assets/Core/Combat/FutureZone.cs`
- `Assets/Core/Combat/ExecutionCardInstance.cs`
- `Assets/Core/Conditions/ResolutionContext.cs`
- `Assets/Core/Conditions/ConditionEvaluator.cs`
- `Assets/Core/Intervention/ChangeExecutionOrderHandler.cs`
- `Assets/Core/Intervention/SwapExecutionOrderHandler.cs`
- `Assets/Core/Combat/TurnResolver.cs` (목록 제거와 반복자 연결만)
테스트: `Assets/Core/Tests/EditMode/FutureZoneTests.cs`, `PreviousExecutedCardConditionTests.cs`,
`CardCancellationTests.cs`, `ConditionEvaluatorTests.cs`.

입력: 기존 ExecutionCardInstance, Def.Side, ExecutionOrder.
출력: FutureZone의 `MoveTo(ExecutionCardInstance card, int order)`,
`SwapPositions(ExecutionCardInstance first, ExecutionCardInstance second)`,
`RemovePendingOwnedBy(string ownerId)`.
ExecutionCardInstance에 실행 상태 `Pending / Executing / Executed / Removed`를 추가한다.
ResolutionContext는 활성 실행선과 실행 이력을 질의하며 고정된 Order 사본을 갖지 않는다.
직전 이력은 현재 카드의 조건을 읽은 후 현재 카드 실행 사실을 추가한다.

- [x] 기존 `FutureZoneTests.Card` 헬퍼를 사용해 아래 실패 테스트를 추가한다.

```csharp
[Test]
public void Moving_to_an_occupied_number_joins_after_existing_peers()
{
    var zone = new FutureZone();
    var a = Card("a", 6);
    var b = Card("b", 5);
    zone.Add(a);
    zone.Add(b);
    zone.MoveTo(a, 5);
    CollectionAssert.AreEqual(new[] { "b", "a" },
        zone.ResolutionOrder().Select(c => c.Def.Id).ToArray());
}

[Test]
public void Exact_swap_preserves_enemy_before_player_on_equal_numbers()
{
    var zone = new FutureZone();
    var p = Card("p", 5, Side.Player);
    var e = Card("e", 5, Side.Enemy);
    zone.Add(p);
    zone.Add(e);
    zone.SwapPositions(p, e);
    CollectionAssert.AreEqual(new[] { "e", "p" },
        zone.ResolutionOrder().Select(c => c.Def.Id).ToArray());
}
```

- [x] `Tools/verify.sh --quick` 실행. 새 API 부재 또는 순서 단언 실패를 확인한다.
- [x] `Add`와 Preview가 같은 삽입 위치 계산을 사용하게 한다. `ResolutionOrder()`는 현재 순서를 읽기 전용으로 반환한다.
  `MoveTo`는 제거 후 번호를 바꾸고 재삽입한다. 같은 번호로의 무변화 이동은 자리를 유지한다.
  `SwapPositions`는 리스트 위치와 번호를 함께 교환하고 전역 재정렬하지 않는다.

```csharp
// SwapPositions 내부. IndexOf 실패·동일 카드 입력은 상태 변경 전에 거부한다.
(_cards[firstIndex], _cards[secondIndex]) = (_cards[secondIndex], _cards[firstIndex]);
(first.ExecutionOrder, second.ExecutionOrder) = (second.ExecutionOrder, first.ExecutionOrder);
```

- [x] 번호 구간에 새 플레이어를 넣으면 마지막 플레이어 뒤(없으면 구간 앞), 새 적은 구간 뒤에 삽입한다.
  기존 교환 순서를 유지한다. 세션의 배치 미리보기와 실제 삽입이 일치하는 테스트를 추가한다.
- [x] 사망 제거는 Pending만 제거한다. Executing/Executed는 남긴다. 반복 중 컬렉션을 변경할 수 있으므로
  TurnResolver는 아직 실행하지 않은 다음 항목을 조회하는 반복으로 전환한다.
- [x] 조건 평가에서 취소 여부를 직접 검사하는 분기를 제거한다. 배치 질의는 공통 실행선,
  이전 실행 조건은 실행 이력만 사용한다. 기존 인접 조건을 전부 이력 조건으로 치환하지 않는다.
- [x] 사망으로 제거한 카드에도 기존처럼 `CardCancelled`(사유 `OwnerDied`)를 기록한다. 지금은 그 카드
  차례에 기록되지만, 제거 후에는 차례가 오지 않으므로 **제거 시점**에 기록한다. Unity 소비자
  `Assets/Unity/Scripts/Battle/Playback/RailCardHighlightPresenter.cs:43`이 이 이벤트로 레일 카드를 처리하므로,
  구현 전에 그 파일을 읽고 기록 시점이 앞당겨져도 표시가 성립하는지 확인한다. 성립하지 않으면 멈추고 보고한다.
- [x] V03~04: 실행 완료 카드 소유자 사망 시 완료 카드는 남고 미실행만 제거됨, 무효과 카드도 직전 실행에
  포함됨을 추가한다. `Tools/verify.sh --quick`과 D3의 Unity EditMode 배치가 통과한 뒤 커밋:
  `refactor(core): 실행선 자리와 실행 이력을 일관되게 관리한다`.

### T2a. 카드 정의·효과 결과·콘텐츠 계약 전환

완료(2026-09-18). 구현 중 결정과 계획과 달라진 점 — T2b·T3 담당자는 먼저 읽는다:
- **런타임 위치 축은 T3로 미뤘다.** `CardDefinition.AllyTarget/EnemyTarget`·`EffectData.TargetFaction`은 만들지 않았다.
  저작 스펙이 카드 축(`targets`)과 효과 진영(`targetFaction`)을 갖고, `EffectSpec.ToEffectData(cardSide, target)`가
  처리기가 지금 읽는 필드(`TargetSelector`, `ApplyStatusPayload.Target`)로 옮긴다. 처리기는 바뀌지 않았다.
  T3가 효과 단위 선택을 넣을 때 런타임 축을 추가하고 이 변환(`ApplyStatusSpec.Build` 등)을 지운다.
- 조건 레지스트리는 만들지 않았다(D4). `ConditionSpec`은 카드 단위 `StartConditionSpec`(kind·n)이 됐다.
- **보상 무효(`RewardNullified`)는 카드 시작 조건의 성공에만 걸린다.** `requires`로 옮긴 소비 보상은 조건이 아니므로
  무효화되지 않는다. 이 효과를 쓰는 게임 카드는 없다(테스트 전용).
- **`CardResolved.ConditionTier`는 카드 시작 조건의 결과다.** `toxic_reclaim`은 이제 Basic으로 기록된다(피해·상태 동일).
  `GoblinParityTests` 고정 서명을 이만큼 갱신했다.
- `condensed_burst`는 `scaleBy`로 피해가 늘어나며 `CardBuffGranted/Consumed(DamageBonus)` 이벤트를 더 내지 않는다.
  `_pendingDamageBonus`는 `GrantNextPlayerDamageCardBonusHandler`용으로 남겼다.
- 여러 대상을 소비하는 경우의 결과 집계는 스펙 §5가 정하지 않았다 — 지금은 합산하며 게임 카드는 단일 대상뿐이다.
- 검증 추가: 쓰이지 않는 카드 축, 시작 조건 없는 `successEffectValue`·`skipOnBasic`, 대상 없는 효과의 `targetFaction`은 오류다.
  변환 효과 ID는 `e0`, `e1`, …이다.
- **C# 샘플 `CounterStance`를 지웠다(사용자 결정).** 효과마다 조건이 달라 카드당 단일 시작 조건으로 옮길 수 없었고,
  검증 내용은 다른 테스트가 모두 덮는다. `SampleMultiTurnScenarios`·`CounterStanceTests`·Unity `PlaytestKoreanText` 항목을 함께 지웠다.
- 편집 도구 스키마(T2b 입력): `effects[]`에 `targeted`·`producesConsumption`·`order`(공통 필드 포함 키 순서)를 두고,
  공통 필드는 `effectCommonFields`로 한 번만 낸다. `selectors`·`statusTargets`를 지우고 `factions`·`ranges`를 더했다.
  `condition`은 `StartConditionSpec`(kind + n)이다. 편집 도구 테스트는 이 스키마 때문에 T2b 전까지 실패한다.
- 설명 문구: `condensed_burst`는 "독 최대 3 소비. 피해 2 (소비 1당 +2). 독 1."이 된다(가산이 피해 문장에 붙음).
  나머지 29장 문구는 그대로다.

수정:
- `Assets/Core/Cards/CardDefinition.cs`, `Assets/Core/Combat/ExecutionCardInstance.cs`
- `Assets/Core/Effects/ConsumeStatusHandler.cs`, `ConsumeStatusPayload.cs`, `DamageHandler.cs`, `IEffectHandler.cs`
- `Assets/Core/Authoring/CardSpec.cs`, `EffectSpec.cs`, `CardSpecMapper.cs`, `AuthoringValidator.cs`, `CardContentLoader.cs`
- `Assets/Core/Authoring/Json/CardSpecJsonConverter.cs`, `EffectSpecJsonConverter.cs`
- `Assets/Core/Authoring/Specs/ConsumeStatusSpec.cs`, `DamageSpec.cs`, `ApplyStatusSpec.cs`
- `Assets/Core/Simulation/Descriptions/DescriptionComposer.cs`, `BuiltInEffectDescriptionHandlers.cs`, `KoreanDescriptionGrammar.cs`
- `Assets/StreamingAssets/Content/Cards/*.json` — **29장 전부** 새 형식으로 변환한다(아래 변환 규칙)
- `Tools/card-idea-notebook/authoring-schema.json` — 손으로 고치지 않는다. `AuthoringSchemaExportTests`가 생성한다
생성:
- `Assets/Core/Effects/EffectResult.cs`, `ConsumptionRule.cs`, `EffectResultRequirement.cs`, `EffectResultScaling.cs`
- `Assets/Core/Combat/CardExecutionContext.cs`
- `Assets/Core/Authoring/Json/CardFormatMigration.cs`
테스트: `ConsumeStatusTests.cs`, `ConditionalEffectResolutionTests.cs`, `CardContentJsonTests.cs`,
`CardContentLoaderTests.cs`, `AuthoringSchemaExportTests.cs`; 신규 `ConsumptionRuleTests.cs`.
조건 레지스트리(`ConditionRegistry`·`IConditionHandler`)는 만들지 않는다(D4). 조건 평가는 기존
`ConditionEvaluator`를 쓰고, 이 작업에서는 평가 **시점**만 카드 시작으로 옮긴다.

계약:
- CardDefinition: `Condition StartCondition`, `CardTargetKey? AllyTarget`, `CardTargetKey? EnemyTarget`.
  기존 CardTargetKey/CardTargetFaction은 `Assets/Core/Cards/CardTarget.cs`에서 재사용한다.
- EffectData: `string Id`, `CardTargetFaction? TargetFaction`, `EffectResultRequirement Requirement`,
  `EffectResultScaling Scaling`. 없으면 각각 null. 카드 조건에 따른 값·생략 설정(`SuccessEffectValue`·`SkipOnBasic`)은 효과별 유지.
- EffectResultRequirement: `string SourceEffectId`, `int MinimumConsumed`. 앞 효과의 실제 소비량이 모자라면 이 효과를 수행하지 않는다.
- EffectResultScaling(D2): `string SourceEffectId`, `int PerConsumed`. 이 효과의 수치에 `앞 효과의 실제 소비량 × PerConsumed`를 더한다.
  Requirement와 같은 참조 검증(앞 효과·소비 생산 효과만)을 받는다.
- EffectResult: `bool Applied`, `int ConsumedAmount`, `int DamageDealt`와 대상별 결과/사건 목록.
- CardExecutionContext: 카드·상태·시작 조건 결과·효과 ID별 결과표. `Record(string id, EffectResult result)`,
  `Get(string id)`를 제공하며 중복 기록·없는 결과 조회는 내부 계약 위반으로 예외 처리한다.
- ConsumptionRule.Take(int available, int requested, ConsumptionMode mode) → int. mode는 Exact/UpTo.
  음수와 requested<=0는 로딩 및 실행 경계에서 거부한다.
- 카드 JSON 최상위의 형식 버전 필드는 `cardFormat`이다(D5). 새 형식은 `2`, 필드 없음은 구형이다.

구형 → 새 형식 변환 규칙(판단 없이 규칙만으로 결정되어야 한다. 규칙으로 안 되는 카드가 나오면 멈추고 보고한다):

| 구형 | 새 형식 |
|---|---|
| 효과마다 붙은 `selector`·`target` | 카드의 `targets.ally`·`targets.enemy` 두 축 + 효과의 `targetFaction` |
| 효과 하나에만 붙은 조건 `kind`·`n` | 카드 `startCondition` |
| 그 조건의 `successEffectValue`·`skipOnBasic` | 그 효과에 그대로 남김 |
| `ConsumedStatusAtLeast n` | 카드 조건이 아니라 해당 효과의 `requires { sourceEffectId: <카드 안 유일한 소비 효과>, minimumConsumed: n }` |
| `consume_status`의 `maxAmount` | `amount` + `mode: "UpTo"` (현재 규칙이 UpTo다, `ConsumeStatusHandler.cs:48`) |
| `damageBonusPerConsumed: k` (`condensed_burst`) | 뒤따르는 첫 `damage` 효과의 `scaleBy { sourceEffectId: <그 소비 효과>, perConsumed: k }` |
| 효과 ID 없음 | 배열 위치로 결정론적으로 생성(`e0`, `e1`, …), 이후 저장 때 보존 |

기준 시점 확인(구현 착수 시 다시 확인): 조건 있는 카드는 distill·foresight·last_drop·riposte·sly_jab·toxic_reclaim이고
모두 조건이 효과 하나에만 있다. 소비 효과는 카드마다 하나다. 따라서 위 규칙으로 29장 모두 결정된다.

- [x] 먼저 소비 규칙 실패 테스트를 추가한다.

```csharp
[TestCase(2, 3, ConsumptionMode.Exact, 0)]
[TestCase(3, 3, ConsumptionMode.Exact, 3)]
[TestCase(2, 3, ConsumptionMode.UpTo, 2)]
[TestCase(5, 3, ConsumptionMode.UpTo, 3)]
public void Consumption_preserves_the_authored_payment_rule(
    int available, int requested, ConsumptionMode mode, int expected)
{
    Assert.AreEqual(expected, ConsumptionRule.Take(available, requested, mode));
}
```

- [x] `Tools/verify.sh --quick`에서 새 타입 부재 확인 후 구현한다.

```csharp
public static int Take(int available, int requested, ConsumptionMode mode)
{
    if (available < 0 || requested <= 0) throw new ArgumentOutOfRangeException();
    if (mode == ConsumptionMode.Exact) return available >= requested ? requested : 0;
    if (mode == ConsumptionMode.UpTo) return Math.Min(available, requested);
    throw new ArgumentOutOfRangeException(nameof(mode));
}
```

- [x] 카드 조건 평가를 시작에 한 번으로 이동한다. 효과 하나에 붙은 조건은 카드 조건으로 승격한다.
  `ConsumedStatusAtLeast`는 일반 조건에서 빼고 해당 소비 효과를 가리키는 Requirement로 옮긴다.
  두 소비 중 특정 결과만 읽는 V16, 소비 0의 정상 미적용, V17 조건 고정 테스트를 작성한다.
  서로 다른 일반 조건 또는 소비 출처가 여러 개인 구형 데이터는 자동 추정하지 않고 변환 오류로 보고한다.
- [x] `condensed_burst` 동작 보존 테스트(D2): 독 3 보유 적에게 사용 → 소비 3, 피해 `2 + 3×2 = 8`.
  독 1 보유 → 소비 1, 피해 4. 독 0 → 소비 0, 피해 2. 변환 전 기준 코드에서 같은 입력의 결과를 먼저 기록해 비교한다.
  `ConsumeStatusPayload.DamageBonusPerConsumed`와 소비 처리기의 `AddPendingDamageBonus` 호출을 제거한다.
  `GrantNextPlayerDamageCardBonusHandler`의 적립 경로는 건드리지 않는다.
- [x] 무버전 JSON은 구형으로 인식한다. 위 변환 규칙으로 로딩 경계에서 변환하며 `cardFormat`은 항상 출력한다.

```json
{
  "cardFormat": 2,
  "id": "payment_fixture",
  "name": "소비 보상 검증",
  "side": "Player",
  "category": "Execution",
  "energyCost": 1,
  "baseExecutionOrder": 5,
  "targets": { "enemy": "FrontOne" },
  "effects": [
    { "id": "pay", "kind": "consume_status", "targetFaction": "Enemy",
      "status": "poison", "amount": 1, "mode": "Exact" },
    { "id": "reward", "kind": "grant_next_turn_fate", "value": 1,
      "requires": { "sourceEffectId": "pay", "minimumConsumed": 1 } }
  ]
}
```

- [x] 위 JSON은 테스트 fixture이며 신규 게임 카드로 배포하지 않는다. 다음 변형을 테스트한다:
  requires의 pay를 missing으로 변경, reward 자기 참조, 소비가 아닌 damage 결과 참조,
  미래 효과 참조, 중복 ID, 대상 축 미정의, `scaleBy`의 같은 오류들. 모두 파일/카드/효과 경로가 있는 로딩 오류여야 한다.
- [x] 효과별 위치를 모아 카드의 두 축으로 정규화한다. 같은 축의 상충 위치는 로딩에서 거부한다.
  런타임에서 NoValidTarget로 넘기지 않는다. 서로 다른 일반 조건을 성공값이 같다는 이유로 합치지 않는다.
- [x] 저장소 카드 29장을 변환기로 한 번 새 형식으로 다시 쓰고 커밋에 포함한다. 변환 전후로 같은 시드
  `ScenarioRunner`·`MultiTurnRunner`의 **Compare** 결과가 같은지 확인한다(이 작업은 데이터 형식만 바꾸며 규칙은
  시작 조건 평가 시점만 바뀐다 — 차이가 나면 그 카드와 이유를 기록한다).
- [x] 스키마 테스트가 갱신한 authoring-schema.json을 검토 후 재실행한다. 구형 샘플의 import 테스트와
  신규 형식 export 테스트를 따로 둔다.
- [x] 편집 도구 테스트(`index.test.mjs`)는 이 커밋에서 깨질 수 있다. `Tools/verify.sh --quick`(헤드리스) 통과 후
  커밋하고, 전체 `Tools/verify.sh`는 T2b에서 통과시킨다. 커밋: `refactor(core): 카드 조건과 소비 보상의 데이터 계약을 분리한다`.

### T2b. 편집 도구의 새 카드 형식 편집 지원 (D6)

완료(2026-09-18, T2a와 한 커밋 — 사용자 결정). `pre-commit`이 `Tools/card-idea-notebook/`가 바뀌면 편집 도구 테스트를
돌려([`.githooks/pre-commit`](../../../.githooks/pre-commit) 102줄), 스키마만 바뀐 T2a 단독 상태는 커밋할 수 없었다.
구현 중 결정:
- 편집 도구는 구형(cardFormat 없음) 카드를 읽지 않고 이유를 보여준다. 변환은 게임의 CardFormatMigration만 한다.
- 노트북 저장 형식 버전(`SCHEMA_VERSION`)을 8로 올렸다. 버전 1·7의 미반영 편집분(카드 형식 1 모델)은 지우지 않고
  `fate-weaver.card-idea-notebook.pending.card-format-1-backup`으로 옮긴 뒤 알린다. 자동 변환은 하지 않는다.
- 저장 전 검증은 게임 로더와 같은 효과 ID·대상 축·결과 참조·조건 필드를 잡는다. **효과 종류별 (진영, 위치) 허용
  조합**(예: 피해는 상대 진영만)은 스키마에 없어 게임 로더만 검사한다.
- 스키마에 `always`(기본값이어도 항상 쓰는 필드, 예: consume_status의 mode)를 더했다.
- 화면: 카드 편집부에 아군/적 위치 축과 시작 조건, 효과 행에 id·대상 진영·(시작 조건이 있을 때) 성공 수치·생략,
  결과 참조 두 칸(소비했다면·소비량 비례 — 앞의 소비 효과만 고를 수 있다).
- 브라우저 확인: 인앱 브라우저가 폴더 연결(File System Access API)을 지원하지 않아, 메모리 폴더와 메모리
  localStorage를 주입한 임시 페이지로 확인했다 — 29장 읽기(검증 오류 0), condensed_burst의 scaleBy 가산 2→3 편집,
  반영 시 그 파일 한 개만 한 줄 바뀌어 쓰임. **실제 Chrome에서 저장소 폴더를 연결해 보는 확인은 사용자 몫이다.**

편집 도구는 저장소 카드 JSON을 직접 읽고 쓰며(`Tools/card-idea-notebook/index.html:1286`), 모르는 키가 있는
카드는 부팅 거부 오류와 함께 읽기 전용으로 띄운다(`index.html:1015`, [노트북 설계](../specs/2026-08-05-card-authoring-json-notebook-design.md) §11.3).
T2a 이후 모든 카드가 새 키를 가지므로 이 작업 없이는 도구로 카드를 편집할 수 없다.
효과 파라미터 폼은 `authoring-schema.json`에서 자동으로 만들어지므로(`index.html:454`) 효과 안 파라미터 변경
(`amount`·`mode`, 효과의 `selector` 제거)은 스키마 갱신으로 따라온다. 아래는 손으로 고쳐야 하는 부분이다.
편집 도구에는 카드 설명 생성 기능이 없으므로 설명 관련 작업은 없다.

수정: `Tools/card-idea-notebook/index.html`, `index.test.mjs`.

- [x] 카드 단위 새 키 읽기·쓰기: `cardFormat`, `targets`(두 축), `startCondition`. `readCardJson`(`:560`)과
  `writeCardJson`(`:863`)은 지금 스칼라 필드만 처리하므로 객체 필드를 `effects`처럼 따로 다룬다.
  키 순서는 스키마의 `cardFields`를 따른다. 편집 도구 자체의 저장 형식 `SCHEMA_VERSION`(`:513`)과 섞지 않는다.
- [x] 조건 편집을 효과에서 카드로 옮긴다. 지금은 `conditionBlock`(`:2543`)·`setEffectCondition`(`:763`)이 효과마다 조건을
  단다. 조건 종류와 `n`은 카드 편집부로, `successEffectValue`·`skipOnBasic`은 효과 행에 남긴다.
- [x] 효과 ID: `readEffectEntry`(`:534`)·`writeEffectEntry`(`:837`)가 `id`를 보존한다. 효과 추가·복제(`:693`·`:712`)는
  카드 안에서 겹치지 않는 ID를 만들고, 이동(`:725`)은 ID를 유지한다.
- [x] 결과 참조 편집: 효과 행에 `requires`와 `scaleBy` 편집부를 둔다. 참조 대상은 **앞쪽의 소비 효과** 드롭다운으로만 고른다.
- [x] 저장 전 검증(`validateContent`, `:989`): `maxAmount ≥ 1` 규칙을 `amount ≥ 1`로 바꾸고, 자기 참조·뒤쪽 참조·
  소비가 아닌 효과 참조·없는 ID 참조·같은 축 위치 충돌을 오류로 잡는다. 게임 로더(T2a)와 같은 것을 거부해야 한다.
- [x] `index.test.mjs`의 구형 카드 fixture를 새 형식으로 바꾸고, 위 각 항목의 테스트를 더한다. 저장소 카드 29장
  전부가 읽기 → 쓰기에서 바이트가 같은지(왕복) 확인한다. 외형 개편은 하지 않는다.
- [x] 브라우저에서 도구를 열어 카드 하나(`condensed_burst`)의 `scaleBy`를 편집·저장해 보고 결과 JSON을 확인한다.
- [x] `Tools/verify.sh` 전체 통과 후 커밋: `feat(tools): 편집 도구가 새 카드 형식의 조건과 결과 참조를 편집한다`.

### T3. 효과 단위 위치 선택과 미적용 분리

수정: `Assets/Core/Combat/CardTargetSnapshot.cs`, `TurnResolver.cs`,
`Assets/Core/Effects/IEffectHandler.cs`, `DamageHandler.cs`, `ApplyStatusHandler.cs`, `MoveFormationHandler.cs`,
`ConsumeStatusHandler.cs`, `TriggerStatusHandler.cs`.
생성: `Assets/Core/Effects/EffectExecutor.cs`, `Assets/Core/Combat/EffectTargetResolver.cs`.
테스트: `CardTargetSnapshotTests.cs`, `FormationTargetingIntegrationTests.cs`, 신규 `EffectTargetResolverTests.cs`.

입력: T2a의 카드 위치 규칙과 효과 대상 축.
출력: `EffectTargetResolver.Resolve(CombatState state, ExecutionCardInstance card, CardTargetKey key)`는
`EffectTargetSnapshot`을 반환한다. 기존 PartyTargets/EnemyTargets 조회 계약을 유지하되
CardTargetSnapshot.cs를 효과 단위 값 객체로 이전한다.
`EffectExecutor.Apply(CardExecutionContext context, EffectData effect)`는 EffectResult를 반환한다.
이 단계의 호출자는 기존 TurnResolver이며 T5에서 CardExecutor로 옮긴다.

- [ ] 신규 EffectTargetResolverTests에 아래 테스트를 추가한다. NUnit, System, System.Linq,
  FateWeaver.Core.Cards/Combat 네임스페이스와 기존 TestContent를 사용한다.

```csharp
[Test]
public void A_new_effect_selects_the_new_front_enemy()
{
    var state = new CombatState(TestContent.Statuses());
    state.AddSoloPlayer(10);
    var a = new Enemy("a", 3);
    var b = new Enemy("b", 10);
    state.Enemies.Add(a);
    state.Enemies.Add(b);
    var card = new ExecutionCardInstance(new CardDefinition(
        "hit", "hit", Side.Player, 1, Array.Empty<EffectData>()));
    var key = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne);
    var resolver = new EffectTargetResolver();
    Assert.AreSame(a, resolver.Resolve(state, card, key).EnemyTargets(key).Single());
    a.Hp = 0;
    Assert.AreSame(b, resolver.Resolve(state, card, key).EnemyTargets(key).Single());
}
```

- [ ] `Tools/verify.sh --quick` 실패 확인 후 EffectExecutor에서 매번 대상을 확보한다.
  처리기는 이번 대상 목록만 받으며 구형 TargetId 선택 분기를 제거한다.
- [ ] 대상 0명은 EffectResult.Applied=false로 반환한다. CardCancellationReason을 변경하지 않는다.
  처리기가 대상 부재로 카드 전체를 중단하는 분기를 제거한다.
- [ ] V05 통합 테스트: a HP3/b HP10, damage3 두 번 → a 사망/b HP7.
  V06: b를 앞으로 이동 → FrontOne 방어는 b에게 적용. 마지막 적 처치 이후 아군 효과는 T5와 통합한다.
- [ ] All은 효과 시작 목록을 한 번 확보해 전체에 적용한다. 적용 도중 목록 구성을 다시 선택하지 않는다.
- [ ] `CardResolved.TargetId`는 이벤트 필드로 유지하고 의미를 "처음 적용된 효과의 첫 대상(없으면 null)"으로
  정의한다. Unity 소비자 `Assets/Unity/Scripts/Battle/Playback/CardResolvedPresenter.cs:46`·`:52`가 이 필드를 읽으므로,
  구현 전에 그 파일을 읽고 새 의미로 표시가 성립하는지 확인한다. 성립하지 않으면 멈추고 보고한다.
- [ ] `Tools/verify.sh --quick`과 D3의 Unity EditMode 배치가 통과한 뒤 커밋:
  `refactor(core): 효과마다 위치를 해석하고 미적용을 분리한다`.

### T4. 공통 사건·직접 반응·공통 피해 경로

수정: `Assets/Core/Effects/DamageHandler.cs`, `ApplyStatusHandler.cs`, `MoveFormationHandler.cs`,
`TriggerStatusHandler.cs`, `EffectExecutor.cs`, `Assets/Core/Status/IStatusBehavior.cs`, `StatusDamageFold.cs`,
`PoisonBehavior.cs`, `ContagionBehavior.cs`, `Assets/Core/Events/ResolutionEvent.cs`,
`Assets/Core/Registries/CombatRegistries.cs`.
생성: `Assets/Core/Events/CombatSignal.cs`, `CombatSignalKey.cs`, `Assets/Core/Status/ReactionDispatcher.cs`,
`ReactionRegistry.cs`, `IReactionHandler.cs`, `Assets/Core/Effects/DamageService.cs`,
`Assets/Core/Combat/DeathProcessor.cs`.
테스트: `StatusHookSurfaceTests.cs`, `ContagionStatusTests.cs`, `PoisonStatusTests.cs`, 신규 `ReactionPipelineTests.cs`.

계약:
- CombatSignalKey는 문자열 키 값 타입. 사건 종류를 중앙 enum 확장으로 처리하지 않는다.
- EffectOrigin은 Primary/Reaction. CombatSignal은 Key, SourceId, TargetId, Amount,
  TargetOrdinal, Sequence를 보관하며 사건 시점 값을 고정한다.
- IReactionHandler: Key, SignalKey,
  `bool CanReact(CombatState state, StatusInstance instance, CombatSignal signal)`,
  `IReadOnlyList<EffectData> EffectsFor(StatusInstance instance, CombatSignal signal)`.
- ReactionRegistry: `Register(IReactionHandler handler)`, SignalKey별 조회. 중복 Key 거부.
- ReactionDispatcher: `CanDispatch(EffectOrigin origin)` 및
  `Dispatch(CardExecutionContext context, IReadOnlyList<CombatSignal> signals)`.
- EffectExecutor.Apply에 EffectOrigin 인자를 추가한다. 기본 호출은 Primary,
  ReactionDispatcher는 반드시 Reaction을 전달한다.
- DamageService는 피해 원인(공격/상태), Piercing(방어 흡수 우회), 배율 적용 여부를 따로 받는다.
  **독은 원인=상태, Piercing, 배율 미적용으로 호출한다(D1).** 지금 독은 방어·배율을 모두 거치지 않으며
  (`PoisonBehavior.cs:32`, `TurnResolver.cs`의 `DealDamage`), 전환 후에도 결과가 같아야 한다.
  공통 경로로 옮긴다고 독에 취약(150%)이 붙으면 안 된다.
- DeathProcessor는 효과 전후 생존 차분으로 사망 사건·실행선 제거를 한 번 수행한다.
  사망 능력을 직접 호출하지 않고 CombatSignal을 반환한다. 덱 제거 연결은 T5에서 수행한다.

- [ ] 아래 경계 테스트와 표의 통합 사례를 먼저 추가한다. 단순 허용 테스트만으로 완료하지 않는다.

```csharp
[TestCase(EffectOrigin.Primary, true)]
[TestCase(EffectOrigin.Reaction, false)]
public void Only_primary_effects_open_a_reaction_boundary(EffectOrigin origin, bool expected)
{
    Assert.AreEqual(expected, ReactionDispatcher.CanDispatch(origin));
}
```

- [ ] `Tools/verify.sh --quick` 실패 확인 후 다음 경계를 구현한다.

```text
EffectExecutor:
  handler.Apply → 전체 대상의 변경 결과
  DeathProcessor → 사망과 제거 (능력 발동이 아님)
  Primary일 때만 ReactionDispatcher.Dispatch
  EffectResult 반환
ReactionDispatcher:
  이번 사건의 후보 목록 확보
  후보별 현재 CanReact 확인
  Reaction 기원으로 효과 적용
  반응이 생성한 사건은 기록하되 Dispatch를 재호출하지 않음
```

- [ ] 사건 당사자의 후보는 효과 시작 대상 목록 순서, 보유자 내에서는 상태 부여 순서로 안정화한다.
  이번 경계에서 후보를 확보하고, 도중에 제거된 상태는 호출 전에 확인한다. 새로 부여한 능력이
  이미 발생한 같은 사건을 다시 받게 하지 않는다.
- [ ] 공격을 방어로 모두 막아도 Attacked를 발생시킨다. HpDamaged는 실제 HP 감소시에만 발생시킨다.
  BlockGained/StatusApplied/FormationMoved/HolderDied도 해당 상태 변경 경계에서 생성한다.
  사건 종류 추가가 중앙 분기 변경을 요구하지 않는지 테스트용 새 능력으로 검증한다.
- [ ] 반응은 사건 SourceId를 대상으로 참조할 수 있다. 일반 카드의 위치 선택과 분리한다.
  반격은 생존 요건, 사망 능력은 사망 보유자 허용 요건을 사용한다. 사망을 일괄 반응 금지로 처리하지 않는다.
- [ ] 아래 사례를 테스트 전용 처리기로 검증한다. 검증용 반격 수치는 테스트에만 두고 신규 게임 카드는 추가하지 않는다.

| 상황 | 필수 결과 |
|---|---|
| damage3 → block3, 반격2 | 방어 획득 전에 HP2 감소, 이후 방어3 |
| damage3 → damage3, 반격2 | 각 피격 직후 한 번씩 반격 |
| All 피해와 여러 반응 | 전체 HP·사망 반영 후 대상 목록 순서로 반응 |
| 방어 획득에 반응 | 공격 이외 사건도 등록된 능력 발동 |
| Primary 사망과 전염 | 사망 능력 실행 |
| 반격 사망과 전염 | 사망·카드 제거는 실행, 전염은 미발동 |
| 반응 효과의 방어 획득 | 방어는 증가, 획득 반응은 연쇄하지 않음 |
| 방어로 전부 막은 공격 | Attacked 있음, HpDamaged 없음 |
| 방어 5·취약을 가진 적에게 독 3 틱 (D1) | HP 3 감소, 방어 5 유지, 배율 미적용 |
| 턴 종료 독 틱으로 사망한 전염 보유자 (D7) | 사망 능력(전염) 실행 — 턴 시점 처리는 Primary 기원 |

- [ ] 표시 이벤트는 실제 발생 순서대로 기록한다. CardResolved 요약을 HP 변경의 재적용 명령으로 만들지 않는다.
  T7에서 기존 소비자의 요약/개별 이벤트 계약을 확인한다.
- [ ] `Tools/verify.sh --quick` 통과 후 커밋: `refactor(core): 전투 사건에서 직접 반응을 해결한다`.

### T5. 카드를 종료 단위로 만든다

수정: `Assets/Core/Combat/TurnResolver.cs`, `ExecutionCardInstance.cs`, `CombatState.cs`,
`Assets/Core/Simulation/DeckCombatSession.cs`, `Assets/Core/Combat/Deck.cs`, `DeathProcessor.cs`.
생성: `Assets/Core/Combat/CardExecutor.cs`, `CombatOutcomeEvaluator.cs`.
테스트: `TurnResolverTests.cs`, `CardCancellationTests.cs`, `PartyDeckCombatSessionTests.cs`,
신규 `CardCompletionBoundaryTests.cs`.

계약:
- `CardExecutor.Execute(CombatState state, ResolutionContext resolution, ExecutionCardInstance card,
  List<ResolutionEvent> events)` → void. 내부 승패 판정 금지.
- `CombatOutcomeEvaluator.Evaluate(CombatState state)` → Outcome.
- DeathProcessor 구성 시 `Action<string> removeOwnedCards`를 주입해 세션의 Deck.RemoveOwnedBy와 연결한다.
  `Deck`은 Core에 있지만(`Assets/Core/Combat/Deck.cs:86`) 전투 상태가 덱을 소유하지 않고 세션이 소유한다
  (지금은 `DeckCombatSession.cs` 339–360이 타임라인의 `PartyMemberDied`를 훑어 제거한다). 그래서 덱 참조 대신
  동작을 주입한다. 덱 없는 코어 테스트는 빈 동작을 명시적으로 전달한다.
- 기존 TurnEnded 이벤트는 소비자 호환을 위해 유지한다. 이 이벤트 출력 때문에 미실행 TurnEnd 능력을 실행하지 않는다.

- [ ] 아래 패배 우선 테스트와 V11~13을 추가한다. Outcome은 기존 Events 네임스페이스를 사용한다.

```csharp
[Test]
public void Defeat_wins_when_both_sides_are_dead()
{
    var state = new CombatState(TestContent.Statuses());
    state.AddSoloPlayer(1).TakeDamage(1);
    state.Enemies.Add(new Enemy("e", 0));
    Assert.AreEqual(Outcome.Lose, CombatOutcomeEvaluator.Evaluate(state));
}
```

- [ ] `Tools/verify.sh --quick` 실패 확인 후 카드 수행을 CardExecutor로 옮긴다.

```text
시작 전에 Removed이면 실행하지 않음
카드 시작 조건 평가 → Executing으로 실행 사실 기록
각 효과:
  고정 조건 결과와 앞 효과 결과로 이번 값/수행 여부 결정
  EffectExecutor.Apply (사망·직접 반응 포함)
  결과를 효과 ID로 기록
Executed로 전환 (무효과·수행자 사망도 동일)
호출자인 TurnResolver가 카드 종료 승패 판정
```

- [ ] 반응 처리에서 부모 카드의 조건을 재평가하거나 부모 효과 ID의 결과를 덮어쓰지 않는다.
  반응 실행 문맥은 부모 사건을 참조하되 결과표를 분리한다.
- [ ] 현재 수행자 사망은 남은 효과를 중단하지 않는다. 죽은 Self의 일반 회복/방어는 미적용,
  다른 생존 아군 효과는 계속 적용한다. 구현되지 않은 부활 능력을 추가하지 않는다.
- [ ] 마지막 적 처치 후 아군 효과, 카드 내 양측 전멸, 승리 후 독 발동 중단을 테스트한다.
  회복 처리기가 없다면 테스트 전용 효과를 등록해 V12를 검증하며 제품 회복 능력을 임의로 신설하지 않는다.
- [ ] 소유 카드 삭제를 턴 종료 로그 순회에서 즉시 사망 처리로 이동한다. 별도 다중 적 정책 API는 변경하지 않는다.
- [ ] `Tools/verify.sh --quick` 통과 후 커밋: `refactor(core): 카드 종료 시 승패를 확정한다`.

### T6. 공통 만료 시점과 턴 경계

수정: `Assets/Core/Status/StatusLifetime.cs`, `StatusInstance.cs`, `StatusBag.cs`, `IStatusBehavior.cs`,
`Assets/Core/Authoring/Statuses/StatusSpec.cs`, `StatusContentLoader.cs`,
`Assets/Core/Simulation/DeckCombatSession.cs`, `Assets/Core/Combat/TurnResolver.cs`,
`Assets/StreamingAssets/Content/Statuses/block.json`, `Assets/Core/Authoring/Json/StatusSpecJsonConverter.cs`.
생성: `Assets/Core/Status/ExpiryPolicy.cs`, `StatusLifetimePolicy.cs`, `Assets/Core/Combat/CombatPhase.cs`.
테스트: `StatusLifetimeScenarioTests.cs`, `StatusTickPipelineTests.cs`, `StatusContentTests.cs`,
`DeckCombatSessionTests.cs`, 신규 `ExpiryPolicyTests.cs`.

계약:
- CombatPhase는 고정 실행 단계: Prepare/TurnStart/Planning/Resolve/TurnEnd/Cleanup.
  개별 능력 사건은 CombatSignalKey로 확장하고 이 enum에 능력 이름을 추가하지 않는다.
- ExpiryPolicy: Permanent/PhaseVisits/UntilConsumed 모드, 기준 phase, remainingVisits.
- `StatusLifetimePolicy.Visit(StatusInstance instance, CombatPhase phase)` → bool (이번에 만료됐는가).
  상태 이름을 검사하지 않고 정책을 평가한다. 시점 진입 시 존재했던 상태만 방문한다.
- StatusBag 재부여는 같은 위치를 갱신한다. 기존 중첩·수명 갱신 의미는 유지한다.

- [ ] 기존 StatusBag 계약으로 재부여 순서 실패 테스트를 추가한다.

```csharp
[Test]
public void Refresh_keeps_the_original_application_order()
{
    var bag = new StatusBag();
    bag.Add(StatusKeys.Poison, StatusLifetime.Permanent, 1);
    bag.Add(StatusKeys.Block, StatusLifetime.ThisTurn, 2);
    bag.Add(StatusKeys.Poison, StatusLifetime.Permanent, 3);
    CollectionAssert.AreEqual(new[] { StatusKeys.Poison, StatusKeys.Block },
        bag.All.Select(s => s.Key).ToArray());
}
```

- [ ] `Tools/verify.sh --quick` 실패 확인 후 기존 상태의 값/수명을 갱신하고 위치를 유지한다.
  신규 부여만 끝에 삽입한다. 수명 갱신은 StatusInstance의 내부 메서드로 한정한다.
- [ ] block의 JSON을 아래 공통 정책으로 변환한다. 다른 ThisTurn 상태를 전부 함께 바꾸지 않는다.

```json
{ "mode": "PhaseVisits", "phase": "Prepare", "remainingVisits": 1 }
```

- [ ] 구형 ThisTurn은 Cleanup 만료, Turns는 Cleanup 방문 횟수로 변환한다. 발동 시점과는 별개다.
  상태 세기와 남은 횟수를 혼동하지 않는다. count/magnitude 전역 개명 및 런 지속값 변경은 범위 밖이다.
- [ ] BeginTurn은 준비 만료·비용 초기화 → TurnStart 능력 → 승패 판정 → 기존 적 배치/드로우 순으로 연결한다.
  적 배치 본문은 별도 작업의 최신 구현을 보존하며 단일 적 가정을 되살리지 않는다.
- [ ] TurnEnd는 해당 시점 능력을 순차 처리하고 사망은 즉시 반영, 승패는 시점 마지막에 판정한다.
  죽은 보유자의 일반 능력은 요건으로 제외하고, 사망 능력은 사망 사건에서 발동한다.
  독의 성장은 독 수행 내부에 유지하며 별도 연쇄 반응으로 만들지 않는다.
- [ ] 턴 시작·턴 종료 시점의 상태 처리가 만든 효과·사건은 Primary 기원으로 실행한다(D7). 독 틱 사망 → 전염 발동을
  회귀 테스트로 잠근다(`ContagionStatusTests`).
- [ ] 방어 만료를 Prepare로 옮기는 변경은 결과가 지금과 같아야 한다 — 목적은 시점 구분이다(「검토 반영 결정」 끝 문단).
  같은 시드 Compare로 차이가 없음을 확인한다.
- [ ] `Assets/Core/Status/StatusLifetime.cs:11`의 "Chosen PER APPLICATION" 주석은 낡았다(수명은 상태 JSON이 정한다,
  `StatusContentCatalog.LifetimeOf`). 새 정책 타입으로 옮기면서 주석을 사실에 맞게 쓴다.
- [ ] Unity 테스트 `Assets/Tests/UnityEditMode/BattleUnitsViewIdentityTests.cs:79–81`·`:105–107`이
  `StatusLifetime.Turns(2)`와 `Statuses.Add`를 쓴다. API를 바꾸면 이 파일도 같은 커밋에서 고친다.
- [ ] V19: 기존 방어 만료 → 시작 방어 획득 → 새 방어 유지.
  V20: 양측 HP1/독1 → 해당 시점 종료 후 Lose. 만료 중 새 부여가 즉시 사라지지 않는 사례도 추가한다.
- [ ] `Tools/verify.sh`와 D3의 Unity EditMode 배치가 통과한 뒤 커밋: `refactor(core): 상태 만료를 공통 턴 시점으로 처리한다`.

### T7. 전환 회귀·표시 계약·최종 인계

수정 범위 (필요한 최소 호환 수정만):
- `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs`, `KoreanDescriptionGrammar.cs`
- `Assets/Core/Simulation/Playback/TimelineBeatPlanner.cs`
- `Assets/Core/Tests/EditMode/CombatLogTests.cs`, `TimelineBeatPlannerTests.cs`, `DescriptionComposerTests.cs`,
  `StarterPoolDescriptionTests.cs`, `CardContentJsonTests.cs`, `AuthoringSchemaExportTests.cs`
- `Tools/card-idea-notebook/index.test.mjs`
- `docs/superpowers/README.md` 및 영향받는 기존 현행 설계 절의 후속 문서 표시.

입력/출력: T1~6 계약을 바꾸지 않고 표시 로그와 데이터 왕복이 같은 규칙을 관측하는지 검증한다.
UI 연출·개편은 하지 않는다. 이벤트 변경으로 기존 소비자가 깨지면 실제 소비자를 읽고 최소 호환 수정한다.
기존 컨트롤러에 규칙 판단을 넣지 않는다.

- [ ] 문구는 ‘직전에 실행된 카드’로 통일한다. 위치 조건에는 ‘실행선에서’를 유지한다.
  Exact와 UpTo 설명을 구분하며 카드 JSON에 설명 문자열을 하드코딩하지 않는다.
- [ ] 신규 테스트에서 저장소 전체 카드의 새 형식 왕복을 검증한다.
  아래에는 System.IO, Newtonsoft.Json.Linq, FateWeaver.Core.Authoring/Json과 NUnit이 필요하다.

```csharp
[Test]
public void Every_card_preserves_its_canonical_serialized_definition()
{
    foreach (var path in Directory.GetFiles(Path.Combine(TestContent.Root(), "Cards"), "*.json"))
    {
        var first = ContentJson.Read<CardSpec>(File.ReadAllText(path));
        var canonical = ContentJson.Write(first);
        var second = ContentJson.Read<CardSpec>(canonical);
        Assert.IsTrue(JToken.DeepEquals(JToken.Parse(canonical),
            JToken.Parse(ContentJson.Write(second))), path);
        Assert.AreEqual(2, (int)JObject.Parse(canonical)["cardFormat"], path);
    }
}
```

- [ ] 이 왕복 테스트에 더해 T2a의 유효성 검사, 구형 입력 변환 테스트를 유지한다.
  왕복 일치만으로 효과 의미 보존을 대신하지 않는다.
- [ ] CardResolved 요약과 개별 HpChanged를 중복 적용하지 않는지 CombatLogTests로 검증한다.
  승리 시 TurnEnded가 턴 종료 상태를 실제 수행했다는 뜻이 아님을 테스트한다.
- [ ] 같은 시드·같은 조작으로 이벤트 열을 두 번 비교한다. 기존 ScenarioRunner/MultiTurnRunner의
  Compare도 실행해 무조작/조작 차이가 의도한 순서·조건 변경인지 기록한다.
- [ ] 최종 명령을 실행한다. 테스트가 생성한 스키마 파일까지 변경 목록에 포함해 확인한다.
  D3의 Unity EditMode 배치도 마지막으로 한 번 더 돌려 결과 XML의 `failed=0`을 확인한다.

```sh
Tools/verify.sh
rg -n 'TargetId|ConsumedStatusAmount|NoValidTarget|OnHolderDied|DamageBonusPerConsumed|SurviveCharges|DeathsDoor' Assets/Core Assets/Unity Assets/Tests
git diff --check
git status --short
```

검색 결과를 0으로 만들기 위한 기계적 삭제는 금지한다. TargetId가 이벤트의 실제 결과에 필요하면 남는다.
검사의 목적은 일반 카드 고정 대상·카드 누적 소비·사망 능력 직접 호출의 구형 이중 경로를 제거하는 것이다.

- [ ] 아래 검증 행렬에 실제 테스트 이름과 실행 결과를 기록한다. 미실행/실패를 통과로 표시하지 않는다.
- [ ] 문서·색인을 같은 커밋에 포함한다. 전체 계획 완료 시에만 계획 페어를 archive로 이동하고
  현행 색인 행을 삭제한다. master 머지는 별도 사용자 승인을 받는다.
  커밋: `test(core): 새 전투 실행 계약의 회귀를 검증한다`.

### 규칙별 검증과 인계

검증 식별자는 `V01`, `V02`, `V03`, `V04`, `V05`, `V06`, `V07`, `V08`, `V09`, `V10`, `V11`, `V12`, `V13`, `V14`, `V15`, `V16`, `V17`, `V18`, `V19`, `V20`, `V21`, `V22`로 고정한다.

| 설계 검증 | 담당 | 관측 지점 |
|---|---|---|
| V01~V04 | T1 | 실제 자리·실행 이력·사망 제거 |
| V05~V06 | T3 | 다음 효과의 위치 해석 |
| V07~V10 | T4 | 효과/반응 순서·연쇄 금지·방어한 공격 |
| V11~V13 | T5 | 사망 후 계속 수행·카드 종료 승패 |
| V14~V17 | T2a | 정량/최대치 소비·효과별 결과·조건 고정 |
| V18~V20 | T6 | 재부여 순서·공통 만료·시점 종료 승패 |
| V21~V22 | T2a,T2b,T7 | 로딩 거부·게임과 도구의 왕복 |

시작 전: 전용 워크트리, 현재 HEAD 차이, 별도 작업과 공유 파일 차이, 기존 검증 결과 확인.
해당 작업의 중단 기준: 일의적으로 이전할 수 없는 카드 데이터, 승인 설계와 다른 반응 의미를 요구하는 능력,
별도 작업 API와 충돌. 관련 없는 작업은 진행하고 근거를 보고하며 게임 의미를 임의로 정하지 않는다.
전역 관찰자의 순서와 여러 대상 Exact의 일괄 지불은 이번 수용 범위 밖이며 필요해지면 설계부터 확장한다.
이 계획 작성 세션에서는 위 소스/JSON/테스트/도구 변경을 실행하지 않는다.
