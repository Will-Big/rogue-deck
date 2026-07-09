# Fate Weaver — 덱 기반 코어 루프 설계 (단일 덱 · 운명력 경제)

작성일: 2026-06-22

## 1. 동기

현재 전투는 **시나리오 스크립트**(고정된 미래 영역 카드 + 스크립트된 개입 플레이)라, 매 턴 같은 카드가
같은 방식으로 발동돼 **원패턴**이다. 또 실행 카드와 개입 카드의 역할이 모호했다. 이 설계는 둘을 해결한다:

- **단일 덱에서 손패를 드로우**하는 덱빌딩 루프로 전환 → 매 턴 손패·적 의도·덱 구성에 따라 전개가 달라짐.
- **역할을 명확히 분리** → 실행 = 효과(WHAT), 개입 = 컨트롤(WHEN/WHERE).

## 2. 확정된 핵심 결정 (브레인스토밍 합의)

1. **단일 덱**(실행 카드 + 개입 카드 혼합). 플레이어의 덱빌딩 대상은 하나의 덱.
2. **모든 카드에 운명력 비용** 표기. 운명력 = 단일 화폐.
3. **역할 분리**
   - **실행 카드 = 효과(WHAT)**: 발동 시 피해/방어/상태를 낸다. **스스로 순서를 바꾸지 못한다.** "불완전" = 효과가 위치·타이밍·조건에 좌우됨.
   - **개입 카드 = 컨트롤(WHEN/WHERE)**: 미래 영역의 순서·타이밍·잠금만 다룬다. **직접 피해/HP에 절대 관여하지 않는다.**
4. **미래 영역 = 턴마다 리셋(모델 A)**: 미래 영역은 "이번 턴의 발동 큐". 드로우→배치→조작→해석→비움. 다음 턴은 새 큐(HP·상태만 이월). 다중턴 타임라인(모델 B)은 채택하지 않음(분산↓·예측 쉬워져 퍼즐화).

## 3. 턴 루프

```
전투 시작: 덱을 섞어 draw pile 구성. 플레이어 HP·운명력 초기화.

매 턴:
  1) 적 의도 배치  — 이번 턴 적의 실행 카드가 미래 영역(FutureZone)에 자동으로 깔림(각자 실행 순서).
  2) 드로우        — draw pile에서 손패 5장(부족하면 discard를 섞어 보충).
  3) 빌드 단계     — 운명력(매 턴 3 충전)으로 손패에서:
        · 실행 카드 플레이 → 비용 지불, 미래 영역에 자기 BaseExecutionOrder로 배치(ExecutionCardInstance)
        · 개입 카드 플레이 → 비용 지불, 대상 카드 선택해 InterventionAction 해석(순서/잠금 조작)
     (실행/개입 순서 자유, 운명력이 남는 한 계속)
  4) 해석          — 미래 영역을 실행 순서로 발동(TurnResolver) → 이벤트 타임라인.
  5) 정리          — 발동·플레이한 카드 → discard. 남은 손패 → discard(use-it-or-lose-it).
                     미래 영역 비움. 승패 판정.
  6) 다음 턴.
```

기본값(가변·성장 가능): **손패 5 · 운명력 3 · 카드 비용 0~3**. 운명력은 매 턴 충전(이월 없음).

## 4. 데이터 모델

### 4.1 카드 정의 확장 (하위호환 init 프로퍼티)

`CardDefinition`에 추가:
- `int EnergyCost { get; init; }` — 운명력 비용(기본 0).
- `CardCategory Category { get; init; }` — `Execution` | `Intervention`(기본 Execution).
- `InterventionActionData InterventionAction { get; init; }` — 개입 카드일 때 플레이 시 해석할 액션 템플릿(기본 null).

실행 카드는 기존대로 `Effects`/`BaseExecutionOrder`를 쓴다. 개입 카드는 `Category=CardCategory.Intervention` + `InterventionAction`을 갖고
`Effects`는 비운다. 기존 시나리오 코드의 `CardDefinition` 생성은 그대로 컴파일된다(추가 필드는 init·기본값).

### 4.2 덱 컴포넌트 (순수 C#)

- **`Deck`** — draw pile / discard pile(둘 다 `List<CardDefinition>`), `Hand`. 메서드: `Shuffle(seed)`, `Draw(n)`(부족 시 discard 셔플 후 보충), `Discard(card)`, `DiscardHand()`.
- **`EncounterState`**(또는 기존 `CombatState` 확장) — `Deck`, `Hand`, `FateEnergy`(매 턴 충전), `FutureZone`, 플레이어 HP·상태, 적들.
- **결정적 RNG** — 셔플/드로우는 **시드 주입**(`System.Random` 래퍼). 헤드리스 테스트가 결정적이도록.

### 4.3 카드 플레이

- **실행 카드 플레이**: 비용 ≤ 운명력 확인 → 운명력 차감 → `new ExecutionCardInstance(def)`를 `FutureZone`에 추가(자기 BaseExecutionOrder) → 손패에서 discard.
- **개입 카드 플레이**: 비용 ≤ 운명력 확인 → 운명력 차감 → 대상 카드(들) 선택 → 기존 `InterventionPlayResolver`로 `InterventionAction` 해석 → 손패에서 discard.

## 5. 시작덱 (10장 · 실행 7 : 개입 3)

| 카드 | 분류 | 코스트 | 효과 | 수 | 프리미티브 |
|---|---|---|---|---|---|
| 베기 | 실행(공격) | 1 | 피해 3 | 2 | Damage 🟢 |
| 막기 | 실행(방어) | 1 | 방어 4 | 2 | ApplyStatus(Block) 🟢 |
| 찰나의 베기 | 실행(공격) | 1 | 피해 2, 첫 발동이면 8 | 1 | Damage + FirstToTrigger 🟢 |
| 강타 | 실행(공격) | 2 | 피해 5, 바로 앞이 아군 공격이면 +5 (총 10) | 1 | Damage + AdjacentCardIs 🟢 |
| 엄호 | 실행(방어) | 1 | 방어 2, 바로 뒤가 적 공격이면 +5 (총 7) | 1 | ApplyStatus(Block) + 조건부 🟡 |
| 앞당김 | 개입 | 1 | 한 카드 실행 순서 −2 | 2 | ChangeExecutionOrder 🟢 |
| 자리 교환 | 개입 | 1 | 두 카드 실행 순서 교환 | 1 | SwapExecutionOrder 🟢 |

🟡 **엄호**만 작은 엔진 확장이 필요: **조건부 ApplyStatus**(기본 방어 2 + 조건 충족 시 방어 5). 현재 조건부 효과
(SuccessEffectValue)는 Damage용이므로, ApplyStatus의 magnitude도 조건부로 받게 일반화한다(`EffectData.ApplyStatus`에
`Condition`/`SuccessEffectValue` 허용 → 충족 시 magnitude=SuccessEffectValue).

## 6. 재사용 / 신규 / 대체

- **재사용(그대로)**: `FutureZone`(안정 오름차순), `TurnResolver`(이벤트 타임라인), `Conditions`/`ConditionEvaluator`, `Effects`(Damage/ApplyStatus/Grant…), `Status`(Block 등), `InterventionPlayResolver`, `CombatRegistries`.
- **신규**: `CardCategory`, `Deck`(pile/hand/draw/shuffle), 결정적 RNG 래퍼, 운명력 비용 경제, 실행 카드 배치, **덱 루프 세션 드라이버**(아래), 조건부 ApplyStatus, 시작덱 정의.
- **대체(보존)**: 시나리오 스크립트 방식(`MultiTurnScenario`+스크립트 개입 플레이)은 새 덱 루프로 **대체**되지만, 기존 `ScenarioRunner`/`MultiTurnRunner`와 그 테스트는 **밸런스 회귀 도구로 보존**(삭제하지 않음). Unity 컨트롤러는 Phase 2에서 리워크.

## 7. 세션 드라이버 (Unity·테스트 공용)

`DeckCombatSession`(Simulation, 순수 C#, 헤드리스 검증):
- 상태: `Hand`, `FateEnergy`, `FutureZone`의 `CurrentOrder`, 플레이어/적 HP·상태, `LastTimeline`, `Outcome`, `TurnIndex`.
- 메서드: `PlayExecutionCard(handIndex)`, `PlayInterventionCard(handIndex, targetCardId, secondary?)`, `ResolveTurn()`, `BeginNextTurn()`(드로우·운명력 충전·적 의도 배치), `IsComplete`.
- 적 의도: Phase 1은 결정적 **적 의도 스크립트**(턴별 적 실행 카드 목록)로 주입 — 루프·밸런스 테스트 가능. 본격 적 AI는 이후.
- `CombatRegistries`로 효과/상태/개입액션 등록 재사용.

## 8. 검증

- 코어 루프·덱·세션은 **순수 C# → 헤드리스 dotnet test**로 검증(결정적 시드).
- 결정적 **이벤트 타임라인**은 유일 출력으로 유지(테스트가 타임라인에 단언; UI는 재생).
- 시작덱 **불변식 테스트**: 예) 찰나의 베기를 앞당김으로 1번에 → 첫 발동 8; 강타를 아군 공격 뒤에 → +5; 엄호를 적 공격 앞에 → +5; 운명력 게이팅(비용>잔여면 거부); 덱 소진 시 discard 셔플 보충.
- Unity 덱/손패 UI는 Phase 2에서 사용자 Play 검증.

## 9. 단계(phasing)

- **Phase 1 (이번 구현)**: 코어 덱 루프(순수 C#) + 카드 정의 확장 + 조건부 ApplyStatus + 시작덱 데이터 + `DeckCombatSession` + 결정적 적 의도 스크립트 + 헤드리스 테스트. **Unity 미변경.**
- **Phase 2**: Unity 덱/손패 UI — 손패·운명력·덱/버림 더미·미래 영역 표시, 카드 플레이/배치. 기존 `CardView`/`CardPresentation`/아트·설명/에디터 빌더 재사용, 컨트롤러는 `DeckCombatSession` 위로 리워크.
- **Phase 3**: 보상 카드 풀 확장(운명·행동) + 새 조건(`LastToTrigger`/`TargetHasStatus`) + 적 의도/조우 콘텐츠.

## 10. 미해결 / 기본값 (구현 중 조정 가능)

- 손패 5 / 운명력 3 / 카드 비용 0~3 / 운명력 이월 없음 / 남은 손패 버림.
- 적 의도 소스: Phase 1은 결정적 스크립트. 다중 적 "그 적" 정밀 타깃은 기존 한계(Enemies[0] 근사) 유지.
- 덱 셔플: draw 소진 시 discard를 셔플해 draw로(시드 진행).

## 11. 파킹 메모

직전 uGUI 작업(CardView/컨트롤러/에디터 빌더/TMP 폰트)은 **미커밋 상태로 작업 트리에 보존**. 기반 조각
(CardView/CardPresentation/아트·설명/EditMode 테스트)은 Phase 2에서 재사용하고, 컨트롤러·씬은 덱 UI로 리워크한다.
