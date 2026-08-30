# 턴 재생 계층 설계 — 상세

- 작성일: 2026-08-30
- 상태: `active` (설계 개요 승인 2026-08-30)
- 범위: 한 턴의 해석 결과를 시간축 위에서 재생하는 표현 계층. 개별 연출의 모양·수치는 범위 밖이다.
- **사람 검수용 개요:** [2026-08-30-turn-playback-design.html](2026-08-30-turn-playback-design.html)

이 파일은 세션 인계용이다(규칙 28). 구조 요약·다이어그램·대안 비교·실행 예시는 위 HTML에 있고,
실행 근거는 아래 `## 상세`에만 있다. 개요와 상세가 어긋나면 상세를 따르지 않고 멈추고 묻는다(규칙 29).

---

## 상세 (세션 인계용)

**Goal:** `ResolveTurn()`이 만든 이벤트 목록을 비트 단위로 시간축 위에서 재생한다. 배속과 스킵을
갖추고, 개별 연출을 붙일 자리를 연다.

**Architecture:** 이벤트 하나 = 큐 하나(트윈 + 개시/후속 역할). 비트 하나 = DOTween `Sequence`
하나. 묶는 일은 순수 함수(`TimelineBeatPlanner`), 연출로 바꾸는 일은 레지스트리에 등록된 연출자,
뷰를 찾는 일은 `BattleStage`가 한다. 연출자는 이벤트 페이로드와 `BattleStage`만 보고
`CombatState`를 보지 않는다(규칙 11).

**Tech Stack:** C# 9 (Unity 6000.5.2f1 / netstandard2.1), DOTween 무료판(`DOTween.Modules`),
NUnit 3, Unity Test Framework 1.7.0 EditMode

**Spec:** 짝이 되는 [HTML 개요](2026-08-30-turn-playback-design.html). 이벤트 어휘는
[`Assets/Core/Events/ResolutionEvent.cs`](../../../Assets/Core/Events/ResolutionEvent.cs),
발행 순서는 [`TurnResolver.cs:151`](../../../Assets/Core/Combat/TurnResolver.cs) 및
[`DamageHandler.cs:161-205`](../../../Assets/Core/Effects/DamageHandler.cs).

## Global Constraints

- 작업은 **전용 워크트리**에서 한다(규칙 15). 메인 체크아웃의 브랜치를 전환하지 않는다.
- 헤드리스: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- **기준선: 557 passed / 0 failed (2026-08-30 master `cdb2545` 실측).** 이 계획은 `Assets/Core`를
  건드리지 않으므로 이 수치가 끝까지 변하지 않아야 한다. 변했다면 코어를 건드린 것이다.
- Unity EditMode 기준선은 착수 세션이 첫 실행에서 실측한다(README의 672는 계획 3.5 시점 수치로 낡았다).
  `-runTests`에 `-quit`를 같이 주지 않는다 — 테스트 없이 exit 0이 된다.
- **`Assets/Core`를 수정하지 않는다.** 이 계층은 코어 이벤트를 읽기만 한다. 코어에 손이 가야 한다면
  그것은 이 계획의 범위를 벗어난 신호이므로 멈추고 보고한다.
- 규칙 32: 연출은 DOTween·Particle System 등 기존 도구로 한다. 직접 구현하는 칸은 개요의
  「도구 선택」 표에 이유가 적힌 것뿐이다. 새 의존성은 사전 승인(규칙 14).
- 규칙 1·2·3: 런타임 `new GameObject` 금지(프리팹 인스턴스화만), `Resources.Load`·
  `GameObject.Find`·`FindObjectOfType` 금지, 경로 하드코딩 금지.
- 규칙 4: 인스펙터 노출은 `[SerializeField] private`.
- 규칙 8: 연출 시간·강도를 코드 상수로 박지 않는다. 전부 `[SerializeField]`로 뺀다.
- **새 `.cs`·`.prefab`·`.asset`에는 대응 `.meta`를 같은 커밋에 포함한다.** 계획 3.5가 `.meta`
  누락으로 한 번 겪었다(커밋 `de1b781`).
- DOTween 관례는 [`ExecutionRailView.cs:250-290`](../../../Assets/Unity/Scripts/Battle/ExecutionRailView.cs)을
  따른다 — `SetUpdate(true)`(unscaled), `SetLink(gameObject, LinkBehaviour.KillOnDestroy)`,
  `OnComplete`/`OnKill` 양쪽에서 완료 콜백을 부르되 `completionSent` 플래그로 중복을 막는다.
- 커밋 메시지: 규칙 27(`타입(범위): 한국어 현재형 제목`).

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Unity/Scripts/Battle/Playback/TimelineBeatPlanner.cs` | 이벤트 목록 → 비트 목록. 순수 정적 클래스, UnityEngine 미사용 |
| `Assets/Unity/Scripts/Battle/Playback/PlaybackCue.cs` | `PlaybackCue`(Tween + `CueRole`)와 `CueRole` enum |
| `Assets/Unity/Scripts/Battle/Playback/IResolutionEventPresenter.cs` | 연출자 계약 |
| `Assets/Unity/Scripts/Battle/Playback/EventPresenterRegistry.cs` | 이벤트 타입 → 연출자 (규칙 9) |
| `Assets/Unity/Scripts/Battle/Playback/BattleStage.cs` | id → `UnitMotionView`·앵커·숫자 스포너 |
| `Assets/Unity/Scripts/Battle/Playback/TurnPlaybackDirector.cs` | 비트 시퀀스 조립, 배속, 스킵, 완료 통지 |
| `Assets/Unity/Scripts/Battle/Playback/CardResolvedPresenter.cs` | 시전자 전진 (개시) |
| `Assets/Unity/Scripts/Battle/Playback/HpChangedPresenter.cs` | 셰이크·플래시·숫자·HP바 (후속) |
| `Assets/Unity/Scripts/Cards/UnitMotionView.cs` | 유닛 하나의 몸짓. DOTween 호출만 |
| `Assets/Unity/Scripts/Cards/FloatingNumberView.cs` | 숫자 하나를 띄우고 사라진다 |
| `Assets/Unity/Prefabs/FloatingNumberView.prefab` | TMP 한 개짜리 프리팹 |
| `Assets/Unity/Prefabs/UnitView.prefab` | `UnitMotionView` 컴포넌트 추가 |
| `Assets/Unity/Scripts/Battle/UnitView.cs` | HP바를 즉시 세팅 대신 트윈 가능한 API로 노출 |
| `Assets/Unity/Scripts/Battle/BattleUnitsView.cs` | `BattleStage`에 id→뷰 사전과 최대 HP 제공 |
| `Assets/Unity/Scripts/Battle/BattleHudView.cs` | 스킵·배속 컨트롤 추가 |
| `Assets/Unity/Scripts/Battle/BattleScreenController.cs` | `_playback` 1개 추가, `OnTurnButton`을 재생 대기로 |
| `Assets/Unity/Editor/BattleSceneBuilder.cs` | 재생 계층 배선 + 씬 재생성 |
| `Assets/Scenes/FateWeaverBattle.unity` | 위 배선의 결과 |

테스트: `Assets/Tests/UnityEditMode/TimelineBeatPlannerTests.cs`,
`EventPresenterRegistryTests.cs`, `TurnPlaybackDirectorTests.cs`.

---

### Task 1: `TimelineBeatPlanner` — 이벤트를 비트로 묶는다

이 계층에서 **유일하게 자동 검증되는 로직**이다. 규칙 32의 "경계에 걸치면 쪼갠다"에 따라 순수
함수로 분리했다. UnityEngine 타입을 쓰지 않는다.

**분할 규칙 (개시 이벤트가 비트를 연다):**

| 이벤트 | 역할 |
|---|---|
| `TurnStarted`, `TurnEnded` | 각각 단독 비트 |
| `CardResolved`, `CardCancelled` | 새 비트를 연다 (개시) |
| 연속 구간의 첫 `StatusTicked` | 새 비트를 연다 (턴 종료 틱 구간의 개시) |
| 그 밖의 모든 이벤트 | 현재 열린 비트에 담긴다 |

열린 비트가 없는 상태에서 개시가 아닌 이벤트를 만나면 그 이벤트만의 단독 비트를 만든다.

**`SourceId`는 경계를 정하지 않는다 — 검산에만 쓴다.** 카드가 연 비트 안의
`HpChanged(Source == CardDamage)`는 `SourceId`가 그 비트의 `CardId`와 같아야 하며, 이를 테스트가
단언한다. 순서로 묶고 `SourceId`로 검산하는 구조라 코어가 순서를 바꾸면 테스트가 걸린다
(개요 「이 선택으로 나중에 어려워지는 것」 3번).

- [ ] **Step 1: RED 테스트를 쓴다**

```csharp
[Test]
public void 광역_피해는_카드와_한_비트가_된다()
{
    var events = new ResolutionEvent[]
    {
        new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null),
        new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
        new HpChanged("member_b", 18, 14, HpChangeSource.CardDamage, "sweep"),
    };

    var beats = TimelineBeatPlanner.Plan(events);

    Assert.AreEqual(1, beats.Count);
    Assert.AreEqual(3, beats[0].Events.Count);
}

[Test]
public void 다음_카드는_새_비트를_연다()
{
    var events = new ResolutionEvent[]
    {
        new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null),
        new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
        new CardResolved(8, "member_a", "jab", Side.Player, 4, "goblin"),
        new HpChanged("goblin", 10, 6, HpChangeSource.CardDamage, "jab"),
    };

    var beats = TimelineBeatPlanner.Plan(events);

    Assert.AreEqual(2, beats.Count);
    Assert.AreEqual(2, beats[0].Events.Count);
    Assert.AreEqual(2, beats[1].Events.Count);
}

[Test]
public void 카드가_연_비트의_피해는_그_카드가_낸_것이다()
{
    var events = new ResolutionEvent[]
    {
        new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null),
        new HpChanged("member_a", 20, 15, HpChangeSource.CardDamage, "sweep"),
        new HpChanged("member_b", 18, 14, HpChangeSource.CardDamage, "sweep"),
    };

    var beat = TimelineBeatPlanner.Plan(events)[0];

    foreach (var hp in beat.Events.OfType<HpChanged>()
                 .Where(e => e.Source == HpChangeSource.CardDamage))
    {
        Assert.AreEqual("sweep", hp.SourceId, "순서로 묶은 비트가 SourceId와 어긋난다");
    }
}

[Test]
public void 턴_종료_틱은_보유자가_달라도_한_비트다()
{
    var events = new ResolutionEvent[]
    {
        new StatusTicked("goblin", "poison", 3, 3),
        new HpChanged("goblin", 10, 7, HpChangeSource.StatusTick, "poison"),
        new StatusTicked("member_a", "poison", 2, 2),
        new HpChanged("member_a", 15, 13, HpChangeSource.StatusTick, "poison"),
    };

    Assert.AreEqual(1, TimelineBeatPlanner.Plan(events).Count);
}

[Test]
public void 턴_경계는_단독_비트다()
{
    var events = new ResolutionEvent[]
    {
        new TurnStarted(0),
        new CardResolved(7, "goblin", "sweep", Side.Enemy, 9, null),
        new TurnEnded(0, Outcome.Ongoing),
    };

    var beats = TimelineBeatPlanner.Plan(events);

    Assert.AreEqual(3, beats.Count);
    Assert.AreEqual(1, beats[0].Events.Count);
    Assert.AreEqual(1, beats[2].Events.Count);
}
```

- [ ] **Step 2: RED 확인** — `TimelineBeatPlannerTests` 필터로 EditMode 실행. 타입이 없어 컴파일 실패.
- [ ] **Step 3: 최소 구현** — `PlaybackBeat`는 `IReadOnlyList<ResolutionEvent> Events`만 갖는
      불변 타입. `Plan`은 목록을 한 번 훑으며 위 표대로 자른다. `switch` 하나에 개시 이벤트 판정만
      담고, 이벤트 종류별 분기를 여기 쌓지 않는다.
- [ ] **Step 4: GREEN 확인 후 커밋** — `feat(ui): 타임라인을 동시 재생 단위인 비트로 나눈다`

---

### Task 2: 큐 계약과 연출자 레지스트리

**Interfaces:**

- `CueRole { Lead, Follow }` — 비트 안에서 개시인지 후속인지.
- `PlaybackCue` — `Tween Tween`, `CueRole Role`. `Tween`이 `null`이면 "연출 없음"이며 director가
  건너뛴다.
- `IResolutionEventPresenter` — `Type EventType { get; }`,
  `PlaybackCue Build(ResolutionEvent evt, BattleStage stage)`.
- `EventPresenterRegistry` — `Register(IResolutionEventPresenter)`,
  `TryResolve(ResolutionEvent, out IResolutionEventPresenter)`. 키는 이벤트의 `Type`이며 중복
  등록은 예외다(규칙 9의 부팅 검증에 해당).

**미등록 이벤트는 조용히 건너뛴다.** 17종 전부에 연출을 다는 것이 목표가 아니므로 정상 동작이다.
대신 EditMode 테스트가 "등록된 타입 집합"을 단언해, 등록을 빠뜨린 것과 의도적 미등록을 가른다.

- [ ] **Step 1: RED** — 중복 등록 예외, 미등록 조회 실패, 타입별 정확한 해결을 단언한다.
- [ ] **Step 2: RED 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: GREEN 후 커밋** — `feat(ui): 이벤트별 연출자를 레지스트리로 등록한다`

---

### Task 3: `BattleStage`와 뷰 도구 — id를 화면으로 바꾼다

`BattleStage`는 MonoBehaviour이며 `BattleUnitsView`에서 id→뷰 사전을 받는다. 규칙 3에 따라
계층 탐색·문자열 조회를 하지 않는다.

**Interfaces:**

- `BattleStage.MotionOf(string holderId) -> UnitMotionView` (없으면 `null`)
- `BattleStage.AnchorOf(string holderId) -> RectTransform`
- `BattleStage.HpBarOf(string holderId) -> UnitView`
- `BattleStage.MaxHpOf(string holderId) -> int` — **스폰 시점 스냅샷**이다. `HpChanged`가 최대 HP를
  싣지 않기 때문이며(개요 4번), `BattleUnitsView.Spawn`에서 채운다.
- `BattleStage.SpawnNumber(RectTransform anchor, int delta) -> FloatingNumberView`

`UnitMotionView`(신규, `UnitView.prefab`에 추가):

- `Tween Lunge(float direction)` — `DOAnchorPosX` 왕복. 규칙 32대로 직접 보간하지 않는다.
- `Tween HitShake()` — `DOShakeAnchorPos`.
- `Tween Flash(Color color)` — 초상 `Image`의 `DOColor` 왕복.
- 강도·시간·거리는 전부 `[SerializeField]`(규칙 8).

`UnitView`는 상태 표시만 유지하되(규칙 30), HP바를 트윈할 수 있도록 현재 비율을 읽고 쓰는
프로퍼티를 노출한다. 즉시 세팅 API(`SetHp`)는 그대로 둔다 — `RefreshAll()`이 계속 쓴다.

`FloatingNumberView`: TMP 하나짜리 프리팹. `Tween Play(int delta)`가 위로 떠오르며 페이드아웃하고
완료 시 자신을 파괴한다. 부호에 따른 색은 `[SerializeField]`.

- [ ] **Step 1: 프리팹·컴포넌트 구조를 만든다** (규칙 17: 구조는 직접, 수치는 사용자 체크포인트)
- [ ] **Step 2: `BattleStage` 조회 테스트** — 없는 id에 `null`/기본값을 돌려주는지, 최대 HP 스냅샷이
      스폰 시점 값인지 단언한다.
- [ ] **Step 3: 커밋** — `feat(ui): 유닛 몸짓과 부동 숫자 뷰를 더한다`

---

### Task 4: 최소 연출자 두 종

- `CardResolvedPresenter` — `evt.OwnerId`로 `MotionOf`를 얻어 `Lunge`. `TargetId`가 `null`이면
  정면, 아니면 대상 앵커 방향. 역할 `Lead`.
- `HpChangedPresenter` — `evt.HolderId`로 `HitShake` + `Flash` + `SpawnNumber(evt.After - evt.Before)`
  + HP바를 `evt.Before`에서 `evt.After`로 트윈. 역할 `Follow`.

두 연출자 모두 `CombatState`를 참조하지 않는다(규칙 11). 한국어 문자열을 갖지 않는다(규칙 10).

**모든 카드는 소유자를 갖는다.** [`DeckCombatSession.cs:434`](../../../Assets/Core/Simulation/DeckCombatSession.cs)가
캐릭터 덱의 카드마다 `loadout.Id`를 붙인다. `OwnerId == null`을 만드는 유일한 가지는 같은 파일
442줄의 `partyCards` 매개변수인데 `BattleScreenController`가 `partyCards: null`을 넘겨 **프로덕션에서
타지 않는다**(2026-08-30 확인). 따라서 소유자 없는 카드를 위한 fallback을 만들지 않는다 — 뷰를 못
찾으면 빈 큐를 돌려주고 끝낸다. 범용 카드가 생겨도 마찬가지다: 범용인 것은 `CardDefinition`이고,
덱에 들어가는 순간 `OwnedCard`가 소유자를 붙인다.

**취소된 카드는 이 범위에서 시전 연출이 없다.** `CardCancelled`는 비트를 열지만 연출자가 없으므로
`Lead` 큐가 비고, 뒤따르는 `HpChanged`만 `Join`된다. 그런데 `NoValidTarget`으로 중간 취소된 카드는
**이미 적용된 앞쪽 효과의 피해를 갖는다**(`CardCancelled.DamageDealt`, 코어 주석: "취소가 피해를
되돌리지 않으므로 로그에서도 사라지면 안 된다"). 즉 화면에서 **시전 모션 없이 피격만 일어나는** 순간이
생긴다. 이 범위에서는 그대로 두고, `CardCancelledPresenter`(레일 카드가 흔들리며 회색으로 꺼지는
연출)를 후속으로 남긴다. 재생이 붙은 뒤 Play에서 실제로 어색한지 먼저 보고 판단한다.

- [ ] **Step 1: 구현** — 각 연출자는 `DOTween.Sequence()`를 조립해 `PlaybackCue`로 돌려준다.
- [ ] **Step 2: 커밋** — `feat(ui): 카드 실행과 HP 변화의 연출자를 더한다`

---

### Task 5: `TurnPlaybackDirector` — 비트를 시간축에 올린다

**Interfaces:**

- `void Play(IReadOnlyList<ResolutionEvent> timeline, Action onComplete)`
- `void Skip()` — 루트 시퀀스를 즉시 완료(`Complete(withCallbacks: true)`).
- `float Speed { get; set; }` — 루트 시퀀스의 `timeScale`.
- `bool IsPlaying { get; }`

**조립 규칙:** 비트마다 하위 `Sequence`를 만들고, 비트 안에서 `Lead` 큐를 `Append`, `Follow` 큐를
그 뒤에 `Join`한다. `Lead`가 없으면 모든 `Follow`를 함께 `Join`한다. 비트 시퀀스들을 루트에
`Append`한다. 비트 사이 간격은 `[SerializeField]`.

**중복 완료를 막는다** — `OnComplete`와 `OnKill` 양쪽에서 콜백을 부르되 플래그로 한 번만 통과시킨다
(`ExecutionRailView`의 `completionSent` 관례).

- [ ] **Step 1: RED** — 빈 타임라인이 즉시 완료 콜백을 부르는지, `Skip()` 후 `IsPlaying`이 `false`이고
      완료 콜백이 정확히 한 번 불리는지, `Speed` 변경이 루트 `timeScale`에 반영되는지 단언한다.
- [ ] **Step 2: RED 확인**
- [ ] **Step 3: 구현**
- [ ] **Step 4: GREEN 후 커밋** — `feat(ui): 비트를 순서대로 재생하는 디렉터를 더한다`

---

### Task 6: 씬 배선 — 한 프레임 갱신을 재생으로 바꾼다

`BattleScreenController.OnTurnButton`의 현재 흐름은 `ResolveTurn()` → `Debug.Log` 루프 →
`RefreshAll()`이다([BattleScreenController.cs:296](../../../Assets/Unity/Scripts/Battle/BattleScreenController.cs)).
이를 `ResolveTurn()` → `Debug.Log` 루프 → `_playback.Play(timeline, 완료콜백)`으로 바꾸고,
완료 콜백에서 `RefreshAll()`을 부른다. **`Debug.Log` 덤프는 남긴다** — 개발용 로그는 재생과 무관하다.

- 재생 중에는 입력을 잠근다. `_hud.SetInputEnabled(false, false)`를 재사용하고, 완료 콜백에서
  `RefreshSelections()`가 원복한다.
- `_selection`은 재생 중 진입할 수 없어야 한다. `OnHandClicked`·`OnZoneClicked`에
  `_playback.IsPlaying` 가드를 더한다.
- `BattleHudView`에 스킵 버튼과 배속 토글을 더한다. 라벨·배치는 사용자 체크포인트(규칙 17).
- `BattleSceneBuilder`가 `TurnPlaybackDirector`·`BattleStage`를 관리자 객체로 만들고
  `SerializedObject`로 배선한다(기존 관례, `BattleSceneBuilder.cs:112`). 씬을 재생성한다.
- `[SerializeField]`는 `_playback` 하나만 는다(8 → 9). 입력 핸들러 분리는 **이번 범위 밖**이다
  (2026-08-30 사용자 결정).

- [ ] **Step 1: 컨트롤러·HUD 수정**
- [ ] **Step 2: `BattleSceneBuilder` 수정 후 씬 재생성**
- [ ] **Step 3: EditMode 회귀 확인 후 `git status`로 의도한 변경만 스테이징**(규칙 17 — 폰트 아틀라스
      같은 Play 부산물을 섞지 않는다)
- [ ] **Step 4: 커밋** — `feat(ui): 턴 해석 결과를 재생으로 보여준다`

---

### Task 7: 검증과 보관

- [ ] **Step 1:** 헤드리스 전체 실행 — **557 passed / 0 failed 유지**(코어 무변경 확인)
- [ ] **Step 2:** Unity EditMode 전체 실행, Task 1 시점 실측치와 비교
- [ ] **Step 3:** 금지 패턴 감사 — `Resources.Load`·`GameObject.Find`·`FindObjectOfType`·
      런타임 `new GameObject`가 새 파일에 없는지, 연출 수치가 코드 상수로 박히지 않았는지
- [ ] **Step 4: 사용자 Play 확인** — 광역 공격의 동시 피격, 배속, 스킵 후 화면이 최종 상태인지.
      **눈으로 판단할 것이므로 사용자 몫이다**(규칙 17)
- [ ] **Step 5:** 승인 후 이 계획을 `archive/plans/`로 옮기고 `docs/superpowers/README.md`를 같은
      커밋에서 갱신한다(규칙 20)

## 범위 밖

- **재생 중 플레이어 개입.** director에 일시정지·입력·재개가 없다(개요 「어려워지는 것」 1번).
- **나머지 15종 이벤트의 연출.** `StatusApplied`·`EnemyDied`·`FateEnergyGained` 등은 이 계층이
  선 뒤에 연출자를 하나씩 더하는 후속 작업이다. 레지스트리가 그 자리를 이미 열어 둔다.
  **1순위 후속은 `CardCancelledPresenter`다** — Task 4에 적은 대로, 중간 취소된 카드는 피해만 보이고
  시전 모션이 없다.
- **개별 연출의 모양·수치.** 셰이크 강도, 색, 지속 시간은 `[SerializeField]`로 열어 두고 사용자가
  Play에서 맞춘다.
- **캐릭터 스프라이트 애니메이션.** 2D Animation·Aseprite 임포터가 설치돼 있으나 스프라이트가 아직
  없다. 교체 지점은 `UnitMotionView` 하나이며 연출자는 바뀌지 않는다.
- **적 카드의 JSON화, 승패 화면, 유닛 상태 아이콘.** 각각 별도 작업이다.
