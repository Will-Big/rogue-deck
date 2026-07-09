# Fate Weaver — 덱/손패 플레이테스트 UI 설계 (Sub-project 2)

작성일: 2026-06-23

## 1. 목표

`DeckCombatSession`(순수 C#, 헤드리스 검증 완료) 위에 **마우스로 플레이 가능한 uGUI 덱 화면**을 올린다.
손패에서 카드를 내고(행동=배치, 운명=순서 조작), 줄을 해석하고, 다음 턴으로 진행하는 코어 루프를 직접 돌려본다.
재사용 UI 요소(특히 카드)는 **프리팹**으로 만든다. 기존 시나리오 기반 컨트롤러/씬은 이걸로 교체한다.

## 2. 레이아웃

```
┌ 상태: 플레이어 HP · 적 HP/상태 · 운명력 ●●● · 턴 N ───────────┐
│ 미래 영역(실행 순서 순): [적공격][강타][막기] …   (줄 카드 = 개입 타깃) │
│ 메시지 / 해석 결과                                              │
│ 손패: [베기①][막기①][앞당김①][강타②][엄호①]   (손패 카드 = 플레이)  │
│ 덱 N · 버림 N                            [턴 실행] [다음 턴] [초기화] │
└──────────────────────────────────────────────────────────┘
```

## 3. 인터랙션 (2단계 클릭)

- **손패 실행 카드 클릭** → 비용 ≤ 운명력이면 즉시 미래 영역에 배치(자기 BaseExecutionOrder), 운명력 차감, 손→버림. (원클릭)
- **손패 개입 카드 클릭** → "타깃 선택" 상태(armed, 강조). 이어서:
  - 단일 타깃(실행 순서 변경/잠금): **줄 카드 1장 클릭** → 적용.
  - 교환: **줄 카드 2장 클릭** → 적용.
  - (대상 규칙 위반/운명력 부족 → 메시지 거부, armed 해제)
- **턴 실행** → `ResolveTurn()` → 타임라인 표시. **다음 턴** → `BeginNextTurn()`(드로우·운명력 충전·적 의도).
  **초기화** → 세션 새로 시작. 승패 시 다음 턴 비활성.

## 4. 컴포넌트

- **`CardView`(프리팹, 재사용)** — 손패 카드 = 줄 카드 동일 프리팹. **추가**: 비용 표시 텍스트(`_costText`).
  손패/줄 모드는 바인딩 데이터로 구분(줄=현재 실행 순서 표시, 손패=비용 강조). 선택 아웃라인으로 armed/타깃 후보 표시.
- **`CardPresentation`(뷰모델)** — **추가**: `int EnergyCost`. **추가 팩토리**: `FromDefinition(CardDefinition)`(손패용,
  BaseExecutionOrder·EnergyCost 사용). 기존 `From(ExecutionCardInstance)`는 줄용(현재 ExecutionOrder).
- **`DeckPlaytestController`(재작성)** — `[SerializeField] DeckAsset _deck`(에디터 빌더가 `StarterDeck.asset` 와이어링).
  Start에서 `_deck.ToSpecs()` → `CardSpecMapper.ToDefinition` → `new DeckCombatSession(deck, hp, enemies, intent…)`.
  손패/줄 컨테이너에 CardView 인스턴스화·바인딩, 클릭 핸들링(armed 상태 머신), 버튼.
  적 의도: 프로토타입은 코드 내장 샘플(고블린 매 턴 공격).
- **에디터 빌더(`FateWeaverPlaytestSceneCreator` 교체/확장)** — Canvas+EventSystem(InputSystem 모듈),
  상태/메시지/타임라인 TMP, 손패·줄 HorizontalLayoutGroup 컨테이너, 버튼, 덱/버림 카운터, 컨트롤러 부착·와이어링,
  `CardView.prefab`(기존 재사용, 비용 필드 추가). 메뉴 `Fate Weaver/Build Deck Playtest Scene`.

## 5. 프리팹화

반복 재사용 원자 = **`CardView`** → 손패·줄에서 N개 인스턴스화하는 단일 프리팹으로 고정(이미 프리팹, 비용 필드만 추가).
버튼/카운터는 단순 텍스트라 별도 프리팹화 불필요(YAGNI). 향후 재사용 위젯이 늘면 그때 프리팹 추가.

## 6. 데이터 흐름

```
DeckAsset(StarterDeck.asset) ─ToSpecs()→ CardSpec[] ─CardSpecMapper→ CardDefinition[]
   → new DeckCombatSession(...)
       Hand: IReadOnlyList<CardDefinition>      → CardPresentation.FromDefinition → 손패 CardView
       CurrentOrder: ExecutionCardInstance[]        → CardPresentation.From          → 줄 CardView
   클릭 → PlayExecutionCard / PlayInterventionCard / ResolveTurn / BeginNextTurn → 재렌더
```

아트/이름/설명은 기존 표현 계층 재사용: `PlaytestCardArt.Sprite(id)`, `PlaytestKoreanText.CardName/CardDescription(id)`.
아트 없는 카드(guard/heavy_strike/cover/pull_forward/swap_positions 등)는 측색 폴백(프롬프트로 아트는 나중에 생성).

## 7. 검증

- 진행 로직은 `DeckCombatSession`(헤드리스 검증 완료) → UI는 표시·입력만. **사용자가 Play로 검증.**
- 순수 추가 로직(예: 개입 카드의 타깃 수 판정)이 생기면 작은 헤드리스/EditMode 테스트로 가드 가능.

## 8. 범위 밖 (후속)

- 드래그앤드롭, 이벤트 타임라인 애니메이션, 다중 적, 적 의도 에셋(IntentAsset), 보상/맵/덱빌딩 화면.
- 확장 seam(특수 효과)·Phase 3 보상 카드는 별도.

## 9. 교체 메모

기존 시나리오 기반 `FateWeaverPlaytestController`(MultiTurnPlaytestSession)와 그 씬 빌더는 이 덱 UI로 **교체**한다.
`MultiTurnPlaytestSession`/시나리오 러너는 헤드리스 회귀 도구로 남긴다(삭제하지 않음).
