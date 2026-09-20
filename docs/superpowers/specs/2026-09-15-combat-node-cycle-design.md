# 전투 노드 한 사이클 — 상세

사람 검수용 개요는 [`2026-09-15-combat-node-cycle-design.html`](2026-09-15-combat-node-cycle-design.html)에 있다.
구조 승인은 그쪽으로 받는다. 이 문서는 세션 인계용이며 `## 상세`만 담는다. 개요와 상세가 어긋나면
상세를 따르지 않고 멈추고 묻는다(규칙 29).

**상태:** `active` — 1단계(흐름) 구현 완료·머지(2026-09-15). 2단계(구성 저작) 구현 완료(2026-09-17). 구현 계획 [`plans/2026-09-17-combat-node-stage2.md`](../.archive/plans/2026-09-17-combat-node-stage2.md)는 보관됐다. 1단계 구현 계획도 보관됐다.

**2026-09-18 제거:** 이 문서의 `surviveCharges`·`SurviveCharges`(치명타 버티기 충전)는 전투 실행 계약 구현 계획
T0에서 코드·캐릭터 JSON 모두 제거됐다(사용자 결정 D8). 아래 해당 서술은 작성 당시 기준이며, 재도입은 별도 설계로 한다.

## 상세

### 목표와 범위

전투 노드 하나를 처음부터 끝까지 완성한다: **구성 → 전투 → 승패 → (승리 시) 보상 선택 → 덱 반영 →
다음 전투.** 두 단계로 나눠 구현하고 각 단계 끝에서 머지 가능한 트리를 남긴다.

| 단계 | 범위 | 끝났을 때 |
|---|---|---|
| 1 | 흐름: 노드 시드·결과·보상·덱 반영·다음 전투, Unity 분리와 두 패널 | 사이클이 돈다. 적·수치는 아직 C# |
| 2 | 구성 저작: 적 카드·적·편성·캐릭터 스탯·전투 규칙 JSON | C# 원본(`GoblinDeck` 등)이 사라진다 |

**범위 밖:** 맵·노드 목록, 세이브, 새 게임마다 런 시드 새로 뽑기, HP 인계(매 전투 최대 HP로 시작),
보스·엘리트·영입, 사망 파티원의 카드 처리(유산), 패널 등장 연출, 캐릭터별 실제 풀 저작(이번엔 두 캐릭터 모두
`starter` 풀을 가리키는 임시 데이터).

### 결정 기록 (2026-09-14~15 사용자 결정)

1. **한 사이클 = 전투 노드 하나.** 노드 진입 과정·보스·맵은 아니다.
2. **보상은 살아남은 캐릭터를 기반으로 정해진다.** 살아남은 캐릭터 → 각자의 캐릭터 풀 → 그 풀에서 뽑을
   수 있는 카드. 후보는 **생존 캐릭터 풀의 합집합에서 카드가 겹치지 않게 n장**이다(2026-09-15 정정).
3. **소유자는 카드가 속한 풀에서 따라온다.** 카드를 뽑은 뒤 캐릭터에 붙이는 방식이 아니다. 고른 카드는 그
   풀 주인의 덱에 들어간다.
3a. **임시 방편은 데이터에 둔다.** 캐릭터가 아직 설계되지 않아 풀도 없으므로, 캐릭터 JSON에 `pool` 키를
   더하고 `member_a`·`member_b` 모두 기존 `starter` 풀(등급 있는 22장 전부)을 가리키게 한다. 캐릭터를
   설계하면 코드 변경 없이 JSON만 바꾼다. 카드풀 설계 §4.1은 "현재 파티에 포함된 캐릭터"라고 적었으나
   이 설계는 사용자 지시대로 "살아남은 캐릭터"를 쓴다.
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
11. **보상 추첨에 소유자 뽑기가 없다.** 공급자가 (카드, 소유자) 쌍을 돌려주고 노드는 쌍을 뽑는다. 앞서 정한
    "카드 먼저·소유자 나중" 순서는 결정 2·3으로 대체됐다(2026-09-15).
12. **HP 인계는 이번 범위 밖**이고, 전투 중 HP 0이 된 파티원의 런 처리 규칙도 이번엔 정하지 않는다.
    반드시 할 후속 작업으로 색인에 기록한다.
13. **같은 적 여럿·다중 적 카드 주인은 이번엔 모양만 맞춘다.** 적 JSON id와 전투 안 id를 분리하고,
    편성 공급자는 적마다 (적, 정책) 쌍을 돌려준다. 세션이 여러 쌍을 받는 일은 반드시 할 후속 작업이다.
14. **적 정책은 Core로 옮긴다(2026-09-17).** `IEnemyTurnPolicy`·정책 3종·`EnemyCardBundle`을 `FateWeaver.Core`로 옮기고
    정책 레지스트리를 `CombatRegistries`에 둔다. 적 로더가 효과 키와 같은 방식(`AuthoringContext`)으로 정책 키를 검증해
    **오타가 부팅 오류 목록에 섞여 잡힌다.** 기각: (B) Core에는 키 문자열만 두고 Simulation의 편성 공급자 생성 때 검증 —
    부팅 성공이 콘텐츠 유효를 보장하지 않고 오류 경로가 둘이 된다. (C) 적·편성·규칙 부팅을 Simulation에 따로 둠 —
    콘텐츠 묶음과 진입점이 둘이 되고 규칙 5의 "`ContentBootstrap.Load`가 읽는다"가 깨진다.
15. **`PartyTuning`도 Core로 옮기고 `CombatRules`가 품는다(2026-09-17).** `CombatRules { Party; FateEnergyPerTurn; RewardChoices }`.
    세션은 지금처럼 `PartyTuning`을 받는다. 기각: Core에 수치만 담은 별도 타입 + Simulation 변환(같은 필드가 두 타입에 중복),
    세션이 `CombatRules`를 직접 받음(전투와 무관한 `RewardChoices`를 세션이 알고, 세션 생성 테스트 약 20곳 변경).
16. **동등성 먼저(2026-09-17).** C# 고블린 경로의 전투 서명을 SHA-256 상수로 박고(실패 시 서명 전문을 출력) JSON 경로가 같은 서명을 내게 하고, 원본은 마지막에 지운다.

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

- 혼합은 **SplitMix64 finalizer**로 한다: `x = ((ulong)(uint)parent * 0x9E3779B97F4A7C15) ^ tag` 후
  `x ^= x >> 30; x *= 0xBF58476D1CE4E5B9; x ^= x >> 27; x *= 0x94D049BB133111EB; x ^= x >> 31;`
  결과의 하위 32비트를 `int`로 자른다. `NodeSeed`의 tag는 `(ulong)nodeIndex + 1`.
  음수 시드를 부호 확장하지 않도록 `(uint)`를 거쳐 0 확장한다 — 골든 테스트 `SeedDerivationTests`가 이 식을 잠근다.
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

/// 적마다 자기 정책을 든다. 편성 전체에 정책 하나를 두면 "이 카드가 어느 적 것인가"를 말할 수단이
/// 없어진다 — 지금 세션이 적 둘 이상에서 카드 주인을 비우는 원인이 그것이다(DeckCombatSession.cs:382-391).
public sealed class EncounterEnemy
{
    public Enemy Enemy { get; }
    public IEnemyTurnPolicy Policy { get; }
}

public sealed class EncounterSetup
{
    public IReadOnlyList<EncounterEnemy> Enemies { get; }
}

public interface IRewardCandidateSource
{
    /// 살아남은 캐릭터 각자의 풀에서 뽑을 수 있는 (카드, 소유자) 쌍 전체.
    /// 순서: 인자로 받은 캐릭터 순서(파티 순서) → 풀 안에서는 카드 id 서수 정렬.
    /// 소유자는 그 카드가 속한 풀의 주인이다. 한 카드가 여러 풀에 있으면 쌍이 여러 개 나온다(임시 데이터 기간).
    IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds);
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
2. `setup = context.Encounters.Pick(new Random(Stream(NodeSeed(run.RunSeed, run.NodesEntered), Encounter)))` —
   곧 들어갈 순번의 노드 시드에서 편성 스트림을 파생한다.
3. **세션은 아직 정책 하나만 받는다.** `setup.Enemies.Count != 1`이면 `InvalidOperationException`
   ("다중 적은 세션이 적마다 정책을 받게 된 뒤 지원" — 후속 작업). **`EnterNode` 전에** 검사하므로
   잘못된 편성이 노드 순번을 헛되이 늘리지 않는다(시드 결과는 2의 파생과 같다).
4. `nodeIndex = run.EnterNode()`, `nodeSeed = SeedDerivation.NodeSeed(run.RunSeed, nodeIndex)`.
5. 파티 로드아웃: `run.LivingMembers`를 파티 순서대로 `PartyMemberLoadout(member.Id, member.Name,
   member.MaxHp, member.Cards의 복사본)`으로. **HP 인계 없음** — 세션이 최대 HP로 시작한다. 그 뒤
   `new DeckCombatSession(context.Statuses, loadouts, [setup.Enemies[0].Enemy], setup.Enemies[0].Policy,
   context.PartyTuning, partyCards: null, fateEnergyPerTurn: context.FateEnergyPerTurn, seed: Stream(nodeSeed, Combat))`.

**`Conclude`**
- `Session.Outcome == Win` → 보상 제안 생성(아래) → `Phase = Reward`.
- `Session.Outcome == Lose` → `run.SetOutcome(RunOutcome.Defeat)` → `Phase = Defeated`. 보상 없음.
- 그 밖(`IsComplete == false`이거나 Phase 위반) → `InvalidOperationException`.

**보상 제안 생성**
```
rng    = new Random(Stream(nodeSeed, Reward))
living = Session.State.Party 중 IsAlive 의 Id, 파티 순서
pairs  = context.RewardCandidates.Eligible(living) 의 복사본 (List)
n      = min(context.RewardChoices, pairs 안의 서로 다른 카드 id 수)
repeat n times:
    j    = rng.Next(pairs.Count)
    pick = pairs[j]
    candidates.Add(pick)                           // 소유자는 쌍에 이미 들어 있다
    pairs.RemoveAll(p => p.Card.Id == pick.Card.Id) // 같은 카드의 다른 소유자 쌍 제거 → 카드 중복 없음
```
- 호출 시점: **승리 직후 `Conclude()` 안에서 한 번.** 전투 중에는 보상 스트림을 쓰지 않는다.
- 소비량은 **정확히 n회**다. 소유자를 따로 뽑지 않는다(결정 11).
- `RemoveAll`은 임시 데이터(두 캐릭터가 같은 풀) 기간에만 실제로 쌍을 지운다. 카드마다 풀이 하나뿐인 진짜
  데이터에서는 뽑힌 쌍 하나만 지워지므로 **추첨 코드를 바꾸지 않고** 풀 데이터만 바뀐다.
- 결과: **같은 노드·같은 생존자 구성이면 전투를 어떻게 싸웠든 같은 후보**다. **생존자 구성이 다르면**
  쌍 목록이 달라지므로 후보·소유자가 달라질 수 있다 — 결정 2의 의도된 동작이다.
- 승리했는데 `living.Count == 0`은 불가능하다(전멸 = 패배). 방어 코드를 넣지 않는다.
- 서로 다른 카드 수가 `RewardChoices`보다 적으면 있는 만큼만 제시한다(생존자 한 명의 풀이 작을 수 있어
  런타임에 생긴다). 0장이면 `Phase = Reward`로 가되 후보가 비고, 뷰는 건너뛰기만 보인다.

**`Choose(i)`**: `i`가 범위 밖이면 `ArgumentOutOfRangeException`. `run.Party`에서 `OwnerId`가 같은 멤버의
`Cards`에 `Candidates[i].Card`를 추가 → `Phase = Done`. **`Skip()`**: 덱 불변 → `Phase = Done`.

#### 1.4 1단계 공급자 구현

| 타입 | 위치 | 구현 |
|---|---|---|
| `GoblinEncounterSource : IEncounterSource` | `Assets/Core/Simulation/` | `Pick`이 RNG를 무시하고 쌍 하나: `new Enemy(id: "goblin#0", specId: GoblinDeck.EnemyId, hp: GoblinDeck.StartingHp)` + `GoblinDeck.Policy()` |
| `CharacterPoolRewardSource : IRewardCandidateSource` | `Assets/Core/Simulation/Run/` | 캐릭터마다 `content.Characters.Get(id).Pool` → `content.Pools`의 카드 id를 서수 정렬 → `(content.Cards.Get(cardId), id)` 쌍. **임시가 아니라 최종 구현이다** — 임시인 것은 데이터(결정 3a) |

**캐릭터 풀 연결 (결정 3a, 1단계에서 도입):** `CharacterSpec`(`Assets/Core/Authoring/Characters/CharacterSpec.cs`)에
`Pool` 필드를 더하고, `CharacterContentLoader.Load`에 풀 카탈로그를 넘겨 `pool` 필수 키와 "존재하는 풀 id" 검증을
더한다(기존 `deck` 검증과 같은 모양). 부팅 순서는 이미 덱·풀이 캐릭터보다 먼저다(`ContentBootstrap.cs:56-79`).
`member_a.json`·`member_b.json`에 `"pool": "starter"`. 풀 로더가 등급·태그 없는 카드를 거부하므로
(`PoolContentLoader.cs:131`) 픽스처·적 카드는 보상에 들어올 수 없다. **"카드는 정확히 한 풀에만" 검증은 켜지
않는다** — 임시 데이터가 그것을 어긴다. 캐릭터 설계 때 켠다.

**적 id 분리 (결정 13, 1단계에서 도입):** `Enemy`(`Assets/Core/Combat/Enemy.cs`)에 `SpecId`를 추가한다.
`Id`는 **전투 안 식별자**(대상 지정·사망 이벤트·카드 소유), `SpecId`는 **적 정의 식별자**(이름·저작 조회).
전투 안 id 규칙은 `"{specId}#{편성 내 순번}"`. 기존 생성자 `Enemy(string id, int hp)`는 `SpecId = id`로 남겨
기존 테스트를 건드리지 않는다. 적 이름 표시는 `Id`가 아니라 **`SpecId`로** 조회한다 — 1단계는
`PlaytestKoreanText.EnemyName(enemy.SpecId, …)`, 2단계는 적 JSON `displayName`. `BattleUnitsView.Spawn`에
넘기는 이름 함수가 전투 id를 받으므로, 컨트롤러가 `_session.State.Enemies`에서 그 id의 `SpecId`를 찾아 넘긴다.

런 시작 파티 생성 헬퍼 `RunSetup.NewRun(GameContent content, IReadOnlyList<string> characterIds, PartyTuning tuning, int runSeed)`:
캐릭터마다 `new RunMember(id, displayName, tuning.DefaultMemberMaxHp, 덱 카드들)`. 덱 카드는 기존
`ContentLoadouts.For`와 같은 경로(`content.Characters.Get(id).Deck` → `content.Decks` → `content.Cards`).
`ContentLoadouts`는 이 헬퍼로 흡수하고 지운다(사용처는 `BattleScreenController.cs:95` 한 곳).

#### 1.5 헤드리스 테스트 (`Assets/Core/Tests/EditMode/`)

`CombatNodeTests` — 전투를 끝내는 헬퍼는 `SequencePolicy`로 적 행동을 고정한 테스트용 `IEncounterSource`와
짧은 HP를 쓴다.

1. **같은 런 시드·같은 노드 순번·같은 생존자 구성이면 전투를 다르게 싸워도 보상 후보가 같다.** (턴 수가 다른 두 진행, 둘 다 전원 생존)
2. **노드 순번이 다르면 보상 후보가 다르다.** (순번 0 vs 1, 고정 시드에서 실측 확인)
3. **죽은 캐릭터는 소유자로 나오지 않는다.** 서로 다른 풀을 가진 합성 캐릭터 둘로, 한 명이 죽으면 모든 후보가
   살아남은 캐릭터 풀의 카드이고 소유자도 그 캐릭터다.
3a. **보상 스트림을 손으로 굴린 결과와 일치:** 같은 시드의 `Random`으로 위 알고리즘을 따라 굴린 쌍이 후보와
    같다(소비량 n회를 잠근다).
3b. **풀이 겹치는 임시 데이터에서도 카드 중복 없음:** 두 캐릭터가 같은 풀일 때 후보 카드 id가 서로 다르다.
3c. 편성 공급자가 적 둘을 돌려주면 `Begin`이 예외(세션 제약이 조용히 무시되지 않음).
4. 모든 후보의 카드는 그 소유자 캐릭터 풀에 들어 있고, 소유자는 전부 생존 파티원.
4a. 생존자 풀의 서로 다른 카드가 `RewardChoices`보다 적으면 그 수만큼만 제시.
5. `Choose(i)` → 그 소유자 덱만 +1, 다른 멤버 덱 불변, `Phase == Done`.
6. `Skip()` → 모든 덱 불변, `Phase == Done`.
7. 패배 → `Phase == Defeated`, `Offer == null`, `run.Outcome == Defeat`, 이후 `Begin`은 예외.
8. `Conclude`를 전투 중에, `Choose`를 `Reward` 밖에서, 범위 밖 번호로 부르면 예외.
9. 다음 전투: `Choose` 후 `Begin` → 새 세션의 전체 덱에 고른 카드가 있고, `NodeIndex == 1`.
10. 캐릭터 로더: `pool` 키 누락·존재하지 않는 풀 id가 각각 오류. 저장소 캐릭터 둘이 `starter` 풀로 로드된다.

`SeedDerivationTests`(1.2), `RunStateTests` 재작성(`EnterNode` 증가, `LivingMembers`).

#### 1.6 Unity 분리

**`CombatNodeFlow`** (신규, `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs`, 관리자 객체)
- 책임: 런 상태를 들고 전투 노드를 시작·종료시키는 호출 순서를 정한다.
- `[SerializeField] private`: `BattleScreenController _battle`, `RewardChoiceView _reward`,
  `CombatResultView _result`, `CharacterAsset[] _party`(1단계: 시작 파티 순서), `int _runSeed = 1`,
  `int _fateEnergyPerTurn = 3`·`int _rewardChoices = 3`(1단계 임시 튜닝 — 코드 상수로 박지 않기 위해 인스펙터
  값으로 두고, 2단계에서 `combat_rules.json`으로 옮긴다, 규칙 8).
- `Start()`: 배선 확인(`_battle`·`_reward`·`_result` null 아님) → `_battle.Initialize(onRestart: NewRun,
  onCombatFinished: OnCombatFinished)` → 콘텐츠 로드(`ContentBootstrap.Load(UnityContentRoot.Path)`, 실패
  시 지금과 같은 메시지 + `Debug.LogError` — 현재 `BattleScreenController.cs:79-91`) → 컨텍스트 조립 →
  `NewRun()`. Initialize를 먼저 하는 이유는 로드 실패 메시지를 HUD에 쓰기 위해서이며, 그 대가로 로드
  실패 뒤 재시작은 로드를 다시 시도하지 않는다.
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
- 버튼 문구(`선택`·`건너뛰기`·`다음 전투`)는 **프리팹 TMP 텍스트**다. 기존 관례대로 뷰의 `EditorCreate`가
  에디터 저작 시점에 굽고(`FloatingNumberView.EditorCreate`·`BattleSceneBuilder.MakeButton`과 같다), **런타임
  C#에는 새 한글 문자열이 없다.** 슬롯마다 카드 아래에 `선택` 버튼을 둔다 — 카드 프리팹이 클릭을 먹을 수 있어
  카드 자체를 버튼으로 쓰지 않는다.

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

2026-09-17 1단계 머지 후 코드와 대조해 다시 썼다(결정 14~16). 초안은 로더가 Simulation 타입을 받는
모양이라 컴파일되지 않았다 — `FateWeaver.Core`는 아무것도 참조하지 않고(`Assets/Core/FateWeaver.Core.asmdef`
`"references": []`) `FateWeaver.Simulation`만 Core를 참조한다(`Assets/Core/Simulation/FateWeaver.Simulation.asmdef`).

#### 2.1 어셈블리 이동 (결정 14·15)

코드는 바꾸지 않고 파일 위치와 네임스페이스만 옮긴다. 옮기는 커밋에서는 동작 변경을 섞지 않는다.

| 타입 | 지금 | 옮길 곳 | 근거 |
|---|---|---|---|
| `IEnemyTurnPolicy`, `RandomPickPolicy`, `ShuffleBagPolicy`, `SequencePolicy`, `EnemyCardBundle` | `Assets/Core/Simulation/Enemies/` (`namespace FateWeaver.Simulation`) | `Assets/Core/Enemies/` (`namespace FateWeaver.Core.Enemies`) | 다섯 파일은 `System`·`FateWeaver.Core.Cards`만 쓴다(`RandomPickPolicy.cs:1-3`, `IEnemyTurnPolicy.cs:1-3`) |
| `PartyTuning` | `Assets/Core/Simulation/PartyTuning.cs` | `Assets/Core/Combat/PartyTuning.cs` (`namespace FateWeaver.Core.Combat`) | `System`·`System.Collections.Generic`만 쓴다 |

- 참조를 고칠 파일(2026-09-17 grep): 정책 타입 — 프로덕션 `DeckCombatSession.cs`·`GoblinDeck.cs`·`Run/Encounters.cs`,
  테스트 `CombatNodeTests`·`ConditionalCardRuleTests`·`DeckCombatSessionTests`·`EncounterSourceTests`·`GrantNextTurnFateTests`·
  `LockCardTests`·`LockedEnemyExecutionOrderTests`·`OwnedCardDeckTests`·`PartyDeckCombatSessionTests`·`RandomPickPolicyTests`·
  `SequencePolicyTests`·`ShuffleBagPolicyTests`·`SlowHasteStatusTests`, Unity `BattleStageTests`·`BattleUnitsViewIdentityTests`·
  `ResolutionEventPresenterTests`. `PartyTuning` — 프로덕션 `DeckCombatSession.cs`·`PartyPrototypeRoster.cs`·`Run/CombatNode.cs`·
  `Run/RunSetup.cs`, 테스트 `CombatNodeTests`·`PartyDeckCombatSessionTests`·`RunSetupTests`와 위 Unity 테스트 셋.
  구현 착수 시 grep으로 다시 확인한다(규칙 31).
- Unity가 옮긴 파일의 `.meta` GUID를 유지하도록 `git mv`로 `.cs`와 `.meta`를 함께 옮긴다.

`PartyTuning`은 이관 6단계(2.7)에서 HP·생존 충전 필드를 잃는다(`Prototype`의 두 값도 함께 지운다). 남는 모양:

```csharp
public sealed class PartyTuning
{
    public int MinPartySize { get; init; }
    public int MaxPartySize { get; init; }
    public IReadOnlyDictionary<int, int> DrawByLivingCount { get; init; }
    public int DrawFor(int livingCount);   // 기존 그대로
}
```

`Prototype` 자체는 9단계(2.8)에서 지운다. 1~5단계의 동등성 테스트와 `RunSetup`이 쓰기 때문이다.

#### 2.2 정책 레지스트리 (규칙 9)

`Assets/Core/Enemies/EnemyPolicyRegistry.cs` (신규)

```csharp
public readonly struct EnemyPolicyKey : IEquatable<EnemyPolicyKey> { public string Id { get; } }  // EffectKey.cs:7과 같은 모양
public sealed class EnemyPolicyRegistry
{
    public void Register(EnemyPolicyKey key, Func<IReadOnlyList<EnemyCardBundle>, IEnemyTurnPolicy> create);
    public bool Contains(EnemyPolicyKey key);
    public IEnemyTurnPolicy Create(EnemyPolicyKey key, IReadOnlyList<EnemyCardBundle> bundles);  // 미등록이면 예외
}
```

- 키 타입은 `EffectKey`(`Assets/Core/Effects/EffectKey.cs:7-20` — `record struct` 아닌 일반 `readonly struct`, Unity C# 9)와
  같은 모양이고, 상수는 `EnemyPolicyKeys.RandomPick` 등으로 둔다(`EffectKeys`와 같다).
- `CombatRegistries.EnemyPolicies()`(`Assets/Core/Registries/CombatRegistries.cs`)에 등록: `random_pick` →
  `RandomPickPolicy`, `shuffle_bag` → `ShuffleBagPolicy`, `sequence` → `SequencePolicy`. 셋 다
  `IReadOnlyList<EnemyCardBundle>` 생성자가 있다(`RandomPickPolicy.cs:16`, `ShuffleBagPolicy.cs:16`, `SequencePolicy.cs:15`).
- `AuthoringContext`(`Assets/Core/Authoring/AuthoringContext.cs:9-35`)에 레지스트리를 하나 더 받고
  `HasEnemyPolicy(EnemyPolicyKey)`를 더한다. `Default()`는 `CombatRegistries.EnemyPolicies()`를 넘긴다.
- **카탈로그는 키만 든다.** 정책 인스턴스는 쓰는 쪽(`ContentEncounterSource`)이 레지스트리로 매번 새로 만든다.
  카드 효과가 `EffectKey`를 들고 실행 시 레지스트리로 해석되는 것과 같다.

#### 2.3 새 JSON 형식과 로더

모든 파일은 `Assets/StreamingAssets/Content/` 아래. 폴더 이름 상수는 `CardContentFiles`
(`Assets/Core/Authoring/CardContentFiles.cs:12-16`)에 `EnemiesFolderName = "Enemies"`, `BattlesFolderName = "Battles"`,
`CombatRulesFileName = "combat_rules.json"`을 더한다.

**① 적 카드 — 기존 `Cards/`**

`goblin_jab.json`·`crude_guard.json`·`sly_jab.json`. 기존 `CardSpec` 모양, `"side": "Enemy"`, `grade`·`tags` 생략.
값은 `GoblinDeck.cs:20-33`에서 옮긴다:

| id | name | order | energyCost | effects |
|---|---|---|---|---|
| `goblin_jab` | 찌르기 | 6 | 0 | damage 4 |
| `crude_guard` | 조잡한 방어 | 4 | 0 | apply_status block 3, 대상 `Self` |
| `sly_jab` | 약삭빠른 찌르기 | 3 | 0 | damage 3, `condition: { kind: NoPrecedingPlayerCard, successEffectValue: 6 }` |

- **손으로 쓰지 않는다.** 카드 왕복 바이트 테스트(`CardContentJsonTests.cs:236`, 노트북 `index.test.mjs:192`)가 키
  순서·생략을 잠그므로 `CardSpec`을 만들어 `ContentJson` 직렬화기로 산출한다.
- (2026-09-18: `StatusApplyTarget`은 [전투 실행 계약](2026-09-18-combat-execution-contract-design.md) 구현으로 제거됐다 — 아래는 작성 당시 기준) `StatusApplyTarget.Self`는 enum 값 0이라(`ApplyStatusHandler.cs:12`) `ContentJson`이 기본값으로 생략한다
  (`ContentJson.cs:37`). 산출 파일에 `"target"`이 없는 것이 정상이다.
- `NoPrecedingPlayerCard`는 `NoPrecedingCardOfSide(Side.Player)`로 매핑된다(`EffectSpec.cs:44-45`).
- 풀 로더의 등급·태그 검사는 풀 소속 카드에만 걸리므로(`PoolContentLoader.cs:122-131`) 적 카드가 보상에 들어갈 수 없다.

**② `Enemies/<id>.json`** — `EnemySpec` → `EnemyDefinition` (`Assets/Core/Authoring/Enemies/`)

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

- 묶음 = 적이 한 턴에 통째로 존에 올리는 카드 세트. 순서·구성은 `GoblinDeck.Bundles()`(`GoblinDeck.cs:59-65`)와 같다.
- 카탈로그 항목:
  ```csharp
  public sealed class EnemyDefinition
  {
      public string Id { get; }
      public string DisplayName { get; }
      public int MaxHp { get; }
      public EnemyPolicyKey Policy { get; }
      public IReadOnlyList<EnemyCardBundle> Bundles { get; }   // 카드 카탈로그의 CardDefinition을 공유
  }
  ```
- 로더 `EnemyContentLoader.Load(IEnumerable<CardContentSource>, CardContentCatalog, AuthoringContext)` → `EnemyContentCatalog`
  (`Get(id)`, `Ids`). 결과 타입은 기존 `CharacterContentLoadResult` 모양을 따른다.
- 검증(각각 별도 오류): 필수 키(`id`·`displayName`·`maxHp`·`policy`·`bundles`), 빈 id, id 중복, `maxHp <= 0`,
  묶음 0개, 빈 묶음, 없는 카드 id, 카드 `Side != Enemy`, `HasEnemyPolicy`가 거짓.
- 사용처: `ContentEncounterSource`(2.5), `BattleScreenController.EnemyNameOf`의 이름(2.6).

**③ `Battles/<id>.json`** — `BattleSpec` → `BattleDefinition` (`Assets/Core/Authoring/Battles/`)

```json
{ "id": "goblin_single", "enemies": ["goblin"] }
```

- 로더 `BattleContentLoader.Load(sources, EnemyContentCatalog)` → `BattleContentCatalog`(`Ids`는 서수 정렬, `Get(id)`).
- 검증: 파일 1개 이상, 필수 키, id 중복, 없는 적 id, **`enemies`가 정확히 1개**. 마지막 검증은 세션이 정책 하나만 받는
  제약 때문이며 다중 적 후속 작업에서 지운다. 같은 적 id 반복은 형식상 막지 않는다(전투 안 id가 `"{specId}#{순번}"`).

**④ `Characters/<id>.json` (수정)** — `CharacterSpec`(`Assets/Core/Authoring/Characters/CharacterSpec.cs`)에 두 필드

```json
{ "id": "member_a", "displayName": "파티원 A", "deck": "starter", "pool": "starter", "maxHp": 25, "surviveCharges": 1 }
```

- `CharacterContentLoader.RequiredKeys`(`CharacterContentLoader.cs:35`)에 `maxHp`·`surviveCharges`를 더하고
  `maxHp <= 0`, `surviveCharges < 0`을 오류로 잡는다.
- `member_a.json`·`member_b.json`에 25·1(현재 `PartyTuning.Prototype` 값, `PartyTuning.cs:29-30`). `member_b`의 덱은
  `party_prototype` 그대로다.

**⑤ `combat_rules.json` (Content 루트 단일 파일)** — `CombatRulesSpec` → `CombatRules` (`Assets/Core/Authoring/Rules/`)

```json
{
  "fateEnergyPerTurn": 3,
  "minPartySize": 1,
  "maxPartySize": 3,
  "drawByLivingCount": { "1": 3, "2": 4, "3": 5 },
  "rewardChoices": 3
}
```

```csharp
public sealed class CombatRules
{
    public PartyTuning Party { get; }        // Min·Max·DrawByLivingCount (결정 15)
    public int FateEnergyPerTurn { get; }
    public int RewardChoices { get; }
}
```

- 로더 `CombatRulesLoader.Load(CardContentSource)`. 파일이 없으면 오류.
- 검증: 필수 키, `fateEnergyPerTurn <= 0`, `minPartySize < 1`, `minPartySize > maxPartySize`,
  `drawByLivingCount`가 1..maxPartySize 중 하나라도 빠지거나 0 이하, `rewardChoices <= 0`.
- 보상 후보 수는 생존자에 따라 런타임에 정해지므로 부팅에서 검증하지 않는다(1.3 — 모자라면 있는 만큼).

#### 2.4 부팅 순서

`ContentBootstrap.Load`(`Assets/Core/Authoring/ContentBootstrap.cs:35-91`)가 한 번에 읽는다:
상태 → 카드 → 덱·풀 → 캐릭터 → **적 → 편성 → 전투 규칙**.

- 적 로더는 카드 카탈로그와 `AuthoringContext.Default()`를, 편성 로더는 적 카탈로그를 받는다.
- 전투 규칙은 다른 카탈로그에 의존하지 않지만 오류를 한 목록에 모으려고 같은 부팅에서 읽는다. 캐릭터까지 성공한 뒤
  적·편성·규칙 세 단계는 **오류를 모두 모은 뒤** `Failed`로 끝낸다(편성은 적이 실패하면 건너뛴다).
- `GameContent`(`Assets/Core/Authoring/GameContent.cs`)에 `Enemies`·`Battles`·`CombatRules`를 더한다. 생성자 호출부는
  `ContentBootstrap.cs:90` 한 곳이다.
- 클래스 주석(`ContentBootstrap.cs:30-32`의 "카탈로그 다섯")을 고친다.

#### 2.5 Simulation 쪽 변경

**`ContentEncounterSource : IEncounterSource`** (`Assets/Core/Simulation/Run/ContentEncounterSource.cs`, 신규)

```csharp
public ContentEncounterSource(GameContent content, EnemyPolicyRegistry policies);
public EncounterSetup Pick(Random encounterRng)
{
    var ids   = content.Battles.Ids;              // 서수 정렬
    var battle = content.Battles.Get(ids[encounterRng.Next(ids.Count)]);
    // 적마다 i = 편성 내 순번
    //   enemy  = new Enemy($"{specId}#{i}", specId, def.MaxHp)
    //   policy = policies.Create(def.Policy, def.Bundles)     // 매 호출 새 인스턴스
}
```

- 편성이 하나뿐이어도 `Next`를 한 번 부른다. 편성 스트림은 태그로 파생되므로(1.2) 전투·보상 스트림 값에 영향이 없다.
- `Flow`는 `CombatRegistries.EnemyPolicies()`를 넘긴다.

**`RunMember`·`PartyMemberLoadout`** — `SurviveCharges`(int, get-only)를 생성자 인자로 더한다.
- `RunMember(id, name, maxHp, surviveCharges, cards)` (`Assets/Core/Simulation/Run/RunMember.cs`)
- `PartyMemberLoadout(id, name, maxHp, surviveCharges, cards)` (`Assets/Core/Simulation/PartyMemberLoadout.cs`)
- `CombatNode.Begin`의 로드아웃 조립(`CombatNode.cs:106`)이 `member.SurviveCharges`를 넘긴다. 전투마다 전량 충전이다
  (`RunMember` 값은 전투 중 줄지 않는다).

**`DeckCombatSession`**
- `new PartyMember(..., partyTuning.SurviveChargesPerCombat)`(`DeckCombatSession.cs:127-131`) → `loadout.SurviveCharges`.
- `ValidateParty`의 튜닝 검사(`:458-460`)에서 HP·충전 조건을 빼고, 로드아웃마다 `MaxHp > 0`·`SurviveCharges >= 0`을 검사한다.
- 생성자 시그니처(`:82-90`)는 그대로다.

**`RunSetup.NewRun(GameContent content, IReadOnlyList<string> characterIds, int runSeed)`** — `PartyTuning` 인자 제거.
`RunMember`의 `MaxHp`·`SurviveCharges`를 `content.Characters.Get(id)`에서 채운다(`RunSetup.cs:11-27`).

**`CombatNodeContext`** (`CombatNode.cs:20-44`) — 생성자가 `(StatusContentCatalog statuses, CombatRules rules,
IEncounterSource encounters, IRewardCandidateSource rewardCandidates)`가 된다. 속성 `Rules` 하나가 기존
`PartyTuning`·`FateEnergyPerTurn`·`RewardChoices` 셋을 대체한다. `Begin`은 `rules.Party`·`rules.FateEnergyPerTurn`을,
보상 제안은 `rules.RewardChoices`를 쓴다. 클래스 주석의 "2단계에서 바뀐다" 문장을 지운다.

#### 2.6 Unity 쪽 변경

**`CombatNodeFlow`** (`Assets/Unity/Scripts/Battle/CombatNodeFlow.cs`)
- `_fateEnergyPerTurn`·`_rewardChoices` 필드 삭제(`:14-26`). `_runSeed`·`_party`·뷰 참조는 그대로.
- 컨텍스트 조립(`:56-62`): `new CombatNodeContext(_content.Statuses, _content.CombatRules,
  new ContentEncounterSource(_content, CombatRegistries.EnemyPolicies()), new CharacterPoolRewardSource(_content))`.
- `NewRun`(`:80`): `RunSetup.NewRun(_content, ids, _runSeed)`. `PartyPrototypeRoster` 참조가 사라진다.

**`BattleScreenController`**
- `EnemyNameOf`(`:92-98`): `_content.Enemies.Get(enemy.SpecId).DisplayName`. `Bind`가 이미 `GameContent`를 받는다.

**`PlaytestKoreanText`** (`Assets/Unity/Scripts/Text/PlaytestKoreanText.cs`)
- `EnemyName`(`:46-50`) 삭제. `CardName`의 고블린 카드 3건(`:33-35`)은 JSON `name`과 같으므로 삭제.

**씬** — `BattleSceneBuilder`로 `Assets/Scenes/FateWeaverBattle.unity`를 재생성한다. 지운 필드의 직렬화 값
(`FateWeaverBattle.unity:2198-2199`)이 남지 않아야 한다. 배치 실행은 `docs/agents/unity-batch-runs.md`.

**Unity EditMode 테스트** — `BattleStageTests`·`BattleUnitsViewIdentityTests`·`ResolutionEventPresenterTests`가
`new PartyTuning { DefaultMemberMaxHp, SurviveChargesPerCombat, … }`를 직접 만든다. 로드아웃 인자로 옮긴다.

#### 2.7 이관 순서 (동등성 먼저)

단계마다 `Tools/verify.sh`가 통과해야 커밋한다. C# 원본은 마지막에 지운다.

| # | 작업 | 확인 |
|---|---|---|
| 1 | **골든 캡처** `GoblinParityTests`: `GoblinEncounterSource`·`PartyTuning.Prototype`·저장소 캐릭터 둘로 런 시드 고정, 노드 0에서 `CombatNode.Begin`, 고정 입력 시퀀스로 전투가 끝날 때까지 진행(입력 규칙은 계획에서 정하되, 승패가 나고 운명력을 쓰는 카드가 한 장 이상 배치되게 한다). 서명은 `CombatRngDeterminismTests.RunSignature`(`:18-41`)와 같은 형식(턴별 손패 id + 해석 이벤트 `ToString`). 서명 문자열의 SHA-256을 **상수**로 박고 실패 시 서명 전문을 출력해 비교한다 | 새 테스트 통과 |
| 2 | 어셈블리 이동(2.1). 동작 변경 없음 | 전체 통과 |
| 3 | 적 카드 JSON 3장(2.3①). `ContentBootstrapTests.cs:20`의 26 → 29, `StructuredCardDescriptionTests.cs:106-110`의 `GoblinDeck.AllCards()` 이어 붙이기 제거 | 카드 왕복·노트북 테스트 |
| 4 | 정책 레지스트리·적 로더·`Enemies/goblin.json`(2.2, 2.3②) | 로더 테스트 |
| 5 | 편성 로더·`Battles/goblin_single.json`·부팅 연결·`ContentEncounterSource`(2.3③, 2.4, 2.5). **동등성 테스트의 공급자를 `ContentEncounterSource`로 바꾼다** | 골든과 동일 |
| 6 | 캐릭터 스탯·멤버별 생존 충전·`RunSetup` 인자 정리(2.3④, 2.5). `DeckPoolCharacterLoaderTests`의 인라인 캐릭터 JSON에 새 키 추가 | 골든과 동일 |
| 7 | `combat_rules.json`·`CombatRules`·`CombatNodeContext` 생성자(2.3⑤, 2.5). 동등성 테스트가 `content.CombatRules`를 쓰게 한다 | 골든과 동일 |
| 8 | Unity 변경·씬 재생성·Unity EditMode 테스트(2.6) | 배치 EditMode 통과 |
| 9 | 삭제와 테스트 이전(2.8)·문서 갱신(2.10) | `Tools/verify.sh` 전체 |

골든은 9단계 뒤에도 남는다 — C# 원본이 사라진 뒤에도 JSON 경로가 그 서명과 계속 비교된다.

#### 2.8 삭제와 테스트 이전

| 삭제 | 위치 |
|---|---|
| `GoblinDeck` | `Assets/Core/Simulation/GoblinDeck.cs` |
| `GoblinEncounterSource` | `Assets/Core/Simulation/GoblinEncounterSource.cs` |
| `PartyPrototypeRoster` | `Assets/Core/Simulation/PartyPrototypeRoster.cs` |
| `PartyTuning.Prototype`, `DefaultMemberMaxHp`, `SurviveChargesPerCombat` | `PartyTuning.cs` (2.1의 이동 후 위치) |
| `PlaytestKoreanText.EnemyName`, `CardName`의 고블린 3건 | 2.6 |

원본을 참조하는 테스트(2026-09-17 조사, 착수 시 grep 재확인):

| 테스트 | 조치 |
|---|---|
| `GoblinDeckTests` | 고블린 JSON을 검증하는 테스트로 바꾸거나(묶음 4개·정책 키·카드 3장) 로더 테스트에 흡수 |
| `EncounterSourceTests` | `ContentEncounterSource` 테스트로 교체 |
| `DescriptionComposerTests`·`DescriptionCatalogValidatorTests`·`DeckPileVisibilityTests`·`StructuredCardDescriptionTests`·`CombatRngDeterminismTests` | `TestContent.Content()`의 JSON 고블린(카드·적 정의)으로 이전 |
| `DeckPoolCharacterContentTests` (`:124-155`) | `PartyPrototypeRoster` 상수 대신 캐릭터 JSON 값으로 |
| `PartyDeckCombatSessionTests` (`:166-174` `Prototype` 단언, HP·충전 검증 테스트) | `combat_rules.json` 값 단언으로, 검증 테스트는 로드아웃 검증으로 |
| `RunSetupTests`·`CombatNodeTests` | 새 `NewRun`·`CombatNodeContext` 시그니처로 |

#### 2.9 2단계 테스트

- 로더마다 2.3의 검증 항목을 하나씩 깨뜨린 입력이 각각 오류로 잡힌다(적·편성·캐릭터 추가분·전투 규칙).
- 부팅: 저장소 콘텐츠 전체가 `Succeeded`이고 `Enemies`·`Battles`·`CombatRules`가 채워진다. 적 정책 키 오타는 부팅
  `Failed`의 오류 목록에 들어간다.
- `EnemyPolicyRegistry`: 기본 세 키 등록, 미등록 키 `Create` 예외.
- `ContentEncounterSource`: 같은 노드 시드 = 같은 편성. 편성 후보 둘인 합성 콘텐츠에서 노드 순번에 따라 달라짐.
  `Pick` 두 번이 서로 다른 정책 인스턴스를 돌려줌. 전투 안 id가 `goblin#0`, `SpecId`가 `goblin`.
- 멤버별 생존 충전: 충전 0인 멤버와 1인 멤버가 한 세션에서 따로 동작한다.
- `GoblinParityTests` 통과. 카드 왕복 바이트 테스트·노트북 테스트(`node --test "Tools/card-idea-notebook/*.test.mjs"`) 통과.
  노트북의 카드 수 단언(`index.test.mjs:192`, `>= 26`)은 그대로 통과한다.

#### 2.10 문서 갱신 (9단계 커밋에 포함)

- `docs/agents/content-authoring.md` 「원본의 위치」: `Content/<종류>/*.json` 한 줄에 `Enemies`·`Battles`를 더하고, 루트 단일
  파일 `combat_rules.json`을 별도 문장으로 적는다.
- `docs/superpowers/README.md:168`의 "적 카드는 아직 JSON이 아니다" 문장 갱신.
- 백로그 §14.2(`plans/2026-07-16-architecture-refactor-backlog.md:588-601`)의 `PlaytestKoreanText` 항목에 `EnemyName`·고블린
  카드 이름 제거 반영.
- 이 문서의 상태를 2단계 완료로, 색인 행 갱신, 2단계 계획 보관(규칙 20).

**사용자 확인(2단계 끝):** Play로 1단계와 같은 사이클(승리 → 보상 → 다음 전투, 패배 → 처음부터)과 적 이름 "고블린" 표시.

### 알려진 제약

- **콘텐츠를 추가하면 같은 시드의 결과가 바뀐다.** 캐릭터 풀의 카드 목록과 편성 후보 목록이 길이·순서째
  RNG 입력이다. 시드는 콘텐츠 버전에 묶인다. 임시 풀 데이터를 실제 캐릭터 풀로 바꾸는 순간에도 바뀐다.
- **임시 데이터 기간에는 "카드는 정확히 한 풀에만" 검증을 켤 수 없다.** 두 캐릭터가 같은 `starter` 풀을 가리킨다.
- **Core가 적 정책과 파티 튜닝을 안다.** 결정 14·15로 "적이 턴마다 무엇을 내놓는가"의 선택 규칙과 파티 규모·드로우 표가
  `FateWeaver.Core`에 들어간다. Simulation에서만 쓰던 타입이지만 콘텐츠 검증이 코어에 있으므로 따라 들어간다.
- ~~**편성은 적 한 마리만 저작할 수 있다.**~~ **2026-09-19 후속 2·3이 해결했다.** 세션이 (적, 정책)
  쌍 목록을 받고 편성 로더의 "적 정확히 1개" 검증이 사라졌다. 2026-09-20에 `goblin_pair.json`
  (고블린 2마리)을 저작했다 — **여러 마리 전투의 화면 표현은 아직 눈으로 확인되지 않았다.**
  적 카드는 대형 순서로 존에 올라가므로 실행 순서가 같으면 앞줄 적의 카드가 앞선다.
- ~~**HP가 전투 사이에 이어지지 않는다.**~~ **2026-09-19 후속 1이 해결했다.** 전투를 끝낸 HP가
  `RunMember.Hp`에 기록되고 다음 전투가 그 HP로 시작한다. **회복 수단이 아직 없다** — 치유 효과도
  휴식 노드도 없으므로 런의 HP는 단조 감소한다.
- 런 시드는 `CombatNodeFlow` 인스펙터 값이다. 새 게임마다 바꾸는 입력 경로는 없다.

### 반드시 할 후속 작업 (사용자 지정 필수, 색인 「후속 작업 대기열」에 기록)

1. ~~**HP 인계.**~~ **2026-09-19 완료.** 로드아웃이 현재 HP를 싣고(`PartyMemberLoadout.Hp`, 생략하면
   최대 HP), 세션이 그 HP로 파티원을 만들며, `CombatNode.Conclude`가 승패와 무관하게 전투가 끝난 HP를
   `RunMember.Hp`에 기록한다(음수는 0으로 깎는다). **선행 결정은 "사망 유지"로 정해졌다**(사용자 결정
   2026-09-19) — 이긴 전투에서 HP 0이 된 파티원은 런에서도 죽은 채로 남아 `RunState.LivingMembers`에서
   빠지고, 다음 전투와 보상 후보에 들어가지 않는다. 체력 1로 버티는 방식은 나중에 별도 설계로 도입한다.
2. ~~**다중 적.**~~ **2026-09-19 완료.** 세션이 `EncounterEnemy` 목록을 받아 적마다 정책을 호출하고
   그 적을 카드 주인으로 확정한다. 실행 순서 보정도 카드를 낸 그 적의 상태로만 한다.
   **선행 결정은 "죽은 적은 정책을 부르지 않는다"로 정해졌다**(사용자 결정 2026-09-19) — 카드도 내지
   않고 RNG도 소비하지 않으므로 적을 먼저 죽이면 이후 추첨이 달라진다(결정 5대로 의도된 차이다).
   죽은 적이 이미 올린 대기 카드는 사라진다. `BattleContentLoader`의 "적 정확히 1개" 검증도 지웠다.
3. ~~**같은 적 여럿.**~~ **2026-09-19 완료.** 추가 코드 없이 확인 테스트만 더했다 —
   `["goblin", "goblin", "rat"]` 편성이 로더를 통과하고 전투 안 id(`goblin#0`·`goblin#1`)로 갈린다.
