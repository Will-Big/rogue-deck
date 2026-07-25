# 전투 시스템 정합성 정리 (설계 + 구현 계획)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended)
> or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax
> for tracking. 설계 근거는 §1~§7, 실행 단계는 §8부터다.

- 작성일: 2026-07-25
- 문서 유형: `plan`
- 주 도메인: `combat-core`, `unity-presentation`
- 상태: `archived` — 2026-07-25 완료
- 선행 점검: 2026-07-25 전투 시스템 리팩토링 점검 (코어·Unity·저작 파이프라인 3개 영역)

## 1. 목적

전투 시스템 점검에서 발견한 **정합성 결함 4건**을 P0-C 대상 선택 메타데이터 착수 전에 정리한다.
네 항목은 서로 독립적이며, 각각 죽은 코드·무동작 코드·분기된 콘텐츠 값·잠복 함정을 제거한다.

이 작업은 새 기능을 만들지 않는다. 백로그의 P0-C 이후 항목을 앞당기지도 않는다.

## 2. 범위

| # | 항목 | 성격 |
|---|---|---|
| 1 | 참조 0건인 `FateWeaverPlaytestController` 삭제 | 죽은 코드 제거 |
| 2 | `PlaytestCardArt` 카드 아트 해석부 삭제 | 무동작 코드 제거 |
| 3 | `pull_forward` 효과값을 SO 정본으로 통일 | 콘텐츠 분기 해소 |
| 4 | `CombatState` 레거시 단일 플레이어 shim 제거 | 잠복 함정 제거 |

범위 밖: P0-C 대상 선택 메타데이터, P1-A SO 단일 원본화, `DeckCombatSession` 모드 분리,
조건 축 레지스트리화, 코어 이벤트 확충. 이들은 §7에서 백로그에 기록만 한다.

## 3. 항목별 설계

### 3.1 죽은 컨트롤러 삭제

`Assets/Unity/FateWeaverPlaytestController.cs`와 `.meta`를 삭제한다.

근거: GUID `871f9debf9f84c44ae7d5fc5c72b1e94`가 `.unity`·`.prefab`·`.asset` 어디에도 없고,
어떤 C# 파일도 이 타입을 참조하지 않는다. 파일 주석이 지목하는 `FateWeaverPlaytestSceneCreator`는
저장소에 존재하지 않는다. 씬 소유 관계는 `BattleScreenController` → `FateWeaverBattle.unity`,
`DeckPlaytestController` → `FateWeaverPlaytest.unity`·`FateWeaverWardenPlaytest.unity`이며
이 컨트롤러에 대응하는 씬이 없다.

부수 효과: 개입 액션을 매직 넘버로 저작하던 4줄(저장소 규칙 5 위반), 다른 컨트롤러와의
중복 약 63줄, 레거시 shim 참조 2곳이 함께 사라진다.

### 3.2 카드 아트 해석부 삭제

`Assets/Unity/PlaytestCardArt.cs`에서 다음을 제거한다.

- `ResolveArtName(string cardId)` — id→파일명 switch 16분기
- `ResolveResourcePath(string cardId)`
- `Sprite(string cardId)`와 그 캐시

**유지**: `LockIconResourcePath`, `LockIconSprite()`, `ResolveStatusIconResourcePath(CardStatusIcon)`,
`StatusIconSprite(CardStatusIcon)`. 이 경로는 `Assets/Unity/Resources/Status/icon_lock.png`가 실제로
존재하고 `RailCardView`·`CardView`가 런타임에 호출한다.

근거: `ResolveResourcePath`가 `"Cards/" + name`을 반환하지만 스프라이트는 전부 하위 폴더
(`Cards/Player/`, `Cards/goblins/`)에 있다. switch가 아는 16개 id 전부가 `null`을 로드하고 그 `null`이
캐시되어 재시도되지 않는다. 즉 카드 아트 fallback은 **현재 전체가 무동작**이며, 플레이어 카드 SO 10개는
`Art: {fileID: 0}`으로 비어 있어 화면에는 색면 fallback만 나온다. 이 코드를 지워도 **화면은 바뀌지 않는다.**

방향 정합성: 유닛·캐릭터·적 시각 표현을 색상 틴트로 통일한다는 기존 아트 방향과 일치하며,
P1-A가 목표로 하는 id→경로 switch 제거를 선행한다.

호출부 정리: `BattleScreenController.BuildArtLookup`/`ArtFor`와 `DeckPlaytestController`의 동일 코드
(양쪽 24줄 중복)를 `CardAsset.Art`만 참조하도록 축소한다.

테스트: `Assets/Tests/UnityEditMode/PlaytestCardArtTests.cs`의 카드 아트 단언을 삭제한다.
현재 이 테스트는 문자열 연결만 검사해 **깨진 경로를 green으로 고정**하고 있으며, 스프라이트가 실제로
로드되는지는 확인하지 않는다.

`Assets/Unity/Resources/Cards/` 아래 PNG 10장은 **삭제하지 않고 남긴다.** 지금 쓰이지 않을 뿐이고,
나중에 방향이 바뀌면 `CardAsset.Art`에 할당하면 된다.

### 3.3 `pull_forward` SO 정본 통일

`Assets/Core/Simulation/StarterDeck.cs`를 SO 저작 값에 맞춘다.

| 항목 | 현재 (코드) | 현재 (SO·specs·생성코드) | 통일 후 |
|---|---|---|---|
| `pull_forward` 장수 | 2 | 1 | 1 |
| `pull_forward` 효과값 | `-2` | `-1` | `-1` |
| `push_back` | 없음 | `+1` 1장 | `+1` 1장 |

정본을 SO로 정한 근거: Unity 씬이 실제로 소비하는 값이고(`CharacterAsset.Deck` → `ToSpecs()` →
`CardSpecMapper.ToDefinition`), 앞당김/밀어내기 대칭 쌍이라는 설계 의도가 분명하며, P1-A의
"SO가 단일 원본" 방향과 일치한다.

영향 범위: 하드코딩 카탈로그 `StarterDeck.Build()`는 프로덕션에서 `PartyPrototypeRoster`만 사용하고,
`PartyPrototypeRoster.Build()` 자체는 테스트에서만 쓰인다. Unity 씬은 `PartyPrototypeRoster.Tuning`만
읽는다. 따라서 이 변경으로 **화면 동작은 바뀌지 않으며**, 헤드리스 픽스처가 Unity와 같은 값을 쓰게 된다.

회귀 방지: `CardContentEquivalenceTests`의 golden 서명을 갱신하고, 분기를 의도적으로 허용하던
헤더 주석("pull_forward … deferred to P1-A cleanup")을 삭제한다. 나아가 하드코딩 카탈로그와
specs/생성코드 사이의 **교차 동등성 단언을 새로 추가**한다. 현재 교차 단언은 파티 프로토타입 쌍에만
있어 시작덱 분기를 잡지 못했다. 이 단언이 앞으로의 재분기를 컴파일이 아니라 테스트로 막는다.

### 3.4 레거시 shim 제거

`Assets/Core/Combat/CombatState.cs`에서 다음을 제거한다.

- `PlayerHp` 속성
- `PlayerStatuses` 속성
- `_legacyPlayer` 필드, `LegacyPlayerName`, `LegacyPlayerDefaultMaxHp`
- 생성자의 `Party.Add(_legacyPlayer)`

`LegacyPlayerId` 상수는 **삭제하지 않고 `SoloPlayerId`로 이름만 바꿔 남긴다.** 이 값은 shim 전용이
아니라 솔로 모드 카드의 `OwnerId`로 쓰이기 때문이다(`Deck.WithLegacyOwner`,
`DeckCombatSession`의 동일 헬퍼).

#### 왜 이 제거가 안전하고 단순화인가

솔로 모드는 **이미 1인 파티**다. `Party[0].Id`도 `"player"`이고 카드 `OwnerId`도 `"player"`라서,
shim은 `Party[0]`에 대한 순수한 별칭이었다. 그 결과 제거는 분기를 늘리지 않고 줄인다.

- `DeckCombatSession.OwnerStatusesFor`의 `if (!_isPartyMode) return _state.PlayerStatuses;`
  분기가 **통째로 사라진다.** 솔로에서도 `card.OwnerId == Party[0].Id`이므로 파티 조회 경로가 그대로 맞다.
- `ApplyStatusHandler.ResolvePlayerSelf`의
  `Party.Count == 1 && Party[0].Id == CombatState.LegacyPlayerId` 조건이 `Party.Count == 1`로
  단순화되어, 바로 아래 `ResolveEnemySelf`의 `Enemies.Count == 1`과 대칭이 된다.

#### 제거하려는 함정

`CombatState()` 생성자는 항상 `_legacyPlayer`를 만들어 `Party`에 넣는데, 파티 모드는
`DeckCombatSession`에서 `Party.Clear()`를 호출한다. `_legacyPlayer`는 private 필드라 리스트에서만
빠지고 객체는 살아남는다. 그래서 파티 모드에서 `PlayerHp`는 리스트 밖 고아 객체를 읽어 항상 `0`을
반환하고 쓰기는 반영되지 않으며, `PlayerStatuses`에 붙은 상태는 `TurnResolver.EndOfTurnMaintenance`가
`Party`를 순회하므로 **영원히 만료되지 않는다.**

현재 파티 모드에서 이 두 속성을 읽는 코드가 없어 화면에 틀린 값이 나오지는 않는다. 제거 대상은
증상이 아니라 **다음 사용자가 조용히 0을 받게 되는 구조**다.

#### 호출자 마이그레이션

솔로 경로 5곳이 파티 멤버를 명시적으로 만든다.

- `ScenarioRunner`, `PlaytestSession`, `MultiTurnRunner`, `MultiTurnPlaytestSession`:
  `new CombatState { PlayerHp = scenario.PlayerHp }` → 멤버를 명시 추가
- `DeckCombatSession` 솔로 생성자: `_state.PlayerHp = playerHp` → 멤버를 명시 추가

`ScenarioDefinition.PlayerHp`, `MultiTurnScenario.PlayerHp`, `ScenarioResult.PlayerHp`와 비교·리포트
타입의 동명 필드는 **shim이 아니라 시나리오 저작·결과 필드이므로 남긴다.** 배선만 바뀐다.

테스트 17개 파일 약 40곳이 `state.PlayerHp` → `state.Party[0].Hp`로 바뀐다.

#### 의도된 동작 변화

태스크 4의 코드 리뷰에서, 아래 다섯 가지가 의도된 동작 변화로 확인되었다. 모두 현재 프로덕션
콘텐츠에서는 무해하다.

1. 솔로 플레이어의 `MaxHp`가 `0`에서 `playerHp`로 바뀐다. 규칙 로직 중 `MaxHp`를 읽는 곳이 없어
   무해하다.
2. `OwnerStatusesFor`가 소유자 id로 매칭하므로, 솔로 모드에서 `OwnerId`가 파티 멤버와 일치하지
   않는 카드는 더 이상 솔로 플레이어의 상태를 상속받지 않는다. 프로덕션 솔로 경로는 항상
   `SoloPlayerId`를 찍으므로 무해하다.
3. 같은 이유로 솔로 모드에서도 `member.IsAlive` 조건이 적용된다. 죽은 솔로 플레이어는 카드를 낼 수
   없으므로 도달 불가하다.
4. `ResolvePlayerSelf`가 `Party.Count == 1`로 단순화되어, **1인 파티의 소유자 없는 Self 카드가 취소
   대신 해결된다.** 현재 프로덕션 파티는 2인이라 도달 불가하지만, `PartyTuning.MinPartySize`가 1이고
   파티 공유 카드(`OwnerId = null`)를 만드는 경로가 존재하므로 **1인 파티나 파티 공유 카드를
   도입하면 살아난다.** 그때 재검토할 것.
5. `new CombatState()`가 빈 파티를 만들므로, 시나리오 리포트 등에서 `Party[0]`를 읽던 자리는 파티가
   비어 있으면 `0`을 조용히 반환하는 대신 예외를 던진다. 모든 프로덕션 경로가 즉시 멤버를 추가하므로
   무해하며, 조용한 오답보다 낫다.

## 4. 작업 순서

의존성이 있는 순서다. 각 커밋은 독립적으로 되돌릴 수 있고, 각 커밋 시점에 전체 테스트가 통과한다.

1. **죽은 컨트롤러 삭제** — 레거시 shim 참조 2곳을 미리 없애 4번의 범위를 줄인다
2. **카드 아트 해석부 삭제** — 1·3·4와 무관하게 독립
3. **`pull_forward` SO 정본 통일** — 콘텐츠 값과 golden 서명만 건드린다
4. **레거시 shim 제거** — diff가 가장 넓고 테스트 파일을 광범위하게 수정하므로 마지막

한 커밋으로 묶지 않는다. 40파일이 넘는 diff는 리뷰가 불가능하다.

## 5. 테스트 전략

각 커밋마다 전체 헤드리스 테스트 통과를 확인한다.

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

- 커밋 3·4는 규칙 경로를 건드리므로 **결정론 타임라인 테스트가 핵심 안전망**이다
  (같은 시나리오+시드 → 동일 이벤트 타임라인).
- 커밋 4는 shim 제거 **전에** "솔로 전투의 플레이어가 명시적 파티 멤버 하나이고 그 `MaxHp`가 주어진
  HP와 같다"를 단언하는 RED 테스트를 먼저 쓴다. 이 테스트는 오늘 컴파일되지만 `MaxHp`가 `0`이라 실패하며,
  §3.4가 서술한 모순을 그대로 겨냥한다. shim 제거 후 GREEN이 된다.
- 커밋 3은 golden 서명 갱신이 곧 검증이며, 새로 추가하는 교차 동등성 단언이 재분기를 막는다.

Unity 검증은 저장소 규칙 17을 따른다. 컴파일과 자동화 검증을 위한 `-batchmode` EditMode 테스트는
이 워크트리에서 실행하되, 씬·프리팹·ScriptableObject 저작과 Play 검증은 하지 않는다. 로그는
`/private/tmp`에 남긴다.

**실행 결과 (2026-07-25, 태스크 5 종료 시점):** 헤드리스 스위트 328/328 통과, Unity EditMode
배치 테스트 386/386 통과, 컴파일 에러 0건 (로그 `/private/tmp/fw-editmode.log`).

## 6. 완료 조건

- `FateWeaverPlaytestController` 관련 파일과 참조가 저장소에 없다
- `PlaytestCardArt`에 카드 id→경로 해석이 없고 상태 아이콘 경로만 남는다
- `PlaytestCardArtTests`에 깨진 경로를 고정하는 단언이 없다
- 하드코딩 시작덱과 specs·생성코드·SO의 `pull_forward`/`push_back`이 일치하고, 교차 동등성 단언이 이를 고정한다
- `CombatState`에 `PlayerHp`/`PlayerStatuses`/`_legacyPlayer`가 없다
- `DeckCombatSession.OwnerStatusesFor`에 `_isPartyMode` 분기가 없다
- 전체 헤드리스 테스트 통과, Unity EditMode 배치 테스트 컴파일·통과
- 백로그와 중앙 색인이 같은 커밋에서 갱신되었다 (규칙 20)

## 7. 백로그에 기록할 미착수 항목

이번 점검에서 확인했으나 이 작업 범위 밖인 항목이다. 다음 세션이 다시 발견하지 않도록
`plans/2026-07-16-architecture-refactor-backlog.md`에 추가한다.

| 항목 | 요지 |
|---|---|
| 조건 축의 침묵 실패 | 조건 추가 시 `ConditionEvaluator`·`KoreanDescriptionGrammar`·`ConditionSpec.ToCondition` 세 곳을 고쳐야 하고, 뒤 두 곳의 `default`가 각각 빈 문자열과 `null`을 조용히 반환한다. 저작 검증은 조건을 보지 않는다 |
| P2의 선행 조건 | `ResolutionEvent`에 HP 변화·상태 부여·상태 만료·대형 이동 이벤트가 없어 타임라인 재생 UI가 물리적으로 불가능하다. P2는 컨트롤러 리팩터가 아니라 코어 이벤트 확충이 먼저다 |
| `reward_nullified` 특수 처리 | 여섯 상태 중 이것만 `TurnResolver`가 직접 조회하고, 대응 behavior는 훅 없는 빈 클래스다. `ModifyConditionTier` 훅이 필요하다 |
| `VulnerableBehavior` 하드코딩 | `(damage * 3) / 2`로 50%를 고정하고 자신의 `Magnitude`를 무시한다. 형제 상태 넷은 모두 `Magnitude`를 읽는다 (규칙 8) |
| 비용 이중 원본 | `CardDefinition.EnergyCost`와 `InterventionActionData.InterventionCost`가 별개이고 개입 경로는 전자를 읽지 않는다. 현재는 매퍼가 같은 값을 채워 우연히 일치한다 |
| `DeckCombatSession` 모드 분리 | 505줄 한 클래스가 `_isPartyMode` 불리언으로 두 모드를 겸하고, 파티 생성자는 죽은 플레이스홀더 인자를 넘긴다. 파티 조립·검증 92줄도 분리 대상이다 |
| 적 대상 선택 중복 | `DamageHandler.SelectEnemy`와 `ApplyStatusHandler.SelectTargetEnemy`가 동일하다. 파티 쪽 `PartyTargeting`에 대응하는 적 쪽 모듈이 없다 |
| 단일 적 가정 | 턴 루프가 텔레그래프 카드를 항상 `Enemies[0]`에 귀속시켜 다중 적 조우를 표현할 수 없다 |
| 독 상태 미구현 | 덱 루프 설계가 명시한 "행동 턴 종료 시 발동 후 1 증가" 훅 지점이 없고 `poison` 상태 키도 없다 |

---

## 8. 구현 계획

**Goal:** 전투 시스템의 정합성 결함 4건(죽은 코드, 무동작 코드, 분기된 콘텐츠 값, 잠복 함정)을
독립적으로 되돌릴 수 있는 4개 커밋으로 제거한다.

**Architecture:** 각 태스크는 하나의 커밋이자 하나의 리뷰 단위다. 순서에 의존성이 있다 — 태스크 1이
태스크 4의 범위를 줄이고, 태스크 4가 가장 넓은 diff이므로 마지막이다. 태스크 3·4는 규칙 경로를
건드리므로 golden 서명과 결정론 타임라인 테스트가 안전망이다.

**Tech Stack:** C# (netstandard2.1, LangVersion 9), NUnit, .NET 5 SDK 헤드리스 하니스, Unity 6000.5.2f1
EditMode(컴파일 검증 한정).

### Global Constraints

- 작업 위치는 이 워크트리(`.claude/worktrees/combat-core-design-progress-685fe1`), 브랜치는
  `claude/combat-core-design-progress-685fe1`. 메인 체크아웃을 건드리지 않는다 (저장소 규칙 15).
- 검증 명령은 항상 `-p:TargetFramework=net5.0`을 포함한다. 로컬 SDK가 .NET 5뿐이라 생략하면 실패한다.
- `FateWeaver.Core`에 UnityEngine 참조를 추가하지 않는다 (규칙 6).
- 무작위는 `CombatState.Rng`만 사용한다. `new Random()`, `DateTime`, `Guid.NewGuid()` 금지 (규칙 7).
- 새 외부 패키지를 추가하지 않는다 (규칙 14).
- 씬·프리팹·ScriptableObject 저작과 Play 검증은 하지 않는다 (규칙 17).
- 각 태스크 종료 시 워킹 트리를 깨끗하게 남긴다 (규칙 18).
- `Assets/Unity/Resources/Cards/` 아래 PNG는 삭제하지 않는다.

### 검증 명령

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

기준선: 현재 전체 통과. 각 태스크의 마지막 단계에서 이 명령이 통과해야 한다.

---

### Task 1: 죽은 컨트롤러 삭제

**Files:**
- Delete: `Assets/Unity/FateWeaverPlaytestController.cs`
- Delete: `Assets/Unity/FateWeaverPlaytestController.cs.meta`

**Interfaces:**
- Consumes: 없음
- Produces: 없음 (순수 삭제). 태스크 4가 마이그레이션할 shim 참조 2곳이 여기서 사라진다.

- [ ] **Step 1: 참조가 정말 0건인지 확인**

```bash
grep -rn "FateWeaverPlaytestController" Assets Tools --include="*.cs" | grep -v "Assets/Unity/FateWeaverPlaytestController.cs:"
grep -rn "871f9debf9f84c44ae7d5fc5c72b1e94" Assets --include="*.unity" --include="*.prefab" --include="*.asset"
```

Expected: 두 명령 모두 출력 없음. 출력이 있으면 **삭제하지 말고 중단**한 뒤 사용자에게 보고한다.

- [ ] **Step 2: 삭제**

```bash
git rm Assets/Unity/FateWeaverPlaytestController.cs Assets/Unity/FateWeaverPlaytestController.cs.meta
```

- [ ] **Step 3: 테스트 통과 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: PASS. (이 컨트롤러는 Unity 전용이라 헤드리스 결과는 변하지 않는다. 회귀가 없음을 확인하는 단계다.)

- [ ] **Step 4: 커밋**

```bash
git commit -m "refactor(unity): remove unreferenced FateWeaverPlaytestController

씬·프리팹·코드 어디에서도 참조되지 않는 328줄 컨트롤러를 삭제한다. 파일 주석이
가리키는 FateWeaverPlaytestSceneCreator는 저장소에 존재하지 않는다.

개입 액션을 매직 넘버로 저작하던 4줄(규칙 5 위반), 다른 컨트롤러와의 중복 63줄,
레거시 단일 플레이어 shim 참조 2곳이 함께 사라진다.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: 카드 아트 해석부 삭제

**Files:**
- Modify: `Assets/Unity/PlaytestCardArt.cs` — `ResolveArtName`/`ResolveResourcePath`/`Sprite`와 `Cache` 제거
- Modify: `Assets/Unity/BattleScreenController.cs` — `ArtFor`
- Modify: `Assets/Unity/DeckPlaytestController.cs` — `ArtFor`
- Modify: `Assets/Tests/UnityEditMode/PlaytestCardArtTests.cs` — 카드 아트 단언 2개 제거

**Interfaces:**
- Consumes: 없음
- Produces: `PlaytestCardArt`의 잔존 공개 API는 `LockIconResourcePath`(const string),
  `LockIconSprite()`, `ResolveStatusIconResourcePath(CardStatusIcon)`, `StatusIconSprite(CardStatusIcon)`
  네 개다. `Sprite(string)`는 더 이상 존재하지 않는다.

- [ ] **Step 1: 삭제 대상이 실제로 무동작인지 확인**

```bash
ls Assets/Unity/Resources/Cards/
```

Expected: `Frame/`, `Player/`, `goblins/` 세 디렉터리만 나온다. 즉 `Cards/` 바로 아래에는 PNG가 없고,
`ResolveResourcePath`가 만드는 `"Cards/slash"` 같은 경로는 어떤 파일과도 대응하지 않는다.

- [ ] **Step 2: `PlaytestCardArt.cs`에서 카드 아트 해석부 제거**

파일 전체를 아래 내용으로 교체한다.

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Resolves card status icons from Resources. Card face art comes from CardAsset.Art
    /// (inspector-assigned, GUID-based); there is no id→path fallback.</summary>
    public static class PlaytestCardArt
    {
        private static readonly Dictionary<CardStatusIcon, Sprite> StatusIconCache = new Dictionary<CardStatusIcon, Sprite>();

        public const string LockIconResourcePath = "Status/icon_lock";

        public static Sprite LockIconSprite()
            => StatusIconSprite(CardStatusIcon.Lock);

        public static string ResolveStatusIconResourcePath(CardStatusIcon icon)
        {
            switch (icon)
            {
                case CardStatusIcon.Lock:
                    return LockIconResourcePath;
                default:
                    return null;
            }
        }

        public static Sprite StatusIconSprite(CardStatusIcon icon)
        {
            if (StatusIconCache.TryGetValue(icon, out var cached))
            {
                return cached;
            }

            var path = ResolveStatusIconResourcePath(icon);
            if (path == null)
            {
                StatusIconCache[icon] = null;
                return null;
            }

            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites != null && sprites.Length > 0)
            {
                StatusIconCache[icon] = sprites[0];
                return sprites[0];
            }

            var sprite = Resources.Load<Sprite>(path);
            StatusIconCache[icon] = sprite;
            return sprite;
        }
    }
}
```

`using System;`이 사라진 점에 유의한다 — `StringComparison`을 쓰던 유일한 자리가 제거되었다.

- [ ] **Step 3: `BattleScreenController.ArtFor` 수정**

현재 (`Assets/Unity/BattleScreenController.cs`):

```csharp
        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : PlaytestCardArt.Sprite(id);
```

변경 후:

```csharp
        // Card face art comes only from authored CardAsset.Art (GUID reference, move-safe).
        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : null;
```

- [ ] **Step 4: `DeckPlaytestController.ArtFor` 수정**

현재 (`Assets/Unity/DeckPlaytestController.cs`):

```csharp
        // Authored CardAsset.Art (GUID, move-safe) first; Resources path only as a last-resort fallback.
        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : PlaytestCardArt.Sprite(id);
```

변경 후:

```csharp
        // Card face art comes only from authored CardAsset.Art (GUID reference, move-safe).
        private Sprite ArtFor(string id)
            => _artById.TryGetValue(id, out var sprite) ? sprite : null;
```

- [ ] **Step 5: 깨진 경로를 고정하던 테스트 제거**

`Assets/Tests/UnityEditMode/PlaytestCardArtTests.cs`에서 `Renamed_ids_map_to_art_files`와
`Resource_path_includes_cards_subfolder` 두 테스트를 **삭제**한다. 이 둘은 문자열 연결만 검사해
로드 실패를 green으로 고정하고 있었다. `Lock_icon_uses_status_resource_path`를 포함한 상태 아이콘
테스트는 **남긴다**.

- [ ] **Step 6: `PlaytestCardArt.Sprite` 참조가 남지 않았는지 확인**

```bash
grep -rn "PlaytestCardArt.Sprite\|ResolveArtName\|ResolveResourcePath" Assets --include="*.cs"
```

Expected: 출력 없음.

- [ ] **Step 7: 테스트 통과 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: PASS.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Unity/PlaytestCardArt.cs Assets/Unity/BattleScreenController.cs Assets/Unity/DeckPlaytestController.cs Assets/Tests/UnityEditMode/PlaytestCardArtTests.cs
git commit -m "refactor(unity): drop dead card-art path resolution

ResolveResourcePath가 'Cards/<name>'을 만들지만 스프라이트는 전부 Cards/Player,
Cards/goblins 하위에 있어 16개 id 전부가 null을 로드하고 그 null이 캐시되었다.
카드 아트 fallback은 전체가 무동작이었고, 플레이어 카드 SO는 Art가 비어 있어
화면에는 색면만 표시된다. 삭제해도 화면은 바뀌지 않는다.

상태 아이콘 경로(Status/icon_lock)는 실제로 존재하고 런타임에 쓰이므로 유지한다.
깨진 경로를 green으로 고정하던 단언 2개를 제거한다.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: `pull_forward`를 SO 정본으로 통일

**Files:**
- Modify: `Assets/Core/Simulation/StarterDeck.cs` — `Build()`, `PullForward()`, `PushBack()` 추가
- Modify: `Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs` — golden 갱신, 교차 단언 추가

**Interfaces:**
- Consumes: `StarterDeckSpecs.PullForward()`/`PushBack()`가 정의한 값 — `pull_forward`는
  `InterventionEffectValue = -1`, `push_back`은 `"밀어내기"` / `InterventionEffectValue = 1`, 둘 다
  `EnergyCost = 1`, `InterventionActionKeys.ChangeExecutionOrder`
- Produces: `StarterDeck.PushBack()` → `CardDefinition` (id `push_back`).
  `StarterDeck.Build()`는 여전히 10장을 반환한다.

- [ ] **Step 1: 실패하는 교차 동등성 테스트를 먼저 추가**

`Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs`의 마지막 `[Test]` 아래에 추가한다.

```csharp
        [Test]
        public void Starter_specs_match_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeck.Build()),
                Sigs(StarterDeckSpecs.Build().Select(CardSpecMapper.ToDefinition)));

        [Test]
        public void Generated_starter_deck_matches_handcoded_deck()
            => CollectionAssert.AreEqual(
                Sigs(StarterDeck.Build()),
                Sigs(GeneratedCards.StarterDeck().Select(CardSpecMapper.ToDefinition)));
```

- [ ] **Step 2: 실패를 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~CardContentEquivalenceTests"
```

Expected: FAIL — 새 테스트 2개가 실패한다. 차이는 `pull_forward` 두 장(`...:-2`)과
`push_back` 부재다.

- [ ] **Step 3: `PullForward()` 효과값을 -1로 바꾸고 `PushBack()` 추가**

`PushBack()`을 먼저 정의해야 다음 단계에서 호출할 수 있다.

현재 (`Assets/Core/Simulation/StarterDeck.cs`):

```csharp
        public static CardDefinition PullForward() => InterventionCard(
            "pull_forward", "앞당김", interventionCost: 1,
            new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2));
```

변경 후 (두 메서드가 나란히 오도록):

```csharp
        public static CardDefinition PullForward() => InterventionCard(
            "pull_forward", "앞당김", interventionCost: 1,
            new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -1));

        public static CardDefinition PushBack() => InterventionCard(
            "push_back", "밀어내기", interventionCost: 1,
            new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: 1));
```

- [ ] **Step 4: `StarterDeck.Build()`에서 중복 `PullForward()`를 `PushBack()`으로 교체**

현재:

```csharp
            cards.Add(PullForward());
            cards.Add(PullForward());
            cards.Add(SwapPositions());
```

변경 후:

```csharp
            cards.Add(PullForward());
            cards.Add(PushBack());
            cards.Add(SwapPositions());
```

- [ ] **Step 5: golden 배열을 갱신**

`GoldenStarterDeckHandCoded`에서 `pull_forward` 두 줄을 지우고, `pull_forward`(-1)와 `push_back` 한 줄씩
넣는다. 정렬 순서를 지켜야 하므로(`CollectionAssert.AreEqual`는 순서를 본다) 배열 전체를
`GoldenStarterDeckSpecs`와 **동일한 내용**으로 만든다.

```csharp
        private static readonly string[] GoldenStarterDeckHandCoded =
        {
            "counter_stance;반격;Player;Execution;2;7;-;damage,4,PreviousExecutedCardHasEffect { Side = Enemy, EffectKey = damage },9,-,-",
            "cover;엄호;Player;Execution;1;5;-;apply_status,2,AdjacentCardHasEffect { Direction = Next, Side = Enemy, EffectKey = damage },7,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "guard;막기;Player;Execution;1;5;-;apply_status,4,-,-,-,block/ThisTurn:0/Self",
            "pull_forward;앞당김;Player;Intervention;1;0;change_execution_order:1:-1;",
            "push_back;밀어내기;Player;Intervention;1;0;change_execution_order:1:1;",
            "quick_cut;찰나의 베기;Player;Execution;1;5;-;damage,2,FirstToTrigger { },8,-,-",
            "slash;베기;Player;Execution;1;4;-;damage,4,-,-,-,-",
            "slash;베기;Player;Execution;1;4;-;damage,4,-,-,-,-",
            "swap_positions;자리 교환;Player;Intervention;1;0;swap_execution_order:1:0;"
        };
```

- [ ] **Step 6: 분기를 허용하던 헤더 주석 삭제**

같은 파일의 클래스 XML 주석에서 아래 문단을 **삭제**한다.

```
    /// Known cross-path divergences, intentionally NOT reconciled here (scheduled for P1-A cleanup):
    /// - pull_forward intervention effectValue: hand-coded StarterDeck has -2, specs/generated have -1.
    /// - push_back: absent from the hand-coded StarterDeck; present in specs and generated
    ///   as "밀어내기".
    /// Because of these, only the party prototype pair (currently equivalent) keeps a cross-path
    /// oracle test; the starter paths are each pinned against their own golden.
```

그 자리에 아래를 넣는다.

```
    /// All three starter paths (hand-coded, specs, generated) are content-equivalent and pinned by
    /// cross-path oracle tests, so a future divergence fails instead of being documented.
```

- [ ] **Step 7: 테스트 통과 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: PASS. 특히 `Starter_specs_match_handcoded_deck`,
`Generated_starter_deck_matches_handcoded_deck`, `Handcoded_starter_deck_matches_golden`가 모두 통과한다.
결정론 테스트(`CombatRngDeterminismTests`)도 통과해야 한다 — 실패하면 시작덱 구성 변화가 타임라인을
바꾼 것이므로 golden이 아니라 원인을 조사한다.

- [ ] **Step 8: 커밋**

```bash
git add Assets/Core/Simulation/StarterDeck.cs Assets/Core/Tests/EditMode/CardContentEquivalenceTests.cs
git commit -m "fix(content): unify pull_forward on the SO source of truth

하드코딩 시작덱은 pull_forward를 -2로 2장 갖고 push_back이 없었고, SO/specs/생성코드는
-1 1장 + push_back +1 1장이었다. Unity 씬이 실제로 소비하는 SO 값을 정본으로 삼아
하드코딩 카탈로그를 맞춘다.

교차 동등성 단언 2개를 추가해 세 저작 경로의 재분기를 테스트가 막게 한다. 분기를
의도적으로 허용하던 헤더 주석을 삭제한다.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: 레거시 단일 플레이어 shim 제거

**Files:**
- Modify: `Assets/Core/Combat/CombatState.cs` — shim 제거, `SoloPlayerId` 개명, `AddSoloPlayer` 추가
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs` — 솔로 분기, `OwnerStatusesFor`, `WithLegacyOwner`
- Modify: `Assets/Core/Simulation/ScenarioRunner.cs`
- Modify: `Assets/Core/Simulation/PlaytestSession.cs`
- Modify: `Assets/Core/Simulation/MultiTurnRunner.cs`
- Modify: `Assets/Core/Simulation/MultiTurnPlaytestSession.cs`
- Modify: `Assets/Core/Effects/ApplyStatusHandler.cs` — `ResolvePlayerSelf`
- Modify: `Assets/Core/Combat/Deck.cs` — `WithLegacyOwner`
- Modify: `Assets/Unity/DeckPlaytestController.cs` — HP·상태 표시 2곳
- Modify: 아래 17개 테스트 파일

**Interfaces:**
- Consumes: `PartyMember(string id, string name, int maxHp, int surviveCharges = 0)` — 생성자가
  `Hp = maxHp`로 초기화한다
- Produces:
  - `CombatState.SoloPlayerId` (const string, 값 `"player"`) — 기존 `LegacyPlayerId`의 새 이름
  - `CombatState.AddSoloPlayer(int hp)` → `PartyMember` — 솔로 멤버를 만들어 `Party`에 넣고 반환
  - `CombatState.PlayerHp`, `CombatState.PlayerStatuses`는 **더 이상 존재하지 않는다**

- [ ] **Step 1: 실패하는 테스트를 먼저 추가**

`Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs`에 추가한다. 이 파일의 기존 헬퍼
`NewSession(deck, intent)`(`playerHp: 30`으로 세션을 만든다)과 `Goblin(executionOrder, damage)`을
그대로 쓴다. 필요한 `using`은 이미 모두 있다.

```csharp
        [Test]
        public void Solo_session_player_is_an_explicit_party_member()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));

            Assert.AreEqual(1, session.State.Party.Count);
            Assert.AreEqual(CombatState.SoloPlayerId, session.State.Party[0].Id);
            Assert.AreEqual(30, session.State.Party[0].Hp);
            Assert.AreEqual(30, session.State.Party[0].MaxHp);
        }
```

- [ ] **Step 2: 실패를 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~Solo_session_player_is_an_explicit_party_member"
```

Expected: 컴파일 실패 — `CombatState.SoloPlayerId`가 아직 없다.

RED를 눈으로 확인하려면 그 한 줄만 잠시 `CombatState.LegacyPlayerId`로 바꿔 실행한다. 그러면 컴파일은
되고 마지막 단언에서 `Expected: 30, But was: 0`으로 FAIL한다 — 솔로 플레이어의 `MaxHp`가 `0`인
§3.4의 모순이 바로 이것이다. 확인 후 `SoloPlayerId`로 되돌리고 Step 3으로 넘어간다.

- [ ] **Step 3: `CombatState`에서 shim 제거**

`Assets/Core/Combat/CombatState.cs`의 상수·필드·생성자·shim 부분을 아래로 교체한다.

```csharp
        /// <summary>Id of the single party member that solo (non-party) combats use. Also the OwnerId
        /// stamped on solo deck cards, so deck ownership and party membership agree.</summary>
        public const string SoloPlayerId = "player";
        private const string SoloPlayerName = "Player";

        private Random _rng;

        /// <summary>Independent party formation; index 0 is the party's front.</summary>
        public List<PartyMember> Party { get; } = new();
```

즉 `_legacyPlayer` 필드, `LegacyPlayerName`, `LegacyPlayerDefaultMaxHp`, 빈 생성자,
`PlayerHp`, `PlayerStatuses`를 모두 삭제한다. 그리고 클래스 끝(`PlayerStatuses`가 있던 자리)에 추가한다.

```csharp
        /// <summary>Adds the solo-mode party member. Party mode adds its own members instead.</summary>
        public PartyMember AddSoloPlayer(int hp)
        {
            var member = new PartyMember(SoloPlayerId, SoloPlayerName, hp);
            Party.Add(member);
            return member;
        }
```

- [ ] **Step 4: `DeckCombatSession`의 솔로 분기와 `_isPartyMode` 잔재 정리**

현재 (`Assets/Core/Simulation/DeckCombatSession.cs`, private 생성자 안):

```csharp
            _isPartyMode = party != null;
            if (_isPartyMode)
            {
                _state.Party.Clear();
                foreach (var loadout in party)
                {
                    _state.Party.Add(new PartyMember(
                        loadout.Id,
                        loadout.Name,
                        loadout.MaxHp,
                        partyTuning.SurviveChargesPerCombat));
                }
            }
            else
            {
                _state.PlayerHp = playerHp;
            }
```

변경 후 (`Party.Clear()`가 사라진다 — 생성자가 더 이상 멤버를 미리 넣지 않는다):

```csharp
            _isPartyMode = party != null;
            if (_isPartyMode)
            {
                foreach (var loadout in party)
                {
                    _state.Party.Add(new PartyMember(
                        loadout.Id,
                        loadout.Name,
                        loadout.MaxHp,
                        partyTuning.SurviveChargesPerCombat));
                }
            }
            else
            {
                _state.AddSoloPlayer(playerHp);
            }
```

- [ ] **Step 5: `OwnerStatusesFor`의 모드 분기를 삭제**

현재:

```csharp
        private StatusBag OwnerStatusesFor(OwnedCard card)
        {
            if (!_isPartyMode)
            {
                return _state.PlayerStatuses;
            }

            foreach (var member in _state.Party)
            {
                if (member.IsAlive && member.Id == card.OwnerId)
                {
                    return member.Statuses;
                }
            }

            return null;
        }
```

변경 후 — 솔로에서도 `card.OwnerId == SoloPlayerId == Party[0].Id`이므로 파티 조회가 그대로 맞다.

```csharp
        private StatusBag OwnerStatusesFor(OwnedCard card)
        {
            foreach (var member in _state.Party)
            {
                if (member.IsAlive && member.Id == card.OwnerId)
                {
                    return member.Statuses;
                }
            }

            return null;
        }
```

- [ ] **Step 6: `LegacyPlayerId` 참조 2곳을 개명**

`Assets/Core/Simulation/DeckCombatSession.cs`:

```csharp
                owned.Add(new OwnedCard(card, CombatState.SoloPlayerId));
```

`Assets/Core/Combat/Deck.cs`:

```csharp
                yield return new OwnedCard(card, CombatState.SoloPlayerId);
```

- [ ] **Step 7: `ApplyStatusHandler.ResolvePlayerSelf` 단순화**

현재:

```csharp
            if (state.Party.Count == 1 && state.Party[0].Id == CombatState.LegacyPlayerId)
            {
                return state.Party[0];
            }
```

변경 후 — 바로 아래 `ResolveEnemySelf`의 `Enemies.Count == 1`과 대칭이 된다.

```csharp
            if (state.Party.Count == 1)
            {
                return state.Party[0];
            }
```

같은 메서드의 XML 주석에서 "pre-party legacy single-player shim" 표현을 아래로 고친다.

```csharp
        /// <summary>Player-side Self: the card's OwnerId party member if alive; with no OwnerId, only a
        /// single party member resolves unambiguously. Two or more ownerless members cancel instead.</summary>
```

- [ ] **Step 8: 순수 코어 러너 4곳의 상태 생성 배선 변경**

`Assets/Core/Simulation/ScenarioRunner.cs` (`BuildState`) — 현재:

```csharp
            var state = new CombatState
            {
                PlayerHp = scenario.PlayerHp,
                FateEnergy = scenario.FateEnergy
            };
```

변경 후:

```csharp
            var state = new CombatState { FateEnergy = scenario.FateEnergy };
            state.AddSoloPlayer(scenario.PlayerHp);
```

`Assets/Core/Simulation/PlaytestSession.cs` (`BuildState`) — 같은 형태이므로 동일하게 바꾼다.

`Assets/Core/Simulation/MultiTurnRunner.cs` (`Run`) — 현재:

```csharp
            var state = new CombatState { PlayerHp = scenario.PlayerHp };
```

변경 후:

```csharp
            var state = new CombatState();
            state.AddSoloPlayer(scenario.PlayerHp);
```

`Assets/Core/Simulation/MultiTurnPlaytestSession.cs` (생성자) — 현재:

```csharp
            _state = new CombatState { PlayerHp = scenario.PlayerHp };
```

변경 후:

```csharp
            _state = new CombatState();
            _state.AddSoloPlayer(scenario.PlayerHp);
```

`ScenarioDefinition.PlayerHp`, `MultiTurnScenario.PlayerHp`, `ScenarioResult.PlayerHp`와 비교·리포트
타입의 동명 필드는 **시나리오 저작·결과 필드이므로 건드리지 않는다.**

- [ ] **Step 9: Unity 솔로 플레이테스트 표시 수정**

`Assets/Unity/DeckPlaytestController.cs` — 현재:

```csharp
              .Append("    플레이어 HP: ").Append(_session.State.PlayerHp)
```

```csharp
              .Append("    ").Append(StatusText(_session.State.PlayerStatuses));
```

변경 후:

```csharp
              .Append("    플레이어 HP: ").Append(_session.State.Party[0].Hp)
```

```csharp
              .Append("    ").Append(StatusText(_session.State.Party[0].Statuses));
```

- [ ] **Step 10: 17개 테스트 파일을 기계적으로 마이그레이션**

대상 파일:

```
Assets/Core/Tests/EditMode/CardCancellationTests.cs
Assets/Core/Tests/EditMode/CombatRngDeterminismTests.cs
Assets/Core/Tests/EditMode/ConditionalEffectResolutionTests.cs
Assets/Core/Tests/EditMode/CounterStanceTests.cs
Assets/Core/Tests/EditMode/DamageHandlerTests.cs
Assets/Core/Tests/EditMode/DeckCombatSessionTests.cs
Assets/Core/Tests/EditMode/InterventionActionTests.cs
Assets/Core/Tests/EditMode/MultiTurnRunnerTests.cs
Assets/Core/Tests/EditMode/NewEffectLocalityTests.cs
Assets/Core/Tests/EditMode/PartyMemberTests.cs
Assets/Core/Tests/EditMode/PreviousExecutedCardConditionTests.cs
Assets/Core/Tests/EditMode/ScenarioComparisonTests.cs
Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs
Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs
Assets/Core/Tests/EditMode/StatusContentTests.cs
Assets/Core/Tests/EditMode/StatusTests.cs
Assets/Core/Tests/EditMode/TurnResolverTests.cs
```

변환 규칙은 세 가지다.

1. 객체 초기화로 HP를 넣던 생성:

   ```csharp
   var state = new CombatState { PlayerHp = 30, FateEnergy = 3 };
   ```

   →

   ```csharp
   var state = new CombatState { FateEnergy = 3 };
   state.AddSoloPlayer(30);
   ```

2. 읽기·쓰기:

   ```csharp
   Assert.AreEqual(26, state.PlayerHp);      →  Assert.AreEqual(26, state.Party[0].Hp);
   state.PlayerHp = 12;                       →  state.Party[0].Hp = 12;
   state.PlayerStatuses.Add(...);             →  state.Party[0].Statuses.Add(...);
   ```

3. `CombatState.LegacyPlayerId` → `CombatState.SoloPlayerId`

**주의:** 규칙 1을 적용한 뒤에는 `PlayerHp`를 설정하지 않던 `new CombatState()`가 파티 없는 상태가 된다.
그런 테스트가 `Party[0]`을 읽으면 인덱스 예외가 나므로, 해당 테스트가 플레이어를 필요로 하는지 확인하고
필요하면 `AddSoloPlayer`를 명시적으로 호출한다.

남은 참조가 없는지 확인한다.

```bash
grep -rn "PlayerHp\|PlayerStatuses\|LegacyPlayerId" Assets/Core/Tests Assets/Unity --include="*.cs" | grep -v "scenario.PlayerHp\|Scenario.PlayerHp\|\.PlayerHp =" 
```

Expected: 시나리오 저작·결과 필드(`ScenarioDefinition`/`MultiTurnScenario`/`ScenarioResult`의
`PlayerHp`)만 남는다. `CombatState`의 것은 하나도 없어야 한다.

- [ ] **Step 11: shim이 완전히 사라졌는지 확인**

```bash
grep -rn "_legacyPlayer\|LegacyPlayerId\|LegacyPlayerName\|LegacyPlayerDefaultMaxHp" Assets Tools --include="*.cs"
grep -n "PlayerHp\|PlayerStatuses" Assets/Core/Combat/CombatState.cs
```

Expected: 두 명령 모두 출력 없음.

- [ ] **Step 12: 테스트 통과 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: PASS, Step 1에서 추가한 `Solo_session_player_is_an_explicit_party_member` 포함.

**결정론 테스트가 실패하면 golden을 고치지 말고 원인을 조사한다.** 솔로 플레이어의 `MaxHp`가
`0`에서 `playerHp`로 바뀌는 것은 의도된 유일한 동작 변화이며, 회복 효과가 없는 현재 규칙에서는
타임라인이 동일해야 한다. 타임라인이 달라졌다면 `MaxHp`를 읽는 경로가 있다는 뜻이므로 보고한다.

- [ ] **Step 13: 커밋**

```bash
git add -A
git commit -m "refactor(core): remove legacy single-player shim from CombatState

CombatState 생성자가 만들던 _legacyPlayer는 파티 모드의 Party.Clear() 이후에도
private 필드로 살아남아, PlayerHp가 리스트 밖 고아 객체를 읽고 PlayerStatuses에
붙은 상태가 EndOfTurnMaintenance를 영원히 벗어나는 함정이었다.

솔로 모드는 이미 1인 파티였으므로(Party[0].Id와 카드 OwnerId가 모두 'player')
제거가 분기를 줄인다: OwnerStatusesFor의 _isPartyMode 분기가 사라지고,
ApplyStatusHandler.ResolvePlayerSelf가 Enemies.Count == 1과 대칭인
Party.Count == 1로 단순해진다.

LegacyPlayerId는 카드 OwnerId로 계속 필요하므로 SoloPlayerId로 개명해 남긴다.
솔로 플레이어의 MaxHp가 0에서 playerHp로 바뀌는 것이 유일한 의도된 동작 변화다.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: 문서 상태 갱신

**Files:**
- Modify: `docs/superpowers/plans/2026-07-25-combat-consistency-cleanup.md` — 상태를 완료로
- Modify: `docs/superpowers/README.md` — 활성 계획에서 제거
- Move: 이 문서를 `docs/superpowers/archive/plans/`로
- Modify: `docs/superpowers/archive/README.md` — 보관 색인에 추가

**Interfaces:**
- Consumes: 태스크 1~4의 완료
- Produces: 없음

- [ ] **Step 1: 전체 테스트 최종 확인**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

Expected: PASS. 실패하면 이 태스크를 진행하지 않는다.

- [ ] **Step 2: 완료 조건을 §6에 대조**

§6의 8개 항목을 하나씩 확인하고, 충족하지 못한 항목이 있으면 해당 태스크로 돌아간다.

- [ ] **Step 3: 문서 상태와 색인 갱신**

이 문서 머리말의 `상태: active — 구현 대기`를 `상태: archived — 2026-07-25 완료`로 바꾸고,
문서를 `docs/superpowers/archive/plans/`로 옮긴 뒤, `docs/superpowers/README.md`의 활성 계획 표에서
해당 줄과 "정합성 정리를 먼저 끝낸 뒤…" 문장을 제거한다. `docs/superpowers/archive/README.md`의
보관 구현 계획 목록에 추가한다 (저장소 규칙 20).

- [ ] **Step 4: 커밋**

```bash
git add -A
git commit -m "docs: archive completed combat consistency cleanup

Co-Authored-By: Claude <noreply@anthropic.com>"
```

## 9. 실행 시 주의

- **Unity EditMode 배치 테스트**는 컴파일 검증용으로만 이 워크트리에서 실행한다. 로그는 `/private/tmp`에
  저장하고, 실행 후 `git status`로 생성 파일이 스테이징되지 않았는지 확인한다 (규칙 17).
- **master 머지는 사용자 승인 후에만** 한다. 머지 전 전체 헤드리스 테스트 통과를 확인한다 (규칙 19).
- 태스크 4의 Step 10은 반복 작업이지만 `sed` 일괄 치환으로 처리하지 않는다. 규칙 1과 규칙 2의 변환이
  파일마다 섞여 있고, 파티를 만들지 않는 `new CombatState()`를 잘못 건드리면 인덱스 예외가 난다.
