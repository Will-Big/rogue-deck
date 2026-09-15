# 전투 노드 한 사이클 — 상세

사람 검수용 개요는 [`2026-09-15-combat-node-cycle-design.html`](2026-09-15-combat-node-cycle-design.html)에 있다.
구조 승인은 그쪽으로 받는다. 이 문서는 세션 인계용이며 `## 상세`만 담는다. 개요와 상세가 어긋나면
상세를 따르지 않고 멈추고 묻는다(규칙 29).

**상태:** `active` — 설계 승인 대기. 구현 착수 전.

## 상세

### 목표와 범위

전투 노드 하나를 처음부터 끝까지 완성한다: **구성 → 전투 → 승패 → (승리 시) 보상 선택 → 덱 반영 →
다음 전투.** 두 단계로 나눠 구현하고 각 단계 끝에서 머지 가능한 트리를 남긴다.

| 단계 | 범위 | 끝났을 때 |
|---|---|---|
| 1 | 흐름: 노드 시드·결과·보상·덱 반영·다음 전투, Unity 분리와 두 패널 | 사이클이 돈다. 적·수치는 아직 C# |
| 2 | 구성 저작: 적 카드·적·편성·캐릭터 스탯·전투 규칙 JSON | C# 원본(`GoblinDeck` 등)이 사라진다 |

**범위 밖:** 맵·노드 목록, 세이브, 새 게임마다 런 시드 새로 뽑기, HP 인계(매 전투 최대 HP로 시작),
보스·엘리트·영입, 사망 파티원의 카드 처리(유산), 패널 등장 연출, 보상 후보의 캐릭터 풀 소유권(§4.1).

### 결정 기록 (2026-09-14~15 사용자 결정)

1. **한 사이클 = 전투 노드 하나.** 노드 진입 과정·보스·맵은 아니다.
2. 보상은 **모든 카드에서 무작위** — 단 `Side == Player && Grade != None`만(픽스처·적 카드 제외).
   후보 공급은 `IRewardCandidateSource` 하나에 가둬 나중에 파티 소유 풀로 교체한다.
3. 후보마다 **소유 파티원이 함께 정해져** 나온다. 고른 카드는 그 파티원의 덱에 들어간다.
4. **노드 시드 방식(B안).** 노드 진입 전에 노드 시드를 확정하고, 그 노드 안의 스트림(편성·전투·보상)을
   노드 시드에서 **목적별로** 파생한다. 보상은 전투 수행과 무관하고, 노드마다 다르다. Slay the Spire의
   런 전체 `cardRng` 방식(k번째 보상이 경로와 거의 무관)은 기각했다.
5. **시드 결정론 = 같은 행동이면 같은 결과.** 행동과 무관하게 결과를 고정하는 설계를 하지 않는다.
   시드 동작을 새로 정할 때 판단이 서지 않으면 사용자에게 묻는다.
6. **저작은 "가능한 것들"만, "이번에 무엇이 나오나"는 시드가 정한다.** 편성은 후보를 저작하고 시드가
   고른다. 노드 목록은 런이 아니라 맵의 범주다(맵은 범위 밖).
7. **멤버 수치는 id로 인덱싱되는 캐릭터 JSON이 원본이다.** `maxHp`·`surviveCharges`는 멤버별.
8. 전투 규칙 파일 이름은 `combat_rules.json`.
9. `BattleScreenController`에 흐름을 얹지 않고 **`CombatNodeFlow`로 분리한다**(규칙 30).
10. "다음 전투"는 같은 `RunState`로 전투 노드를 한 번 더 시작한다(노드 순번이 1 증가).

---

### 1단계 — 흐름

#### 1.1 기존 Run 뼈대 정리

`Assets/Core/Simulation/Run/`의 타입은 프로덕션 참조가 0이다(2026-09-15 grep: 테스트와 자기 폴더 밖에서
참조하는 `.cs` 파일 없음). 노드 목록은 맵의 범주로 결정됐으므로(결정 6) 노드 목록 계열을 지운다.

| 파일 | 조치 |
|---|---|
| `RunDefinition.cs`, `RunNodeData.cs`, `RunDefinitionValidator.cs` | **삭제** — 노드 목록 |
| `RunNodeKey.cs`, `RunNodeRegistry.cs`, `IRunNodeHandler.cs`, `IRunNodePayload.cs` | **삭제** — 노드 종류 확장점. 맵 없이 해석할 호출자가 없다 |
| `RunState.cs` | **수정** — 노드 목록 제거, 노드 순번 추가(아래) |
| `RunMember.cs`, `RunOutcome.cs` | 유지 |
| 테스트 `RunNodeKeyTests`, `RunNodeRegistryTests` | 삭제. `RunStateTests`는 새 모양으로 재작성 |

`RunState` 새 모양:

```csharp
public sealed class RunState
{
    public RunState(IReadOnlyList<RunMember> startingParty, int runSeed);
    public int RunSeed { get; }
    public List<RunMember> Party { get; }
    public IReadOnlyList<RunMember> LivingMembers { get; }
    public RunOutcome Outcome { get; private set; }
    public int NodesEntered { get; private set; }   // 진입한 노드 수 = 다음 노드의 순번
    public int EnterNode();                          // 현재 순번을 돌려주고 1 증가
    public void SetOutcome(RunOutcome outcome);
}
```

`Rng`·`NextCombatSeed()`·`Tuning`은 지운다. 순차 스트림에서 전투 시드를 꺼내는 방식은 결정 4와 맞지 않는다.
`PartyTuning`은 1단계에서 `RunState`가 아니라 노드 문맥이 든다(1.3).

#### 1.2 시드 파생 — `SeedDerivation`

`Assets/Core/Simulation/Run/SeedDerivation.cs` (신규, 순수 함수, UnityEngine·`DateTime`·`Guid` 없음)

```csharp
public static class SeedDerivation
{
    public static int NodeSeed(int runSeed, int nodeIndex);
    public static int Stream(int nodeSeed, SeedStream stream);
}

public enum SeedStream : ulong
{
    Encounter = 0x454E43, // "ENC"
    Combat    = 0x434D42, // "CMB"
    Reward    = 0x524557, // "REW"
}
```

- 혼합은 **SplitMix64 finalizer**로 한다: `x = (ulong)parent * 0x9E3779B97F4A7C15 ^ tag` 후
  `x ^= x >> 30; x *= 0xBF58476D1CE4E5B9; x ^= x >> 27; x *= 0x94D049BB133111EB; x ^= x >> 31;`
  결과의 하위 32비트를 `int`로 자른다. `NodeSeed`의 tag는 `(ulong)nodeIndex + 1`.
- **`seed + index` 같은 선형 파생을 쓰지 않는다.** 인접 시드로 초기화한 `System.Random`들의 첫 출력이
  상관되는 문제가 Slay the Spire 2에서 실제로 보고됐다(개요 문서 출처). 해시 혼합으로 스트림을 분리한다.
- **`string.GetHashCode()`로 tag를 만들지 않는다** — .NET Core에서 프로세스마다 무작위화된다. tag는 위
  고정 상수다.
- 스트림은 **순서가 아니라 tag로** 파생되므로, 어느 스트림을 먼저 쓰든·안 쓰든 다른 스트림 값이 변하지
  않는다. 1단계에서 `Encounter` 스트림을 쓰지 않아도 2단계에서 `Combat`·`Reward` 값이 그대로다.

테스트(`SeedDerivationTests`): 같은 입력 = 같은 출력, 노드 순번 0·1의 노드 시드가 다름, 같은 노드
시드의 세 스트림이 서로 다름, **골든 값 고정**(구현 직후 실측값을 박아 두어 혼합식이 바뀌면 실패).

#### 1.3 전투 노드 — `CombatNode`

`Assets/Core/Simulation/Run/` (신규)

```csharp
public enum CombatNodePhase { Combat, Reward, Defeated, Done }

public sealed class CombatNodeContext
{
    public StatusContentCatalog Statuses;
    public PartyTuning PartyTuning;          // 2단계에서 CombatRules로 바뀐다
    public int FateEnergyPerTurn;             // 2단계에서 CombatRules로 바뀐다
    public int RewardChoices;                 // 1단계 값 3. 2단계에서 CombatRules로 바뀐다
    public IEncounterSource Encounters;
    public IRewardCandidateSource RewardCandidates;
}

public interface IEncounterSource
{
    /// 매 호출 새 적 인스턴스와 새 정책 인스턴스를 돌려준다(정책은 상태를 가진다 — ShuffleBag).
    EncounterSetup Pick(Random encounterRng);
}

public sealed class EncounterSetup
{
    public IReadOnlyList<Enemy> Enemies { get; }
    public IEnemyTurnPolicy Policy { get; }
}

public interface IRewardCandidateSource
{
    /// 결정론적 순서(id 서수 정렬)로 정렬된 후보 카드 전체.
    IReadOnlyList<CardDefinition> Eligible();
}

public sealed class RewardCandidate
{
    public CardDefinition Card { get; }
    public string OwnerId { get; }
}

public sealed class RewardOffer
{
    public IReadOnlyList<RewardCandidate> Candidates { get; }
}

public sealed class CombatNode
{
    public static CombatNode Begin(RunState run, CombatNodeContext context);
    public int NodeIndex { get; }
    public int NodeSeed { get; }
    public DeckCombatSession Session { get; }
    public CombatNodePhase Phase { get; }
    public RewardOffer Offer { get; }         // Phase가 Reward 또는 승리 후 Done일 때만 non-null
    public void Conclude();                    // Session.IsComplete && Phase == Combat 에서만
    public void Choose(int index);             // Phase == Reward 에서만 → Done
    public void Skip();                        // Phase == Reward 에서만 → Done
}
```

**`Begin`이 하는 일 (순서 고정)**
1. `run.Outcome != InProgress`면 `InvalidOperationException`.
2. `nodeIndex = run.EnterNode()`, `nodeSeed = SeedDerivation.NodeSeed(run.RunSeed, nodeIndex)`.
3. `setup = context.Encounters.Pick(new Random(Stream(nodeSeed, Encounter)))`.
4. 파티 로드아웃: `run.LivingMembers`를 파티 순서대로 `PartyMemberLoadout(member.Id, member.Name,
   member.MaxHp, member.Cards)`로. **HP 인계 없음** — 세션이 최대 HP로 시작한다.
5. `new DeckCombatSession(context.Statuses, loadouts, setup.Enemies, setup.Policy, context.PartyTuning,
   partyCards: null, fateEnergyPerTurn: context.FateEnergyPerTurn, seed: Stream(nodeSeed, Combat))`.

**`Conclude`**
- `Session.Outcome == Win` → 보상 제안 생성(아래) → `Phase = Reward`.
- `Session.Outcome == Lose` → `run.SetOutcome(RunOutcome.Defeat)` → `Phase = Defeated`. 보상 없음.
- 그 밖(`IsComplete == false`이거나 Phase 위반) → `InvalidOperationException`.

**보상 제안 생성 (소비량 고정)**
```
rng      = new Random(Stream(nodeSeed, Reward))
pool     = context.RewardCandidates.Eligible() 의 복사본
living   = Session.State.Party 중 IsAlive, 파티 순서
n        = min(context.RewardChoices, pool.Count)   // 부팅 검증으로 pool.Count ≥ RewardChoices 보장(2단계)
for i in 0..n-1:
    j = rng.Next(i, pool.Count); swap(pool[i], pool[j])   // 부분 Fisher–Yates, 중복 없음
    owner = living[rng.Next(living.Count)]
    candidates.Add(new RewardCandidate(pool[i], owner.Id))
```
- 후보 하나당 RNG 소비는 **정확히 2회**다. 살아있는 인원이 달라져도 소비량은 같으므로 **카드 목록은
  전투 수행과 무관**하고, **소유자만** 생존자가 바뀌면 달라진다. 이 성질을 테스트로 고정한다.
- 승리했는데 `living.Count == 0`은 불가능하다(전멸 = 패배). 방어 코드를 넣지 않는다.

**`Choose(i)`**: `i`가 범위 밖이면 `ArgumentOutOfRangeException`. `run.Party`에서 `OwnerId`가 같은 멤버의
`Cards`에 `Candidates[i].Card`를 추가 → `Phase = Done`. **`Skip()`**: 덱 불변 → `Phase = Done`.

#### 1.4 1단계 공급자 구현

| 타입 | 위치 | 구현 |
|---|---|---|
| `GoblinEncounterSource : IEncounterSource` | `Assets/Core/Simulation/` | `Pick`이 RNG를 무시하고 `new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp)` + `GoblinDeck.Policy()` |
| `GradedPlayerCardSource : IRewardCandidateSource` | `Assets/Core/Simulation/Run/` | `CardContentCatalog.Specs`에서 `Side == Player && Grade != CardGrade.None`, id 서수 정렬, `Cards[id]` |

`CardSpec.Grade`는 `Assets/Core/Authoring/CardSpec.cs:42`, 등급 없는 픽스처 4장은 `fixture_*`.
2026-09-15 기준 대상은 22장이다(`Pools/starter.json`의 22장과 같은 집합이어야 한다 — 테스트로 확인).

런 시작 파티 생성 헬퍼 `RunSetup.NewRun(GameContent content, IReadOnlyList<string> characterIds, PartyTuning tuning, int runSeed)`:
캐릭터마다 `new RunMember(id, displayName, tuning.DefaultMemberMaxHp, 덱 카드들)`. 덱 카드는 기존
`ContentLoadouts.For`와 같은 경로(`content.Characters.Get(id).Deck` → `content.Decks` → `content.Cards`).
`ContentLoadouts`는 이 헬퍼로 흡수하고 지운다(사용처는 `BattleScreenController.cs:95` 한 곳).

#### 1.5 헤드리스 테스트 (`Assets/Core/Tests/EditMode/`)

`CombatNodeTests` — 전투를 끝내는 헬퍼는 `SequencePolicy`로 적 행동을 고정한 테스트용 `IEncounterSource`와
짧은 HP를 쓴다.

1. **같은 런 시드·같은 노드 순번이면 전투를 다르게 싸워도 보상 카드 목록이 같다.** (턴 수가 다른 두 진행)
2. **노드 순번이 다르면 보상 카드 목록이 다르다.** (순번 0 vs 1, 고정 시드에서 실측 확인)
3. 같은 노드 시드에서 **생존자가 달라지면 카드 목록은 같고 소유자만 달라질 수 있다.**
4. 후보는 중복 없음, 전부 `Side.Player`이고 `Grade != None`, 소유자는 전부 생존 파티원.
5. `Choose(i)` → 그 소유자 덱만 +1, 다른 멤버 덱 불변, `Phase == Done`.
6. `Skip()` → 모든 덱 불변, `Phase == Done`.
7. 패배 → `Phase == Defeated`, `Offer == null`, `run.Outcome == Defeat`, 이후 `Begin`은 예외.
8. `Conclude`를 전투 중에, `Choose`를 `Reward` 밖에서, 범위 밖 번호로 부르면 예외.
9. 다음 전투: `Choose` 후 `Begin` → 새 세션의 전체 덱에 고른 카드가 있고, `NodeIndex == 1`.
10. `GradedPlayerCardSource.Eligible()`의 id 집합 == `Pools/starter.json`의 id 집합.

`SeedDerivationTests`(1.2), `RunStateTests` 재작성(`EnterNode` 증가, `LivingMembers`).

#### 1.6 Unity 분리

**`CombatNodeFlow`** (신규, `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs`, 관리자 객체)
- 책임: 런 상태를 들고 전투 노드를 시작·종료시키는 호출 순서를 정한다.
- `[SerializeField] private`: `BattleScreenController _battle`, `RewardChoiceView _reward`,
  `CombatResultView _result`, `CharacterAsset[] _party`(1단계: 시작 파티 순서 + 색), `int _runSeed = 1`.
- `Start()`: 콘텐츠 로드(`ContentBootstrap.Load(UnityContentRoot.Path)`, 실패 시 지금과 같은 메시지 +
  `Debug.LogError` — 현재 `BattleScreenController.cs:79-91`) → 컨텍스트 조립 → `_battle.Initialize(onRestart:
  NewRun, onCombatFinished: OnCombatFinished)` → `NewRun()`.
- `NewRun()`: `RunSetup.NewRun(...)` → `BeginNode()`.
- `BeginNode()`: `_node = CombatNode.Begin(_run, _context)` → 패널 둘 숨김 → `_battle.Bind(_node.Session, content)`.
- `OnCombatFinished()`: `_node.Conclude()` → `Reward`면 `_reward.Show(offer 표시값, OnChoose, OnSkip)`,
  `Defeated`면 `_result.ShowDefeat(NewRun)`.
- `OnChoose(i)`: `_node.Choose(i)` → `_reward.ShowNextButton(BeginNode)`. `OnSkip()`: `_node.Skip()` → 같음.
- 규칙 판단을 하지 않는다: 승패·후보·반영은 전부 `CombatNode`가 답한다.

**`BattleScreenController`** (수정)
- 삭제: `_party` 필드, `FateEnergyPerTurn`·`Seed` 상수, `_content` 로드, `StartSession()`의 구성 조립
  (`BattleScreenController.cs:70-120`).
- 추가: `Initialize(Action onRestart, Action onCombatFinished)` — HUD 리셋 버튼에 `onRestart`를 넘긴다
  (현재 `_hud.Initialize(OnTurnButton, StartSession, ...)`의 두 번째 인자 자리, `:55-56`).
- `StartSession()` → `Bind(DeckCombatSession session, GameContent content)`: 기존 `:109-125`(설명 카탈로그·
  presenter·units·piles 바인딩, "전투 시작." 메시지, `RefreshAll`)만 남긴다.
- `OnPlaybackComplete()`(`:332`): `_session.IsComplete`면 `onCombatFinished` 호출. 결과 메시지 한 줄은 유지.
- `OwnerNameOf`의 파티원 이름 조회는 그대로(`_session.State.Party`).
- `Start()`의 `StartSession()` 호출 제거 — 시작은 `CombatNodeFlow`가 한다.

**`RewardChoiceView`** (신규 뷰 + `Assets/Unity/Prefabs/RewardChoiceView.prefab`)
- 책임: 보상 후보를 보여주고 고른 번호를 알린다.
- `[SerializeField] private`: `RectTransform[] _slots`(3), `TMP_Text[] _ownerLabels`(3), `Button[] _slotButtons`(3),
  `Button _skipButton`, `Button _nextButton`, `CardPrefabCatalog _cards`, `BattlePresenter _presenter`.
- `Show(IReadOnlyList<RewardCandidate> candidates, Func<string, string> ownerName, Action<int> onChoose, Action onSkip)`:
  슬롯마다 `_cards.Create(_presenter.For(new OwnedCard(c.Card, c.OwnerId)), slot)`
  (`CardPrefabCatalog.cs:37`, `BattlePresenter.cs:32`), 소유자 라벨에 `ownerName(c.OwnerId)`, 다음 버튼 숨김.
- `ShowNextButton(Action onNext)`: 슬롯·건너뛰기 비활성, 다음 버튼 표시. `Hide()`.
- 소유자 이름은 `CombatNodeFlow`가 `_run.Party`의 `RunMember.Name`으로 조회하는 함수를 넘긴다.
  **`BattleScreenController`는 보상 표시에 관여하지 않는다.**
- 버튼 문구(`건너뛰기`·`다음 전투`)는 **프리팹 TMP 텍스트**로 저작한다. C#에 새 한글 문자열 없음.

**`CombatResultView`** (신규 뷰 + `Assets/Unity/Prefabs/CombatResultView.prefab`)
- 책임: 패배 결과를 보여주고 다시 시작을 알린다.
- `[SerializeField] private`: `Button _restartButton`. 문구(`패배`·`처음부터`)는 프리팹 텍스트.
- `ShowDefeat(Action onRestart)`, `Hide()`.

**`BattleSceneBuilder`** (수정, `Assets/Unity/Editor/BattleSceneBuilder.cs`)
- 두 패널 프리팹을 overlay 아래에 인스턴스화하고 기본 비활성. `CombatNodeFlow` 관리자 객체를 만들고
  참조를 배선한다. 컨트롤러의 `_party` 배선(`BattleSceneBuilder.cs:224`)을 flow로 옮긴다. presenter의
  `_party`(`:236-240`, 파티원 색)는 그대로 둔다.
- 씬 `Assets/Scenes/FateWeaverBattle.unity` 재생성.

**Unity EditMode 테스트** (`Assets/Tests/UnityEditMode/`)
- 두 패널 프리팹 계약: 슬롯·라벨·버튼 배열 길이 3, 참조 non-null.
- 씬 배선: `CombatNodeFlow`의 참조 전부 non-null, 컨트롤러에 `_party` 필드가 없음.

**사용자 확인(1단계 끝):** 두 패널의 위치·크기·색 조정, Play로 승리→보상→다음 전투, 패배→처음부터.

---

### 2단계 — 구성 저작

#### 2.1 새 JSON 형식

모든 파일은 `Assets/StreamingAssets/Content/` 아래. 폴더 이름 상수는 `CardContentFiles`
(`Assets/Core/Authoring/CardContentFiles.cs:12-16`)에 더한다.

**① 적 카드 — 기존 `Cards/`**

`goblin_jab.json`·`crude_guard.json`·`sly_jab.json`. 기존 `ExecutionCardSpec` 모양 그대로, `"side": "Enemy"`,
`grade`·`tags` 생략. 값은 `GoblinDeck.cs`의 `Thrust`·`CrudeGuard`·`SlyJab`에서 옮긴다:

| id | name | order | energyCost | effects |
|---|---|---|---|---|
| `goblin_jab` | 찌르기 | 6 | 0 | damage 4 |
| `crude_guard` | 조잡한 방어 | 4 | 0 | apply_status block 3, target Self |
| `sly_jab` | 약삭빠른 찌르기 | 3 | 0 | damage 3, `condition: { kind: NoPrecedingPlayerCard, successEffectValue: 6 }` |

**손으로 쓰지 않는다.** 카드 왕복 바이트 테스트가 키 순서·생략을 잠그므로 `CardSpec`을 만들어
`ContentJson` 직렬화기로 산출한다. 조건 객체 모양은 `foresight.json:13-16`과 같다.

**② `Enemies/<id>.json`** — `EnemySpec` (`Assets/Core/Authoring/Enemies/EnemySpec.cs`)

```json
{
  "id": "goblin",
  "displayName": "고블린",
  "maxHp": 28,
  "policy": "random_pick",
  "bundles": [
    ["goblin_jab"],
    ["sly_jab", "crude_guard"],
    ["sly_jab", "goblin_jab"],
    ["crude_guard", "crude_guard"]
  ]
}
```

- 로더 `EnemyContentLoader.Load(sources, CardContentCatalog cards, EnemyPolicyRegistry policies)` →
  `EnemyContentCatalog`. 검증: 필수 키, `maxHp > 0`, `bundles` 1개 이상, 빈 묶음 없음, 카드 id 존재,
  카드 `Side == Enemy`, `policy` 등록됨, id 중복 없음.
- `EnemyPolicyRegistry` (`Assets/Core/Simulation/Enemies/`, 규칙 9): 키 → `Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy>`.
  등록: `random_pick` → `RandomPickPolicy`, `shuffle_bag` → `ShuffleBagPolicy`, `sequence` → `SequencePolicy`
  (셋 다 `IReadOnlyList<EnemyCardBundle>` 생성자가 있다).
- 사용처: `ContentEncounterSource`가 편성의 적 id로 조회해 `Enemy`와 정책을 **매번 새로** 만든다.
  `BattleScreenController`의 적 이름 표시(`PlaytestKoreanText.EnemyName` 호출부)가 `displayName`을 쓴다.

**③ `Battles/<id>.json`** — `BattleSpec` (`Assets/Core/Authoring/Battles/BattleSpec.cs`)

```json
{ "id": "goblin_single", "enemies": ["goblin"] }
```

- 로더 `BattleContentLoader.Load(sources, EnemyContentCatalog enemies)` → `BattleContentCatalog`.
  검증: 파일 1개 이상, `enemies` 1개 이상, 적 id 존재, **한 편성 안에 같은 적 id 중복 금지**(전투 안 `Enemy.Id`가
  유일해야 한다 — 복수 동종 적은 범위 밖).
- 사용처: `ContentEncounterSource.Pick(rng)` = id 서수 정렬 후 `battles[rng.Next(count)]`.

**④ `Characters/<id>.json` (수정)** — `CharacterSpec`에 두 필드 추가

```json
{ "id": "member_a", "displayName": "파티원 A", "deck": "starter", "maxHp": 25, "surviveCharges": 1 }
```

- 로더 `CharacterContentLoader`에 필수 키 두 개와 검증(`maxHp > 0`, `surviveCharges >= 0`) 추가.
  기존 `member_a.json`·`member_b.json`에 25·1을 넣는다(현재 `PartyTuning.Prototype` 값,
  `Assets/Core/Simulation/PartyTuning.cs:29-30`).
- 사용처: `RunSetup.NewRun`이 `RunMember`의 `MaxHp`·`SurviveCharges`를 캐릭터에서 채운다.

**⑤ `combat_rules.json` (Content 루트 단일 파일)** — `CombatRulesSpec` → `CombatRules`

```json
{
  "fateEnergyPerTurn": 3,
  "minPartySize": 1,
  "maxPartySize": 3,
  "drawByLivingCount": { "1": 3, "2": 4, "3": 5 },
  "rewardChoices": 3
}
```

- 로더 `CombatRulesLoader.Load(source, GradedPlayerCardSource eligible)`. 검증: 필수 키,
  `fateEnergyPerTurn > 0`, `1 <= minPartySize <= maxPartySize`, `drawByLivingCount`가 1..maxPartySize 전부를
  양수로 덮음, `rewardChoices > 0`, **보상 대상 카드 수 >= rewardChoices**.
- 사용처: `CombatNodeContext`의 `FateEnergyPerTurn`·`RewardChoices`와 `PartyTuning` 생성.

#### 2.2 부팅 순서

`ContentBootstrap.Load`(`Assets/Core/Authoring/ContentBootstrap.cs:35-91`) 뒤에 이어 붙인다:
상태 → 카드 → 덱·풀 → 캐릭터 → **적 → 편성 → 전투 규칙**. 각 단계 실패 시 지금처럼 오류를 모아
`Failed`로 끝낸다. `GameContent`에 `Enemies`·`Battles`·`CombatRules`를 더한다.

#### 2.3 코드 변경과 대체

| 대체되는 것 | 위치 | 대체하는 것 |
|---|---|---|
| `GoblinDeck` (카드 3장·묶음 4개·정책·`EnemyId`·`StartingHp`) | `Assets/Core/Simulation/GoblinDeck.cs` | `Cards/` 적 카드 3장 + `Enemies/goblin.json` |
| `GoblinEncounterSource` (1단계 산물) | `Assets/Core/Simulation/` | `ContentEncounterSource` + `Battles/` |
| `PartyTuning.Prototype` | `PartyTuning.cs:27` | `combat_rules.json` |
| `PartyTuning.DefaultMemberMaxHp`·`SurviveChargesPerCombat` | `PartyTuning.cs:11-12` | 캐릭터 JSON `maxHp`·`surviveCharges` |
| `PartyPrototypeRoster` (id·이름 상수, `Tuning`) | `Assets/Core/Simulation/PartyPrototypeRoster.cs` | 캐릭터 JSON + `combat_rules.json` |
| `PlaytestKoreanText.EnemyName` | `Assets/Unity/Scripts/Text/PlaytestKoreanText.cs:46` | `Enemies/*.json`의 `displayName` |
| 세션의 전역 생존 충전 | `DeckCombatSession.cs:131`, 검증 `:459-460` | `PartyMemberLoadout.SurviveCharges` |
| `CombatNodeContext`의 1단계 값 필드 | 1.3 | `CombatRules` 하나 |

`PartyMemberLoadout`에 `SurviveCharges`, `RunMember`에 `SurviveCharges`(기본값, 전투마다 로드아웃으로 전달)를 추가한다.

#### 2.4 이관 순서 (동등성 먼저)

1. **골든 캡처:** `GoblinDeck`이 살아 있는 상태에서 고정 런 시드·노드 순번 0으로 `CombatNode`를 만들고
   고정 입력 시퀀스로 전투를 끝까지 돌려 타임라인 전체를 테스트 픽스처로 박는다(`GoblinParityTests`).
2. JSON 적 카드·적·편성·로더를 추가하고 `ContentEncounterSource`로 **같은 테스트를 통과**시킨다.
3. 그 뒤에야 `GoblinDeck`·`GoblinEncounterSource`·`PartyPrototypeRoster`·`PartyTuning.Prototype`·
   `EnemyName`을 지운다.
4. `GoblinDeck`을 참조하던 테스트를 `TestContent`(`Assets/Core/Tests/EditMode/TestContent.cs`)의 JSON
   고블린으로 옮긴다. 2026-09-15 기준 참조 파일: `GoblinDeckTests`·`DescriptionComposerTests`·
   `DescriptionCatalogValidatorTests`·`DeckPileVisibilityTests`·`StructuredCardDescriptionTests`·
   `CombatRngDeterminismTests`(Core). `PartyPrototypeRoster`는
   `DeckPoolCharacterContentTests`, `PartyTuning.Prototype`은 `PartyDeckCombatSessionTests`·`RunStateTests`.

#### 2.5 2단계 테스트

- 로더마다 위 검증 항목이 각각 오류로 잡히는지(파일 하나씩 깨뜨린 입력).
- 부팅: 저장소 콘텐츠 전체가 `Succeeded`.
- 편성 선택: 같은 노드 시드 = 같은 편성, 편성 후보 둘인 합성 콘텐츠에서 노드 순번에 따라 달라짐.
- 카드 왕복 바이트 테스트에 적 카드 3장 포함 통과. 노트북 테스트(`node --test "Tools/card-idea-notebook/*.test.mjs"`) 통과.
- `GoblinParityTests` 통과 후 C# 원본 삭제.

#### 2.6 문서 갱신 (2단계 커밋에 포함)

- `docs/agents/content-authoring.md` 「원본의 위치」에 적·편성·전투 규칙 JSON 추가.
- `docs/superpowers/README.md` 카드 콘텐츠 흐름의 "적 카드는 아직 JSON이 아니다" 문장 갱신.
- 백로그 §14.2의 `PlaytestKoreanText` 항목에 `EnemyName` 제거 반영.

### 알려진 제약

- **콘텐츠를 추가하면 같은 시드의 결과가 바뀐다.** 보상 대상·편성 후보 목록이 길이·순서째 RNG 입력이다.
  시드는 콘텐츠 버전에 묶인다.
- **적이 둘 이상인 편성은 적 카드의 소유자가 비워진다.** `DeckCombatSession`이 적이 정확히 하나일 때만
  소유자를 확정한다(`DeckCombatSession.cs:387` 부근, README 후속 작업 대기열). 이번 저작 편성은 1마리다.
- **한 편성 안 동종 적 중복 불가.** 전투 안 `Enemy.Id` 유일성 때문이다.
- 런 시드는 `CombatNodeFlow` 인스펙터 값이다. 새 게임마다 바꾸는 입력 경로는 없다.
