# 적 카드 묶음 — 상세

사람 검수용 개요는 [`2026-08-31-enemy-card-bundle-design.html`](2026-08-31-enemy-card-bundle-design.html)에 있다.
구조 승인은 그쪽으로 받는다. 이 문서는 세션 인계용이며 `## 상세`만 담는다.

**상태:** 1단계 구현 완료(2026-08-31). 2단계(조건)는 착수 전.

## 상세

### 1단계에서 실제로 바뀐 것

선택 단위를 낱장에서 묶음으로 옮겼다. `IEnemyTurnPolicy.CardsForTurn(int turnIndex, Random rng)`의
**시그니처는 그대로다** — 반환형 `IReadOnlyList<CardDefinition>`이 이미 "이번 턴 함께 깔릴 카드들",
즉 묶음의 정의였기 때문이다. 바뀐 것은 정책이 **무엇을 들고 무엇을 고르느냐**다.

| 파일 | 변경 |
|---|---|
| `Assets/Core/Simulation/Enemies/EnemyCardBundle.cs` | 신규. 카드 목록 하나를 감싼다 |
| `Assets/Core/Simulation/Enemies/RandomPickPolicy.cs` | 신규. `RandomMovesetPolicy`를 대체 |
| `Assets/Core/Simulation/Enemies/SequencePolicy.cs` | 신규. `Simulation/EnemyIntent.cs`를 대체 |
| `Assets/Core/Simulation/Enemies/ShuffleBagPolicy.cs` | 낱장 → 묶음. 이름 유지 |
| `Assets/Core/Simulation/GoblinDeck.cs` | `Bundles()` 추가, `MaxCardsPerTurn` 삭제, `Policy()`가 `RandomPickPolicy`를 반환 |
| `Assets/Core/Simulation/Enemies/IEnemyTurnPolicy.cs` | 주석만. 선택 단위가 묶음임을 명시 |

`DeckCombatSession`, `FutureZone`, `TurnResolver`, 이벤트 타입은 **한 줄도 바뀌지 않았다.** 묶음은
정책 안에서 태어나 정책 안에서 죽고, 경계를 넘는 것은 고른 묶음의 카드 목록뿐이기 때문이다.

### 정책 셋의 계약

셋 다 "묶음 목록에서 하나를 골라 그 카드들을 돌려준다"이고 고르는 규칙만 다르다.

- **`RandomPickPolicy`** — 복원 추출. `rng.Next(_bundles.Count)` **턴당 정확히 1회**. 같은 묶음이
  연달아 나올 수 있다. `RandomPickPolicyTests.Consumes_exactly_one_rng_draw_per_turn`이 소비량을
  못 박는다(같은 시드의 두 RNG를 정책과 손으로 각각 굴려 이후 값이 일치하는지 본다).
- **`ShuffleBagPolicy`** — 비복원. 가방이 비면 전체를 다시 섞는다. 한 바퀴 안에서는 모든 묶음이
  정확히 한 번씩 나온다. 낱장 시절의 `drawPerTurn` 인자는 **사라졌다** — 한 턴에 묶음 하나이므로
  뽑는 개수가 상수 1이다.
- **`SequencePolicy`** — RNG를 쓰지 않는다. 턴 번호로 색인하고 끝을 넘으면 마지막에 고정된다
  (구 `EnemyIntent`의 clamp 동작 그대로). 편의 생성자
  `SequencePolicy(IReadOnlyList<IReadOnlyList<CardDefinition>>)`가 안쪽 목록 하나를 묶음 하나로
  받으므로, 기존 테스트 12개 파일은 **클래스 이름만** 바뀌었다.

### 고블린 묶음

`GoblinDeck.Bundles()`가 넷을 돌려준다. 괄호 안은 카드의 `BaseExecutionOrder`다.

| 묶음 | 구성 | 존에서의 순서 | 의도 |
|---|---|---|---|
| A | 찌르기(6) | 6 | 늦은 단타 |
| B | 약삭빠른 찌르기(3) + 조잡한 방어(4) | 3 → 4 | 선공 후 방어 |
| C | 약삭빠른 찌르기(3) + 찌르기(6) | 3 → 6 | 앞뒤로 벌린 2연타 |
| D | 조잡한 방어(4) + 조잡한 방어(4) | 4 → 4 | 농성 |

**D는 방어도 6이다.** `BlockBehavior.StacksMagnitude`가 `true`라(`Assets/Core/Status/BlockBehavior.cs:11`)
재부여가 교체가 아니라 합산이다. 의도된 값이다.

D는 두 카드의 실행 순서가 같은데, `FutureZone.Ordered`가
`OrderBy(ExecutionOrder).ThenBy(Side)` — LINQ 안정 정렬 — 이므로 **묶음에 적은 순서가 유지된다**
(`Assets/Core/Combat/FutureZone.cs:36`). 동점 묶음 일반에 적용되는 성질이다.

### 저작되는 조합의 수

바뀐 것은 무작위성의 유무가 아니라 무작위가 작용하는 단위다. 이전
`RandomMovesetPolicy(카드 3장, 1..2)`는 개수와 조합을 함께 굴려 **저작된 적 없는 6가지**를 만들었다.
지금은 저작된 넷만 나온다. `GoblinDeckTests.Every_bundle_is_reachable`이 닿을 수 없는 묶음이 없음을,
`Policy_deploys_exactly_one_authored_bundle_each_turn`이 저작 밖 조합이 없음을 잠근다.

### 결정론에 관한 주의

**과거 시드의 기대값은 무효다.** RNG 소비가 "개수 뽑기 1회 + 카드 뽑기 1~2회"에서 "묶음 하나 고르기
1회"로 바뀌어, 같은 시드라도 이전과 다른 전개가 나온다. 결정론 자체는 유지된다(규칙 7) —
`CombatRngDeterminismTests`가 통과한다. 시드를 박아 기대 타임라인을 기록한 테스트를 새로 쓸 때는
이 변경 이후 값으로 기록해야 한다.

### 2단계 — 조건 (착수 전)

조건이 필요한 적이 나올 때 함께 들어간다. 지금 만들지 않은 이유는 쓰는 곳이 없기 때문이다(규칙 28).

1. `Enemy`에 `MaxHp` 추가. 현재 `Id`·`Hp`·`Statuses`뿐이라 비율 조건을 계산할 수 없다.
   `Enemy(id, hp)`가 `MaxHp = hp`로 채우게 하면 기존 생성 지점(~40곳)이 그대로 컴파일된다.
2. `CardsForTurn(int, Random)` → `CardsForTurn(EnemyTurnContext)`. 맥락에 턴 번호·RNG·전투 상태·
   주인 적을 담는다. 조건이 **선택 시점의 상태를 읽어야** 하기 때문이며, 맥락 객체로 받으면 다음에
   조건이 더 필요해져도 계약을 다시 깨지 않는다. 영향은 9개 파일 15곳.
3. `IBundleCondition` + 레지스트리. 새 조건 = 클래스 1개 + 키 등록(규칙 9).
4. `ScriptedPolicy(규칙: [(조건, 묶음)], 기본: IEnemyTurnPolicy)`. 규칙을 위에서부터 훑어 첫 일치의
   묶음을 내고, 없으면 기본에 위임한다. 스스로도 `IEnemyTurnPolicy`이므로 "무작위 + 조건",
   "순서 + 조건"이 합성으로 나온다 — 조합마다 새 클래스를 만들지 않는다.

**이벤트 구독은 기각됐다.** 코어의 출력인 이벤트 타임라인을 규칙 판단이 되먹으면 발행 순서가 규칙
결과를 바꾸고(규칙 11), 정책 내부 누적이 상태와 별개의 두 번째 진실이 되어 시드 재현이 구독 시점에
의존한다(규칙 7). 상태에 남지 않는 이력("이번 턴에 세 번 맞았다")이 필요하면 구독이 아니라 **그
사실을 상태로 적고** 조건이 그것을 읽는다.

### 알려진 제약

- 묶음은 "함께 등장한다"이지 "붙어서 실행된다"가 아니다. 실행 순서가 다르면 플레이어의 개입 카드가
  그 사이를 가른다. 붙여 실행하고 싶으면 카드의 `BaseExecutionOrder`를 같게 저작해야 한다.
- 묶음 정체성은 존에 도달하지 않는다. 묶음 단위 개입 카드("이 묶음을 통째로 민다")는 지금 구조로
  불가능하며, 넣으려면 `ExecutionCardInstance`·이벤트·표현에 묶음 id를 얹어야 한다.
- 한 턴에 묶음 하나는 매개변수가 아니라 계약이다.
- `CardDefinition.StartsLocked`는 `Assets/Core/Authoring/CardSpec.cs`의 `ExecutionCardSpec`에 없다.
  적 카드를 JSON으로 옮길 때 잠긴 텔레그래프를 저작하려면 이 필드가 함께 가야 한다.
