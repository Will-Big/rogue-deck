# 전투 시스템 정합성 정리 (설계)

- 작성일: 2026-07-25
- 문서 유형: `plan`
- 주 도메인: `combat-core`, `unity-presentation`
- 상태: `active` — 구현 대기
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

테스트 14개 파일 약 40곳이 `state.PlayerHp` → `state.Party[0].Hp`로 바뀐다.

#### 의도된 동작 변화 1건

현재 솔로 플레이어는 `MaxHp = 0`, `Hp = playerHp`라는 모순된 상태다(`PartyMember` 생성자가
`Hp = maxHp`로 두는데 shim이 `Hp`만 덮어썼기 때문). 제거 후에는 `MaxHp = Hp = playerHp`가 된다.

회복 효과가 아직 없으므로 현재 규칙 결과는 동일해야 한다. **이는 가정이 아니라 테스트로 확인할 사항이며,
결정론 타임라인 비교가 그 근거가 된다.**

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
- 커밋 4는 shim 제거 **전에** "파티 모드에서 솔로 API가 실패한다"를 단언하는 RED 테스트를 먼저 쓴다.
  제거 후에는 그 테스트를 "솔로 상태도 명시적 파티 멤버 하나로 표현된다"는 GREEN 단언으로 바꾼다.
- 커밋 3은 golden 서명 갱신이 곧 검증이며, 새로 추가하는 교차 동등성 단언이 재분기를 막는다.

Unity 검증은 저장소 규칙 17을 따른다. 컴파일과 자동화 검증을 위한 `-batchmode` EditMode 테스트는
이 워크트리에서 실행하되, 씬·프리팹·ScriptableObject 저작과 Play 검증은 하지 않는다. 로그는
`/private/tmp`에 남긴다.

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
