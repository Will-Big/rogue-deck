# P0-A — 전투 RNG를 CombatState로 단일화 (구현 기록)

- 작성일: 2026-07-18
- 상태: 완료 (헤드리스 검증 통과, Unity Play 검증은 사용자 확인 대기)
- 원 백로그: [`2026-07-16-architecture-refactor-backlog.md`](2026-07-16-architecture-refactor-backlog.md) §3

## 설계 결정

백로그는 "결정적 `IRandomSource` 또는 동등한 작은 추상화"를 요구한다. 이미 `CombatState.Rng`(시드
`System.Random`)가 존재하고 `PartyTargeting`이 사용 중이므로, 새 인터페이스를 만들지 않고 **기존
`CombatState.Rng` 인스턴스를 주입**하는 최소 형태를 채택했다 (구현체가 하나뿐인 인터페이스는 추가
간접층만 늘림 — 단순함 우선).

RNG가 필요한 지점에 전달하는 방식:

- `Deck`: 생성자가 `int seed` 대신 `Random rng`를 받는다. 세션이 `_state.Rng`를 넘긴다.
- `IEnemyTurnPolicy.CardsForTurn(int turnIndex, Random rng)`: 호출 시점에 RNG를 주입한다.
  정책 생성 시점(세션 생성 전)에는 CombatState가 없으므로, 생성자 주입 대신 호출 주입을 택해
  팩토리/지연 바인딩 없이 소유권 문제를 해결했다. 스크립트형 정책(`EnemyIntent`)은 무시한다.
- `RandomMovesetPolicy`: 기존 "(seed, turnIndex) 순수 함수 + `seed * 1000003 + turnIndex` 파생"
  구조를 제거하고 공유 RNG 소비형으로 변경. 같은 턴 재질의 멱등성 보장은 사라졌지만, 제품 경로는
  계약대로 턴당 정확히 1회만 호출한다 (인터페이스 주석에 명시).
- `GoblinDeck.Policy()`, `WardenDeck.Policy()`: 시드 파라미터 제거.

## 변경 파일

- 코어/시뮬레이션: `Deck`, `IEnemyTurnPolicy`, `EnemyIntent`, `RandomMovesetPolicy`,
  `ShuffleBagPolicy`, `SelfLockPolicy`, `GoblinDeck`, `WardenDeck`, `DeckCombatSession`
- Unity: `DeckPlaytestController`(EnemyPolicy 시드 제거), `BattleScreenController`(GoblinDeck.Policy())
- 테스트: 정책/덱 테스트를 RNG 주입 API로 갱신, `CombatRngDeterminismTests` 신규
  (전체 세션 타임라인 결정론 + 시드 분산, Goblin/Warden 조합, 재셔플 포함 8턴)

## 완료 조건 검증

- `new Random` 검색: 제품 규칙 경로에서 `CombatState.Rng` 내부 1곳만 남음 ✅
- 같은 시드 → 전체 이벤트 타임라인(손패 순서 포함) 동일 ✅ (`CombatRngDeterminismTests`)
- 다른 시드 → 의미 있는 분산 유지 ✅
- 전체 헤드리스 테스트: 260/260 통과
  (`dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`) ✅
- Unity Play 검증(전투 화면 정상 구동): 사용자 확인 필요 ⏳

## 동작 변화 (의도된 것)

- RNG 소비 순서가 [덱 셔플 → 턴별 적 정책 → 재셔플] 하나의 스트림으로 합쳐져, 같은 시드라도
  이전 버전과는 다른 셔플/적 패가 나온다 (결정론 자체는 유지).
- `RandomMovesetPolicy`의 "같은 턴 재질의 시 같은 결과" 보장 제거 (호출 계약이 턴당 1회이므로 무영향).
