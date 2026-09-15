# 전투 노드 한 사이클 1단계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

사람 검수용 개요는 [`2026-09-15-combat-node-stage1.html`](2026-09-15-combat-node-stage1.html)에 있다.
설계 권위는 [`../../specs/2026-09-15-combat-node-cycle-design.md`](../../specs/2026-09-15-combat-node-cycle-design.md)의
「1단계 — 흐름」이다. **이 계획과 설계 상세가 어긋나면 구현을 멈추고 사용자에게 묻는다(규칙 29).**

**Goal:** 전투 한 판이 승리 → 생존 캐릭터 풀 기반 보상 선택 → 덱 반영 → 다음 전투, 패배 → 처음부터로 돈다.

**Architecture:** 코어 `CombatNode`가 전투 한 판의 단계 전이와 결과 반영을 소유하고, `SeedDerivation`이
런 시드·노드 순번에서 노드 시드와 목적별 스트림(편성·전투·보상)을 파생한다. 편성과 보상 후보는
`IEncounterSource`·`IRewardCandidateSource` 인터페이스 뒤에 있다. Unity에서는 새 조정자
`CombatNodeFlow`가 호출 순서만 정하고, `BattleScreenController`는 주어진 세션 하나를 화면에 붙이는
역할로 줄어든다. 적·수치는 1단계에서 C#(`GoblinDeck`·`PartyTuning.Prototype`) 그대로다.

**Tech Stack:** Unity 6000.5.2f1, C# 9(LangVersion 9 — record struct·file-scoped namespace 금지), NUnit 3,
헤드리스 `dotnet test`(net5.0 오버라이드), uGUI + TextMeshPro, Newtonsoft.Json

## Global Constraints

- 작업 위치는 **전용 워크트리**다. 메인 체크아웃 `/Users/ish/Git/rogue-deck`의 브랜치를 전환하지 않고, 거기서 `Assets/`를 건드리지 않는다(규칙 15·16). 브랜치 이름 `feat/combat-node-stage1`.
- `Assets/Core`는 UnityEngine을 참조하지 않는다(규칙 6). 무작위는 전부 시드에서 온다. `new Random()`(시드 없음)·`DateTime.Now`·`Guid.NewGuid()` 금지(규칙 7).
- 튜닝 수치를 코드 상수로 박지 않는다(규칙 8). 1단계의 운명력·보상 장수는 `CombatNodeFlow`의 `[SerializeField] private` 값이고, 2단계에서 `combat_rules.json`으로 간다.
- Unity 레이어는 `[SerializeField] private`만 쓴다(규칙 4). 런타임 `GameObject.Find`·`FindObjectOfType`·`Resources.Load` 신규 추가 금지(규칙 3). 화면 객체는 프리팹(규칙 1).
- **런타임 C#에 새 한글 UI 문구를 넣지 않는다.** 버튼 문구(`선택`·`건너뛰기`·`다음 전투`·`패배`·`처음부터`·`보상`)는 기존 관례대로 `EditorCreate`가 프리팹에 굽는다(`BattleSceneBuilder.MakeButton`·`FloatingNumberView.EditorCreate`와 같은 방식). 기존 컨트롤러에 있던 안내 문구를 옮기는 것은 신규가 아니다.
- 새 규칙 로직은 헤드리스 테스트가 필수다(규칙 12). 코어를 고친 턴은 Stop 훅이 `Tools/verify.sh`를 강제한다.
- 커밋 제목은 `타입(범위): 한국어 현재형 평서문`, `-다`로 끝난다(규칙 27). 본문은 왜 바꿨는지를 한국어로.
- 새 `.cs`는 Unity가 `.meta`를 만든다. Task 1~8은 `.cs`만 커밋하고, Task 9의 첫 Unity 배치 실행에서 생긴 `.meta`를 그때 모두 커밋한다. 파일을 지울 때는 `.cs`와 `.cs.meta`를 함께 `git rm`한다.
- 시드 동작을 계획에 없는 방식으로 바꾸지 않는다. 판단이 필요하면 멈추고 묻는다(사용자 지시).

## 사전 준비 (태스크 전에 한 번)

- [ ] **워크트리를 만든다**

세션 도구가 워크트리를 만들어 주면 그것을 쓴다(`.claude/worktrees/<작업명>`). 직접 만들 때:

```bash
git -C /Users/ish/Git/rogue-deck worktree add /Users/ish/Git/rogue-deck/.claude/worktrees/combat-node-stage1 -b feat/combat-node-stage1 master
```

이후 모든 경로는 이 워크트리 루트(`<WT>`) 기준이다.

- [ ] **훅을 켜고 기준선을 확인한다**

```bash
cd <WT> && Tools/setup-dev.sh && Tools/verify.sh
```

Expected: 마지막 줄 `모두 통과`. 헤드리스 통과 수를 기록해 둔다(이후 태스크의 증감 확인용).

---

### Task 1: 시드 파생 `SeedDerivation`

**Files:**
- Create: `Assets/Core/Simulation/Run/SeedDerivation.cs`
- Test: `Assets/Core/Tests/EditMode/SeedDerivationTests.cs`

**Interfaces:**
- Produces: `public enum SeedStream : ulong { Encounter = 0x454E43, Combat = 0x434D42, Reward = 0x524557 }`
- Produces: `public static class SeedDerivation { public static int NodeSeed(int runSeed, int nodeIndex); public static int Stream(int nodeSeed, SeedStream stream); }` (namespace `FateWeaver.Simulation.Run`)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/SeedDerivationTests.cs`:

```csharp
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>시드 파생식은 사실상 저장 형식이다 — 바뀌면 모든 기존 시드의 결과가 바뀐다.
    /// 골든 값은 설계 확정 시점(2026-09-15)에 같은 식을 독립 구현으로 계산해 박았다.</summary>
    public class SeedDerivationTests
    {
        [Test]
        public void NodeSeed_matches_the_golden_values()
        {
            Assert.AreEqual(459615264, SeedDerivation.NodeSeed(1, 0));
            Assert.AreEqual(479680206, SeedDerivation.NodeSeed(1, 1));
            Assert.AreEqual(-672360639, SeedDerivation.NodeSeed(-7, 3));
        }

        [Test]
        public void Stream_matches_the_golden_values()
        {
            var node = SeedDerivation.NodeSeed(1, 0);

            Assert.AreEqual(1652837576, SeedDerivation.Stream(node, SeedStream.Encounter));
            Assert.AreEqual(1998286874, SeedDerivation.Stream(node, SeedStream.Combat));
            Assert.AreEqual(-45639579, SeedDerivation.Stream(node, SeedStream.Reward));
        }

        [Test]
        public void Same_inputs_give_the_same_seed()
        {
            Assert.AreEqual(SeedDerivation.NodeSeed(42, 5), SeedDerivation.NodeSeed(42, 5));
        }

        [Test]
        public void Adjacent_nodes_and_streams_differ()
        {
            var node0 = SeedDerivation.NodeSeed(42, 0);
            var node1 = SeedDerivation.NodeSeed(42, 1);

            Assert.AreNotEqual(node0, node1);
            Assert.AreNotEqual(
                SeedDerivation.Stream(node0, SeedStream.Combat),
                SeedDerivation.Stream(node0, SeedStream.Reward));
            Assert.AreNotEqual(
                SeedDerivation.Stream(node0, SeedStream.Encounter),
                SeedDerivation.Stream(node0, SeedStream.Combat));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~SeedDerivationTests`
Expected: 빌드 실패 — `The type or namespace name 'SeedDerivation' could not be found`

- [ ] **Step 3: 구현한다**

`Assets/Core/Simulation/Run/SeedDerivation.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>노드 안에서 쓰는 무작위의 목적. 값은 스트림 이름표이며 저장 형식처럼 굳는다 —
    /// 바꾸면 모든 기존 시드의 결과가 바뀐다. string.GetHashCode는 프로세스마다 무작위화되므로
    /// 이름표를 문자열에서 만들지 않고 고정 상수로 둔다.</summary>
    public enum SeedStream : ulong
    {
        Encounter = 0x454E43,
        Combat = 0x434D42,
        Reward = 0x524557
    }

    /// <summary>런 시드에서 노드 시드를, 노드 시드에서 목적별 스트림 시드를 만든다(설계 결정 4).
    ///
    /// 스트림은 뽑는 순서가 아니라 이름표로 파생된다. 그래서 어느 스트림을 먼저 쓰든·안 쓰든 다른
    /// 스트림의 값이 변하지 않는다. seed + index 같은 선형 파생은 인접 시드로 초기화한 Random들의
    /// 첫 출력이 상관되므로(Slay the Spire 2 보고) SplitMix64 finalizer로 섞는다.</summary>
    public static class SeedDerivation
    {
        public static int NodeSeed(int runSeed, int nodeIndex)
            => Mix(runSeed, unchecked((ulong)nodeIndex + 1UL));

        public static int Stream(int nodeSeed, SeedStream stream)
            => Mix(nodeSeed, (ulong)stream);

        private static int Mix(int parent, ulong tag)
        {
            unchecked
            {
                ulong x = ((ulong)(uint)parent * 0x9E3779B97F4A7C15UL) ^ tag;
                x ^= x >> 30;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 27;
                x *= 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return (int)(uint)x;
            }
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~SeedDerivationTests`
Expected: `Passed: 4`. 골든 값이 다르면 구현을 고치지 말고 Step 3 코드와 한 글자씩 대조한다(특히 `(uint)` 캐스트와 `^`의 괄호).

- [ ] **Step 5: 커밋한다**

```bash
git add Assets/Core/Simulation/Run/SeedDerivation.cs Assets/Core/Tests/EditMode/SeedDerivationTests.cs
git commit -m "feat(core): 런 시드에서 노드 시드와 목적별 스트림을 파생한다"
```

---

### Task 2: `RunState` 재구성과 노드 목록 계열 삭제

**Files:**
- Modify: `Assets/Core/Simulation/Run/RunState.cs` (전체 교체)
- Delete: `Assets/Core/Simulation/Run/RunDefinition.cs`, `RunNodeData.cs`, `RunDefinitionValidator.cs`, `RunNodeKey.cs`, `RunNodeRegistry.cs`, `IRunNodeHandler.cs`, `IRunNodePayload.cs` (각 `.meta` 포함)
- Delete: `Assets/Core/Tests/EditMode/RunNodeKeyTests.cs`, `RunNodeRegistryTests.cs` (각 `.meta` 포함)
- Modify: `Assets/Core/Tests/EditMode/RunStateTests.cs` (전체 교체)

**Interfaces:**
- Produces: `public RunState(IReadOnlyList<RunMember> startingParty, int runSeed)`, `int RunSeed`, `List<RunMember> Party`, `IReadOnlyList<RunMember> LivingMembers`, `RunOutcome Outcome`, `int NodesEntered`, `int EnterNode()`, `void SetOutcome(RunOutcome)`
- 사라지는 것: `Nodes`, `CurrentNodeIndex`, `CurrentNode`, `AdvanceToNextNode()`, `Tuning`, `Rng`, `NextCombatSeed()`

근거: 2026-09-15 grep으로 위 7개 타입과 `NextCombatSeed`를 참조하는 프로덕션 코드가 없다(참조는 자기 폴더와 테스트 3개뿐). 노드 목록은 맵의 범주로 결정됐다(설계 결정 6).

- [ ] **Step 1: 테스트를 새 모양으로 교체한다**

`Assets/Core/Tests/EditMode/RunStateTests.cs` 전체:

```csharp
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class RunStateTests
    {
        private static RunState NewRun(int seed) => new RunState(
            new[]
            {
                new RunMember("member_a", "파티원 A", 25, null),
                new RunMember("member_b", "파티원 B", 25, null)
            },
            seed);

        [Test]
        public void EnterNode_returns_the_current_index_then_advances()
        {
            var run = NewRun(seed: 1);

            Assert.AreEqual(0, run.NodesEntered);
            Assert.AreEqual(0, run.EnterNode());
            Assert.AreEqual(1, run.EnterNode());
            Assert.AreEqual(2, run.NodesEntered);
        }

        [Test]
        public void RunSeed_is_kept()
        {
            Assert.AreEqual(41, NewRun(seed: 41).RunSeed);
        }

        [Test]
        public void LivingMembers_excludes_dead_in_party_order()
        {
            var run = NewRun(seed: 1);
            run.Party[0].Hp = 0;

            Assert.AreEqual(1, run.LivingMembers.Count);
            Assert.AreEqual("member_b", run.LivingMembers[0].Id);
        }

        [Test]
        public void Outcome_starts_in_progress_and_is_settable()
        {
            var run = NewRun(seed: 1);

            Assert.AreEqual(RunOutcome.InProgress, run.Outcome);
            run.SetOutcome(RunOutcome.Defeat);
            Assert.AreEqual(RunOutcome.Defeat, run.Outcome);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~RunStateTests`
Expected: 빌드 실패 — `RunState`에 인자 2개 생성자·`EnterNode`·`NodesEntered`가 없다

- [ ] **Step 3: `RunState`를 교체하고 노드 목록 계열을 지운다**

`Assets/Core/Simulation/Run/RunState.cs` 전체:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Simulation.Run
{
    /// <summary>전투 사이에 이어지는 런 상태: 런 시드, 파티원(덱·HP), 진입한 노드 수.
    /// 노드 목록은 여기 없다 — 노드 목록은 맵의 범주다(전투 노드 설계 결정 6). 무작위는 이 객체가
    /// 들고 있지 않고, SeedDerivation이 런 시드와 노드 순번에서 노드마다 새로 파생한다.</summary>
    public sealed class RunState
    {
        public RunState(IReadOnlyList<RunMember> startingParty, int runSeed)
        {
            Party = new List<RunMember>(startingParty);
            RunSeed = runSeed;
        }

        public int RunSeed { get; }
        public List<RunMember> Party { get; }
        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;

        /// <summary>진입한 노드 수. 다음에 진입할 노드의 순번이기도 하다.</summary>
        public int NodesEntered { get; private set; }

        public IReadOnlyList<RunMember> LivingMembers => Party.Where(m => m.IsAlive).ToList();

        /// <summary>현재 순번을 돌려주고 1 늘린다. 노드 시드는 이 순번에서 파생된다.</summary>
        public int EnterNode() => NodesEntered++;

        public void SetOutcome(RunOutcome outcome) => Outcome = outcome;
    }
}
```

```bash
git rm Assets/Core/Simulation/Run/RunDefinition.cs Assets/Core/Simulation/Run/RunDefinition.cs.meta \
  Assets/Core/Simulation/Run/RunNodeData.cs Assets/Core/Simulation/Run/RunNodeData.cs.meta \
  Assets/Core/Simulation/Run/RunDefinitionValidator.cs Assets/Core/Simulation/Run/RunDefinitionValidator.cs.meta \
  Assets/Core/Simulation/Run/RunNodeKey.cs Assets/Core/Simulation/Run/RunNodeKey.cs.meta \
  Assets/Core/Simulation/Run/RunNodeRegistry.cs Assets/Core/Simulation/Run/RunNodeRegistry.cs.meta \
  Assets/Core/Simulation/Run/IRunNodeHandler.cs Assets/Core/Simulation/Run/IRunNodeHandler.cs.meta \
  Assets/Core/Simulation/Run/IRunNodePayload.cs Assets/Core/Simulation/Run/IRunNodePayload.cs.meta \
  Assets/Core/Tests/EditMode/RunNodeKeyTests.cs Assets/Core/Tests/EditMode/RunNodeKeyTests.cs.meta \
  Assets/Core/Tests/EditMode/RunNodeRegistryTests.cs Assets/Core/Tests/EditMode/RunNodeRegistryTests.cs.meta
```

`.meta`가 없어 `git rm`이 실패하면 그 `.meta` 인자만 빼고 다시 실행한다.

- [ ] **Step 4: 전체 헤드리스로 확인한다**

Run: `Tools/verify.sh --quick`
Expected: `모두 통과`. 참조가 남아 빌드가 깨지면 `grep -rn "RunNode\|RunDefinition\|NextCombatSeed" Assets`로 찾아 이 태스크 안에서 처리한다(2026-09-15 기준으로는 없어야 한다).

- [ ] **Step 5: 커밋한다**

```bash
git add Assets/Core/Simulation/Run/RunState.cs Assets/Core/Tests/EditMode/RunStateTests.cs
git commit -m "refactor(core): 런 상태에서 노드 목록을 떼고 노드 순번만 남긴다"
```

---

### Task 3: 캐릭터 풀 연결 (`pool` 키)

**Files:**
- Modify: `Assets/Core/Authoring/Characters/CharacterSpec.cs`
- Modify: `Assets/Core/Authoring/Characters/CharacterContentCatalog.cs` (`CharacterContent`)
- Modify: `Assets/Core/Authoring/Characters/CharacterContentLoader.cs`
- Modify: `Assets/Core/Authoring/ContentBootstrap.cs:76-78`
- Modify: `Assets/StreamingAssets/Content/Characters/member_a.json`, `member_b.json`
- Modify: `Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs` (캐릭터 절 6개 호출)
- Modify: `Assets/Core/Tests/EditMode/DeckPoolCharacterContentTests.cs` (`Characters()` 헬퍼 + 테스트 1개 추가)

**Interfaces:**
- Produces: `CharacterContent.Pool` (string, 로드 시 존재 확인된 풀 id)
- Produces: `CharacterContentLoader.Load(IEnumerable<CardContentSource> sources, DeckContentCatalog decks, PoolContentCatalog pools)`

**임시 데이터(설계 결정 3a):** 두 캐릭터 모두 `"pool": "starter"`. "카드는 정확히 한 풀에만" 검증은 켜지 않는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`DeckPoolCharacterLoaderTests.cs`에 `Decks` 헬퍼 바로 아래에 풀 헬퍼를 더한다:

```csharp
        private static PoolContentCatalog Pools(params CardContentSource[] sources)
        {
            var result = PoolContentLoader.Load(sources, Cards("hasten", "breather"));
            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }

        private static PoolContentCatalog StarterPool()
            => Pools(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }"));
```

캐릭터 절(`// --- 캐릭터 ---` 아래)의 **모든** `CharacterContentLoader.Load(...)` 호출 6개를 고친다:
① 각 캐릭터 JSON 문자열의 `\"deck\": \"...\"` 뒤에 `, \"pool\": \"starter\"`를 넣는다.
② 마지막 인자 뒤에 `, StarterPool()`을 더한다. 예 — `CharacterLoaderReadsIdNameAndDeck`:

```csharp
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source(
                        "member_a.json",
                        "{ \"id\": \"member_a\", \"displayName\": \"파티원 A\", \"deck\": \"starter\", \"pool\": \"starter\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")),
                StarterPool());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            var member = result.Catalog.Get("member_a");
            Assert.AreEqual("member_a", member.Id);
            Assert.AreEqual("파티원 A", member.DisplayName);
            Assert.AreEqual("starter", member.Deck);
            Assert.AreEqual("starter", member.Pool);
```

같은 절 끝(`// --- 공통 ---` 위)에 테스트 둘을 더한다:

```csharp
        [Test]
        public void CharacterLoaderRejectsAnUnknownPoolId()
        {
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source(
                        "member_a.json",
                        "{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\", \"pool\": \"ghost_pool\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")),
                StarterPool());

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "member_a.json: unknown pool id 'ghost_pool'.");
        }

        [Test]
        public void CharacterLoaderRequiresAPoolKey()
        {
            var result = CharacterContentLoader.Load(
                new[]
                {
                    Source(
                        "member_a.json",
                        "{ \"id\": \"member_a\", \"displayName\": \"A\", \"deck\": \"starter\" }")
                },
                Decks(Source("starter.json", "{ \"id\": \"starter\", \"cards\": [\"hasten\"] }")),
                StarterPool());

            Assert.IsFalse(result.Succeeded);
            CollectionAssert.Contains(result.Errors, "member_a.json: required key 'pool' is missing.");
        }
```

`DeckPoolCharacterContentTests.cs`의 `Characters()` 헬퍼를 고친다:

```csharp
        private static CharacterContentCatalog Characters()
        {
            var result = CharacterContentLoader.Load(
                CardContentFiles.ReadDirectory(Folder(CardContentFiles.CharactersFolderName)),
                Decks(),
                Pools());

            Assert.IsTrue(result.Succeeded, string.Join("\n", result.Errors));
            return result.Catalog;
        }
```

그리고 `CharacterJsonPointsAtTheRosterDecks` 아래에 추가:

```csharp
        /// <summary>임시 데이터(전투 노드 설계 결정 3a): 캐릭터가 아직 설계되지 않아 둘 다 starter
        /// 풀을 가리킨다. 캐릭터 설계 때 이 테스트를 실제 풀 id로 바꾼다.</summary>
        [Test]
        public void CharacterJsonPointsAtTheTemporaryStarterPool()
        {
            var characters = Characters();

            Assert.AreEqual(StarterPoolId, characters.Get(PartyPrototypeRoster.MemberAId).Pool);
            Assert.AreEqual(StarterPoolId, characters.Get(PartyPrototypeRoster.MemberBId).Pool);
        }
```

- [ ] **Step 2: 실패를 확인한다**

Run: `Tools/verify.sh --quick`
Expected: 빌드 실패 — `CharacterContentLoader.Load`에 인자 3개 오버로드가 없고 `CharacterContent.Pool`이 없다

- [ ] **Step 3: 구현한다**

`CharacterSpec.cs`의 `Deck` 필드 아래에:

```csharp
        /// <summary>이 캐릭터가 소유한 카드풀 id. 전투 보상 후보가 여기서 나온다(전투 노드 설계 결정 2·3).</summary>
        public string Pool;
```

`CharacterContentCatalog.cs`의 `CharacterContent`를 교체:

```csharp
    public sealed class CharacterContent
    {
        public CharacterContent(string id, string displayName, string deck, string pool)
        {
            Id = id;
            DisplayName = displayName;
            Deck = deck;
            Pool = pool;
        }

        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>시작 덱의 id. DeckContentCatalog가 이것을 푼다.</summary>
        public string Deck { get; }

        /// <summary>소유 카드풀의 id. PoolContentCatalog가 이것을 푼다. 보상 후보의 출처다.</summary>
        public string Pool { get; }
    }
```

`CharacterContentLoader.cs`:
- `RequiredKeys`를 `{ "id", "displayName", "deck", "pool" }`로.
- `Load` 시그니처에 `PoolContentCatalog pools` 세 번째 인자를 더한다.
- `if (!decks.Contains(spec.Deck)) { ... }` 블록 바로 아래에:

```csharp
                if (!pools.Contains(spec.Pool))
                {
                    errors.Add(source.Name + ": unknown pool id '" + spec.Pool + "'.");
                    rejected = true;
                }
```

- 카탈로그에 넣는 줄을 `new CharacterContent(spec.Id, spec.DisplayName, spec.Deck, spec.Pool)`로.
- 클래스 요약 주석의 "덱 카탈로그를 인자로 받으므로"를 "덱·풀 카탈로그를 인자로 받으므로"로.

`ContentBootstrap.cs` 캐릭터 로드 호출(76-78행)에 `pools.Catalog`를 더한다:

```csharp
            var characters = CharacterContentLoader.Load(
                Read(contentRoot, CardContentFiles.CharactersFolderName, errors),
                decks.Catalog,
                pools.Catalog);
```

`Assets/StreamingAssets/Content/Characters/member_a.json` 전체:

```json
{
  "id": "member_a",
  "displayName": "파티원 A",
  "deck": "starter",
  "pool": "starter"
}
```

`member_b.json` 전체:

```json
{
  "id": "member_b",
  "displayName": "파티원 B",
  "deck": "party_prototype",
  "pool": "starter"
}
```

- [ ] **Step 4: 통과를 확인한다**

Run: `Tools/verify.sh`
Expected: `모두 통과`. 노트북 테스트도 돈다 — 노트북은 캐릭터 JSON을 읽지 않으므로(2026-09-15 grep: `index.html`에 `Characters` 참조 없음) 영향이 없어야 한다.

- [ ] **Step 5: 커밋한다**

```bash
git add Assets/Core/Authoring/Characters Assets/Core/Authoring/ContentBootstrap.cs \
  Assets/StreamingAssets/Content/Characters/member_a.json Assets/StreamingAssets/Content/Characters/member_b.json \
  Assets/Core/Tests/EditMode/DeckPoolCharacterLoaderTests.cs Assets/Core/Tests/EditMode/DeckPoolCharacterContentTests.cs
git commit -m "feat(core): 캐릭터가 소유 카드풀을 가리키게 한다"
```

본문: 보상 후보가 생존 캐릭터의 풀에서 나오기 위한 연결이며, 캐릭터 미설계로 두 캐릭터가 임시로 starter 풀을 가리킨다는 점을 적는다.

---

### Task 4: 런 시작 헬퍼 `RunSetup` (`ContentLoadouts` 흡수)

**Files:**
- Create: `Assets/Core/Simulation/Run/RunSetup.cs`
- Delete: `Assets/Core/Simulation/ContentLoadouts.cs` (+ `.meta`)
- Delete: `Assets/Core/Tests/EditMode/ContentDrivenLoadoutTests.cs` (+ `.meta`)
- Create: `Assets/Core/Tests/EditMode/RunSetupTests.cs`

`ContentLoadouts`의 프로덕션 사용처는 `BattleScreenController.cs:95` 한 곳이며 Task 9에서 사라진다. 이 태스크가 끝나면 Unity 프로젝트는 컨트롤러 컴파일 오류 상태가 되지만 **헤드리스는 통과한다**(헤드리스는 `Assets/Unity`를 컴파일하지 않는다). Unity 컴파일은 Task 9에서 복구하고 그 전에는 Unity를 돌리지 않는다.

**Interfaces:**
- Consumes: `CharacterContent.Deck` (Task 3 이전부터 존재)
- Produces: `public static RunState RunSetup.NewRun(GameContent content, IReadOnlyList<string> characterIds, PartyTuning tuning, int runSeed)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/RunSetupTests.cs`:

```csharp
using System.Linq;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    /// <summary>콘텐츠에서 시작 파티를 조립하는 경로를 잠근다. ContentDrivenLoadoutTests를 대체한다.</summary>
    public class RunSetupTests
    {
        [Test]
        public void NewRun_builds_each_member_from_the_authored_deck()
        {
            var content = TestContent.Content();

            var run = RunSetup.NewRun(
                content, new[] { "member_a", "member_b" }, PartyTuning.Prototype, runSeed: 7);

            Assert.AreEqual(7, run.RunSeed);
            Assert.AreEqual(0, run.NodesEntered);
            Assert.AreEqual(2, run.Party.Count);
            var a = run.Party[0];
            Assert.AreEqual("member_a", a.Id);
            Assert.AreEqual("파티원 A", a.Name);
            Assert.AreEqual(PartyTuning.Prototype.DefaultMemberMaxHp, a.MaxHp);
            Assert.AreEqual(a.MaxHp, a.Hp);
            CollectionAssert.AreEqual(
                content.Decks.Get("starter").ToArray(),
                a.Cards.Select(card => card.Id).ToArray());
        }

        [Test]
        public void NewRun_shares_one_definition_per_card_id()
        {
            var run = RunSetup.NewRun(
                TestContent.Content(), new[] { "member_b" }, PartyTuning.Prototype, runSeed: 1);
            var attacks = run.Party[0].Cards.Where(card => card.Id == "fixture_attack").ToArray();

            Assert.AreEqual(2, attacks.Length, "party_prototype 덱은 fixture_attack을 둘 갖는다.");
            Assert.AreSame(attacks[0], attacks[1]);
        }

        [Test]
        public void NewRun_keeps_the_given_party_order()
        {
            var run = RunSetup.NewRun(
                TestContent.Content(), new[] { "member_b", "member_a" }, PartyTuning.Prototype, runSeed: 1);

            CollectionAssert.AreEqual(
                new[] { "member_b", "member_a" }, run.Party.Select(m => m.Id).ToArray());
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~RunSetupTests`
Expected: 빌드 실패 — `RunSetup`이 없다

- [ ] **Step 3: 구현하고 옛 경로를 지운다**

`Assets/Core/Simulation/Run/RunSetup.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Authoring;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>콘텐츠에서 새 런의 시작 상태를 조립한다. 같은 카드 id는 정의 객체 하나를 공유한다
    /// (카드 카탈로그가 id마다 하나를 든다).</summary>
    public static class RunSetup
    {
        public static RunState NewRun(
            GameContent content,
            IReadOnlyList<string> characterIds,
            PartyTuning tuning,
            int runSeed)
        {
            var party = new List<RunMember>();
            foreach (var id in characterIds)
            {
                var character = content.Characters.Get(id);
                party.Add(new RunMember(
                    character.Id,
                    character.DisplayName,
                    tuning.DefaultMemberMaxHp,
                    DeckCards(content, character.Deck)));
            }

            return new RunState(party, runSeed);
        }

        private static List<CardDefinition> DeckCards(GameContent content, string deckId)
        {
            var cards = new List<CardDefinition>();
            foreach (var cardId in content.Decks.Get(deckId))
            {
                cards.Add(content.Cards.Get(cardId));
            }

            return cards;
        }
    }
}
```

```bash
git rm Assets/Core/Simulation/ContentLoadouts.cs Assets/Core/Simulation/ContentLoadouts.cs.meta \
  Assets/Core/Tests/EditMode/ContentDrivenLoadoutTests.cs Assets/Core/Tests/EditMode/ContentDrivenLoadoutTests.cs.meta
```

- [ ] **Step 4: 통과를 확인한다**

Run: `Tools/verify.sh --quick`
Expected: `모두 통과`

- [ ] **Step 5: 커밋한다**

```bash
git add Assets/Core/Simulation/Run/RunSetup.cs Assets/Core/Tests/EditMode/RunSetupTests.cs
git commit -m "refactor(core): 콘텐츠에서 런 시작 파티를 조립하는 헬퍼로 로드아웃 조립을 옮긴다"
```

---

### Task 5: 편성 공급 — 적 id 분리와 `GoblinEncounterSource`

**Files:**
- Modify: `Assets/Core/Combat/Enemy.cs`
- Create: `Assets/Core/Simulation/Run/Encounters.cs` (`IEncounterSource`, `EncounterEnemy`, `EncounterSetup`)
- Create: `Assets/Core/Simulation/GoblinEncounterSource.cs`
- Test: `Assets/Core/Tests/EditMode/EncounterSourceTests.cs`

**Interfaces:**
- Produces: `Enemy.SpecId` (string), 생성자 `Enemy(string id, string specId, int hp)`. 기존 `Enemy(string id, int hp)`는 `SpecId = id`
- Produces: `public interface IEncounterSource { EncounterSetup Pick(Random encounterRng); }`
- Produces: `public sealed class EncounterEnemy { public EncounterEnemy(Enemy enemy, IEnemyTurnPolicy policy); Enemy Enemy; IEnemyTurnPolicy Policy; }`
- Produces: `public sealed class EncounterSetup { public EncounterSetup(IReadOnlyList<EncounterEnemy> enemies); IReadOnlyList<EncounterEnemy> Enemies; }`
- Produces: `public sealed class GoblinEncounterSource : IEncounterSource` (namespace `FateWeaver.Simulation`)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/EncounterSourceTests.cs`:

```csharp
using System;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class EncounterSourceTests
    {
        [Test]
        public void Enemy_keeps_combat_id_and_spec_id_apart()
        {
            var enemy = new Enemy("goblin#0", "goblin", 28);

            Assert.AreEqual("goblin#0", enemy.Id);
            Assert.AreEqual("goblin", enemy.SpecId);
            Assert.AreEqual(28, enemy.Hp);
        }

        [Test]
        public void Legacy_enemy_constructor_uses_the_id_as_spec_id()
        {
            Assert.AreEqual("goblin", new Enemy("goblin", 28).SpecId);
        }

        [Test]
        public void Goblin_source_returns_one_goblin_with_its_own_policy()
        {
            var setup = new GoblinEncounterSource().Pick(new Random(1));

            Assert.AreEqual(1, setup.Enemies.Count);
            var pair = setup.Enemies[0];
            Assert.AreEqual("goblin#0", pair.Enemy.Id);
            Assert.AreEqual(GoblinDeck.EnemyId, pair.Enemy.SpecId);
            Assert.AreEqual(GoblinDeck.StartingHp, pair.Enemy.Hp);
            Assert.IsInstanceOf<RandomPickPolicy>(pair.Policy);
        }

        [Test]
        public void Goblin_source_makes_fresh_instances_every_pick()
        {
            var source = new GoblinEncounterSource();

            var first = source.Pick(new Random(1)).Enemies[0];
            var second = source.Pick(new Random(1)).Enemies[0];

            Assert.AreNotSame(first.Enemy, second.Enemy);
            Assert.AreNotSame(first.Policy, second.Policy);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~EncounterSourceTests`
Expected: 빌드 실패 — `Enemy`에 인자 3개 생성자·`SpecId`, `GoblinEncounterSource`가 없다

- [ ] **Step 3: 구현한다**

`Assets/Core/Combat/Enemy.cs` 전체:

```csharp
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Combat
{
    public sealed class Enemy : IStatusHolder
    {
        /// <summary>전투 안 식별자. 대상 지정·사망 이벤트·카드 소유가 이것을 쓴다. 한 전투 안에서
        /// 유일하며, 편성에서 만들 때는 "{SpecId}#{편성 내 순번}"이다.</summary>
        public string Id { get; }

        /// <summary>적 정의 식별자. 이름 표시·저작 조회가 이것을 쓴다. 같은 적이 여럿 나와도 같다.</summary>
        public string SpecId { get; }

        public int Hp { get; set; }
        public StatusBag Statuses { get; } = new();

        public Enemy(string id, string specId, int hp)
        {
            Id = id;
            SpecId = specId;
            Hp = hp;
        }

        /// <summary>정의와 전투 안 식별자를 가르지 않는 기존 호출부용. SpecId = id.</summary>
        public Enemy(string id, int hp)
            : this(id, id, hp)
        {
        }
    }
}
```

(원래 파일의 `using` 줄이 위와 다르면 원래 것을 유지한다 — `IStatusHolder`·`StatusBag`이 해석되면 된다.)

`Assets/Core/Simulation/Run/Encounters.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Combat;

namespace FateWeaver.Simulation.Run
{
    /// <summary>편성 스트림을 받아 이번 전투의 적마다 (적, 정책) 쌍을 새로 만든다. 정책은 상태를
    /// 가지므로(ShuffleBagPolicy) 매 호출 새 인스턴스를 돌려줘야 한다.</summary>
    public interface IEncounterSource
    {
        EncounterSetup Pick(Random encounterRng);
    }

    /// <summary>적 하나와 그 적의 정책. 편성 전체에 정책 하나를 두면 "이 카드가 어느 적 것인가"를
    /// 말할 수단이 없어진다 — 지금 세션이 적 둘 이상에서 카드 주인을 비우는 원인이 그것이다
    /// (DeckCombatSession.BeginTurn). 세션이 쌍 목록을 받게 되는 것은 필수 후속 작업이다.</summary>
    public sealed class EncounterEnemy
    {
        public EncounterEnemy(Enemy enemy, IEnemyTurnPolicy policy)
        {
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public Enemy Enemy { get; }
        public IEnemyTurnPolicy Policy { get; }
    }

    public sealed class EncounterSetup
    {
        public EncounterSetup(IReadOnlyList<EncounterEnemy> enemies)
        {
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        }

        public IReadOnlyList<EncounterEnemy> Enemies { get; }
    }
}
```

`Assets/Core/Simulation/GoblinEncounterSource.cs`:

```csharp
using System;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Simulation
{
    /// <summary>1단계 편성 공급자: 항상 고블린 한 마리. 편성 스트림을 받지만 쓰지 않는다 — 스트림은
    /// 이름표로 파생되므로 안 써도 전투·보상 스트림 값이 변하지 않는다. 2단계에서 편성 JSON 공급자로
    /// 대체된다(전투 노드 설계 2.3).</summary>
    public sealed class GoblinEncounterSource : IEncounterSource
    {
        public EncounterSetup Pick(Random encounterRng)
            => new EncounterSetup(new[]
            {
                new EncounterEnemy(
                    new Enemy(GoblinDeck.EnemyId + "#0", GoblinDeck.EnemyId, GoblinDeck.StartingHp),
                    GoblinDeck.Policy())
            });
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

Run: `Tools/verify.sh --quick`
Expected: `모두 통과`

- [ ] **Step 5: 커밋한다**

```bash
git add Assets/Core/Combat/Enemy.cs Assets/Core/Simulation/Run/Encounters.cs \
  Assets/Core/Simulation/GoblinEncounterSource.cs Assets/Core/Tests/EditMode/EncounterSourceTests.cs
git commit -m "feat(core): 적의 전투 안 식별자와 정의 식별자를 가르고 편성 공급자를 둔다"
```

---

### Task 6: 보상 후보 공급 — `CharacterPoolRewardSource`

**Files:**
- Create: `Assets/Core/Simulation/Run/Rewards.cs` (`RewardCandidate`, `RewardOffer`, `IRewardCandidateSource`)
- Create: `Assets/Core/Simulation/Run/CharacterPoolRewardSource.cs`
- Test: `Assets/Core/Tests/EditMode/CharacterPoolRewardSourceTests.cs`

**Interfaces:**
- Consumes: `CharacterContent.Pool` (Task 3)
- Produces: `public sealed class RewardCandidate { public RewardCandidate(CardDefinition card, string ownerId); CardDefinition Card; string OwnerId; }`
- Produces: `public sealed class RewardOffer { public RewardOffer(IReadOnlyList<RewardCandidate> candidates); IReadOnlyList<RewardCandidate> Candidates; }`
- Produces: `public interface IRewardCandidateSource { IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds); }`
- Produces: `public sealed class CharacterPoolRewardSource : IRewardCandidateSource { public CharacterPoolRewardSource(GameContent content); }`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Core/Tests/EditMode/CharacterPoolRewardSourceTests.cs`:

```csharp
using System;
using System.Linq;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CharacterPoolRewardSourceTests
    {
        [Test]
        public void Eligible_lists_the_living_characters_pool_owned_by_that_character()
        {
            var content = TestContent.Content();
            var source = new CharacterPoolRewardSource(content);

            var pairs = source.Eligible(new[] { "member_a" });

            var poolIds = content.Pools.Get(content.Characters.Get("member_a").Pool)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(poolIds, pairs.Select(p => p.Card.Id).ToArray());
            Assert.IsTrue(pairs.All(p => p.OwnerId == "member_a"));
        }

        [Test]
        public void Eligible_follows_the_given_character_order()
        {
            var content = TestContent.Content();
            var source = new CharacterPoolRewardSource(content);
            var poolSize = content.Pools.Get("starter").Count;

            var pairs = source.Eligible(new[] { "member_b", "member_a" });

            Assert.AreEqual(poolSize * 2, pairs.Count, "임시 데이터: 두 캐릭터가 같은 starter 풀이다.");
            Assert.IsTrue(pairs.Take(poolSize).All(p => p.OwnerId == "member_b"));
            Assert.IsTrue(pairs.Skip(poolSize).All(p => p.OwnerId == "member_a"));
        }

        [Test]
        public void Eligible_is_empty_without_living_characters()
        {
            var source = new CharacterPoolRewardSource(TestContent.Content());

            Assert.AreEqual(0, source.Eligible(Array.Empty<string>()).Count);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~CharacterPoolRewardSourceTests`
Expected: 빌드 실패 — `CharacterPoolRewardSource`가 없다

- [ ] **Step 3: 구현한다**

`Assets/Core/Simulation/Run/Rewards.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>보상 후보 하나. 소유자는 카드가 속한 풀의 주인이며, 뽑은 뒤에 붙이는 값이 아니다
    /// (전투 노드 설계 결정 3).</summary>
    public sealed class RewardCandidate
    {
        public RewardCandidate(CardDefinition card, string ownerId)
        {
            Card = card ?? throw new ArgumentNullException(nameof(card));
            OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
        }

        public CardDefinition Card { get; }
        public string OwnerId { get; }
    }

    public sealed class RewardOffer
    {
        public RewardOffer(IReadOnlyList<RewardCandidate> candidates)
        {
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        }

        public IReadOnlyList<RewardCandidate> Candidates { get; }
    }

    /// <summary>살아남은 캐릭터 각자의 풀에서 뽑을 수 있는 (카드, 소유자) 쌍 전체를 돌려준다.
    /// 순서: 인자로 받은 캐릭터 순서 → 풀 안에서는 카드 id 서수 정렬. 이 순서는 보상 추첨의 입력이라
    /// 사실상 저장 형식처럼 굳는다. 한 카드가 여러 풀에 있으면 쌍이 여러 개 나온다(임시 데이터 기간).</summary>
    public interface IRewardCandidateSource
    {
        IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds);
    }
}
```

`Assets/Core/Simulation/Run/CharacterPoolRewardSource.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring;

namespace FateWeaver.Simulation.Run
{
    /// <summary>캐릭터 JSON의 pool이 가리키는 풀에서 보상 후보를 만든다. 임시인 것은 코드가 아니라
    /// 데이터다 — 캐릭터를 설계하면 pool 값만 바뀐다(전투 노드 설계 결정 3a).</summary>
    public sealed class CharacterPoolRewardSource : IRewardCandidateSource
    {
        private readonly GameContent _content;

        public CharacterPoolRewardSource(GameContent content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds)
        {
            var pairs = new List<RewardCandidate>();
            foreach (var characterId in livingCharacterIds)
            {
                var cardIds = new List<string>(
                    _content.Pools.Get(_content.Characters.Get(characterId).Pool));
                cardIds.Sort(StringComparer.Ordinal);
                foreach (var cardId in cardIds)
                {
                    pairs.Add(new RewardCandidate(_content.Cards.Get(cardId), characterId));
                }
            }

            return pairs;
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

Run: `Tools/verify.sh --quick`
Expected: `모두 통과`

- [ ] **Step 5: 커밋한다**

```bash
git add Assets/Core/Simulation/Run/Rewards.cs Assets/Core/Simulation/Run/CharacterPoolRewardSource.cs \
  Assets/Core/Tests/EditMode/CharacterPoolRewardSourceTests.cs
git commit -m "feat(core): 생존 캐릭터의 풀에서 보상 후보 쌍을 만든다"
```

---

### Task 7: 전투 노드 `CombatNode`

**Files:**
- Create: `Assets/Core/Simulation/Run/CombatNode.cs` (`CombatNodePhase`, `CombatNodeContext`, `CombatNode`)
- Test: `Assets/Core/Tests/EditMode/CombatNodeTests.cs`

**Interfaces:**
- Consumes: `SeedDerivation`(T1), `RunState`(T2), `IEncounterSource`·`EncounterSetup`·`EncounterEnemy`(T5), `IRewardCandidateSource`·`RewardCandidate`·`RewardOffer`(T6), `DeckCombatSession`(기존 파티 생성자)
- Produces: `public enum CombatNodePhase { Combat, Reward, Defeated, Done }`
- Produces: `public sealed class CombatNodeContext { public CombatNodeContext(StatusContentCatalog statuses, PartyTuning partyTuning, int fateEnergyPerTurn, int rewardChoices, IEncounterSource encounters, IRewardCandidateSource rewardCandidates); }` — 같은 이름의 get 전용 속성
- Produces: `public sealed class CombatNode { static CombatNode Begin(RunState run, CombatNodeContext context); int NodeIndex; int NodeSeed; DeckCombatSession Session; CombatNodePhase Phase; RewardOffer Offer; void Conclude(); void Choose(int index); void Skip(); }`

- [ ] **Step 1: 테스트 뼈대와 패배·예외 테스트를 쓴다**

`Assets/Core/Tests/EditMode/CombatNodeTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using NUnit.Framework;

namespace FateWeaver.Tests
{
    public class CombatNodeTests
    {
        // --- 합성 재료 -------------------------------------------------------

        private static CardDefinition Hit() => CardFixtures.Damage("hit", 5, cost: 0);

        private static RunMember Member(string id, int hitCount)
            => new RunMember(id, id, 20, Enumerable.Range(0, hitCount).Select(_ => Hit()));

        private static PartyTuning Tuning() => new PartyTuning
        {
            MinPartySize = 1,
            MaxPartySize = 3,
            DefaultMemberMaxHp = 20,
            SurviveChargesPerCombat = 0,
            DrawByLivingCount = new Dictionary<int, int> { { 1, 3 }, { 2, 4 }, { 3, 5 } }
        };

        private sealed class FixedEncounter : IEncounterSource
        {
            private readonly Func<EncounterSetup> _make;
            public FixedEncounter(Func<EncounterSetup> make) => _make = make;
            public EncounterSetup Pick(Random encounterRng) => _make();
        }

        /// <summary>HP 5짜리 적. turnCards가 비면 아무것도 하지 않는다.</summary>
        private static IEncounterSource Dummy(int hp = 5, params CardDefinition[] turnCards)
            => new FixedEncounter(() => new EncounterSetup(new[]
            {
                new EncounterEnemy(
                    new Enemy("dummy#0", "dummy", hp),
                    new SequencePolicy(new[] { new EnemyCardBundle(turnCards) }))
            }));

        private static CardDefinition Smash() => CardFixtures.EnemyAttack("smash", 1, 999);

        /// <summary>캐릭터 id → 풀 카드 id. 카드 정의는 id마다 하나를 공유한다.</summary>
        private sealed class FakePools : IRewardCandidateSource
        {
            private readonly Dictionary<string, string[]> _pools;
            private readonly Dictionary<string, CardDefinition> _cards = new Dictionary<string, CardDefinition>();

            public FakePools(Dictionary<string, string[]> pools) => _pools = pools;

            public IReadOnlyList<RewardCandidate> Eligible(IReadOnlyList<string> livingCharacterIds)
            {
                var pairs = new List<RewardCandidate>();
                foreach (var characterId in livingCharacterIds)
                {
                    foreach (var cardId in _pools[characterId].OrderBy(id => id, StringComparer.Ordinal))
                    {
                        if (!_cards.TryGetValue(cardId, out var card))
                        {
                            card = CardFixtures.Damage(cardId, 1, cost: 0);
                            _cards.Add(cardId, card);
                        }

                        pairs.Add(new RewardCandidate(card, characterId));
                    }
                }

                return pairs;
            }
        }

        private static FakePools SixEach() => new FakePools(new Dictionary<string, string[]>
        {
            { "a", new[] { "a1", "a2", "a3", "a4", "a5", "a6" } },
            { "b", new[] { "b1", "b2", "b3", "b4", "b5", "b6" } }
        });

        private static CombatNodeContext Context(
            IEncounterSource encounters, IRewardCandidateSource rewards, int rewardChoices = 3)
            => new CombatNodeContext(
                TestContent.Statuses(), Tuning(), fateEnergyPerTurn: 3, rewardChoices: rewardChoices,
                encounters: encounters, rewardCandidates: rewards);

        /// <summary>turn번째 턴(0부터)에 hit 한 장을 내고 해석해 이긴다. 그 전 턴은 아무것도 내지 않는다.</summary>
        private static void WinOnTurn(CombatNode node, int turn)
        {
            for (int t = 0; t < turn; t++)
            {
                node.Session.ResolveTurn();
                Assert.IsTrue(node.Session.BeginNextTurn(), "전제: 전투가 아직 끝나지 않았어야 한다.");
            }

            Assert.IsTrue(node.Session.PlayExecutionCard(0), "전제: 손패 첫 장이 hit이어야 한다.");
            node.Session.ResolveTurn();
            Assert.AreEqual(Outcome.Win, node.Session.Outcome, "전제: 이번 턴에 이겨야 한다.");
        }

        private static string[] Describe(RewardOffer offer)
            => offer.Candidates.Select(c => c.Card.Id + "@" + c.OwnerId).ToArray();

        // --- 시작 ------------------------------------------------------------

        [Test]
        public void Begin_enters_a_node_and_seeds_the_session_from_the_combat_stream()
        {
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);

            var node = CombatNode.Begin(run, Context(Dummy(), SixEach()));

            Assert.AreEqual(0, node.NodeIndex);
            Assert.AreEqual(1, run.NodesEntered);
            Assert.AreEqual(SeedDerivation.NodeSeed(11, 0), node.NodeSeed);
            Assert.AreEqual(
                SeedDerivation.Stream(node.NodeSeed, SeedStream.Combat),
                node.Session.State.RngSeed);
            Assert.AreEqual(CombatNodePhase.Combat, node.Phase);
            Assert.IsNull(node.Offer);
        }

        [Test]
        public void Begin_rejects_more_than_one_enemy()
        {
            var twoEnemies = new FixedEncounter(() => new EncounterSetup(new[]
            {
                new EncounterEnemy(new Enemy("dummy#0", "dummy", 5), new SequencePolicy(Array.Empty<EnemyCardBundle>())),
                new EncounterEnemy(new Enemy("dummy#1", "dummy", 5), new SequencePolicy(Array.Empty<EnemyCardBundle>()))
            }));
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);

            Assert.Throws<InvalidOperationException>(() => CombatNode.Begin(run, Context(twoEnemies, SixEach())));
        }

        // --- 패배 ------------------------------------------------------------

        [Test]
        public void Defeat_ends_the_run_without_an_offer()
        {
            var run = new RunState(new[] { Member("a", 6) }, runSeed: 11);
            var node = CombatNode.Begin(run, Context(Dummy(99, Smash()), SixEach()));

            node.Session.ResolveTurn();
            Assert.AreEqual(Outcome.Lose, node.Session.Outcome, "전제: 999 피해로 한 턴에 져야 한다.");
            node.Conclude();

            Assert.AreEqual(CombatNodePhase.Defeated, node.Phase);
            Assert.IsNull(node.Offer);
            Assert.AreEqual(RunOutcome.Defeat, run.Outcome);
            Assert.Throws<InvalidOperationException>(() => CombatNode.Begin(run, Context(Dummy(), SixEach())));
        }

        // --- 잘못된 호출 -----------------------------------------------------

        [Test]
        public void Conclude_before_the_combat_ends_throws()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6) }, 11), Context(Dummy(), SixEach()));

            Assert.Throws<InvalidOperationException>(node.Conclude);
        }

        [Test]
        public void Choose_and_skip_outside_the_reward_phase_throw()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6) }, 11), Context(Dummy(), SixEach()));

            Assert.Throws<InvalidOperationException>(() => node.Choose(0));
            Assert.Throws<InvalidOperationException>(node.Skip);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~CombatNodeTests`
Expected: 빌드 실패 — `CombatNode`·`CombatNodeContext`·`CombatNodePhase`가 없다

- [ ] **Step 3: 구현한다**

`Assets/Core/Simulation/Run/CombatNode.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation.Run
{
    public enum CombatNodePhase
    {
        Combat,
        Reward,
        Defeated,
        Done
    }

    /// <summary>전투 노드가 쓰는 규칙 값과 공급자. 1단계의 값 셋(PartyTuning·운명력·보상 장수)은
    /// 2단계에서 combat_rules.json 하나로 바뀐다.</summary>
    public sealed class CombatNodeContext
    {
        public CombatNodeContext(
            StatusContentCatalog statuses,
            PartyTuning partyTuning,
            int fateEnergyPerTurn,
            int rewardChoices,
            IEncounterSource encounters,
            IRewardCandidateSource rewardCandidates)
        {
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
            PartyTuning = partyTuning ?? throw new ArgumentNullException(nameof(partyTuning));
            FateEnergyPerTurn = fateEnergyPerTurn;
            RewardChoices = rewardChoices;
            Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            RewardCandidates = rewardCandidates ?? throw new ArgumentNullException(nameof(rewardCandidates));
        }

        public StatusContentCatalog Statuses { get; }
        public PartyTuning PartyTuning { get; }
        public int FateEnergyPerTurn { get; }
        public int RewardChoices { get; }
        public IEncounterSource Encounters { get; }
        public IRewardCandidateSource RewardCandidates { get; }
    }

    /// <summary>전투 한 판의 단계(전투·보상·패배·완료)를 전이시키고 결과를 런에 반영한다.
    /// 화면·콘텐츠 파일·적의 출처를 모른다. 무작위는 노드 시드에서 목적별로 파생한 스트림만 쓴다
    /// (전투 노드 설계 1.2·1.3).</summary>
    public sealed class CombatNode
    {
        private readonly RunState _run;
        private readonly CombatNodeContext _context;

        private CombatNode(
            RunState run, CombatNodeContext context, int nodeIndex, int nodeSeed, DeckCombatSession session)
        {
            _run = run;
            _context = context;
            NodeIndex = nodeIndex;
            NodeSeed = nodeSeed;
            Session = session;
        }

        public int NodeIndex { get; }
        public int NodeSeed { get; }
        public DeckCombatSession Session { get; }
        public CombatNodePhase Phase { get; private set; } = CombatNodePhase.Combat;

        /// <summary>승리 후(Reward·Done)에만 있다.</summary>
        public RewardOffer Offer { get; private set; }

        public static CombatNode Begin(RunState run, CombatNodeContext context)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (run.Outcome != RunOutcome.InProgress)
            {
                throw new InvalidOperationException("Cannot begin a combat node on a finished run.");
            }

            var setup = context.Encounters.Pick(
                new Random(SeedDerivation.Stream(SeedDerivation.NodeSeed(run.RunSeed, run.NodesEntered), SeedStream.Encounter)));
            if (setup == null || setup.Enemies.Count != 1)
            {
                // 세션이 정책 하나만 받는다. 다중 적은 세션이 적마다 정책을 받게 된 뒤 지원한다(필수 후속 작업).
                throw new InvalidOperationException(
                    "The combat session supports exactly one enemy until per-enemy policies land.");
            }

            var nodeIndex = run.EnterNode();
            var nodeSeed = SeedDerivation.NodeSeed(run.RunSeed, nodeIndex);
            var loadouts = run.LivingMembers
                .Select(member => new PartyMemberLoadout(member.Id, member.Name, member.MaxHp, member.Cards.ToList()))
                .ToList();
            var session = new DeckCombatSession(
                context.Statuses,
                loadouts,
                new[] { setup.Enemies[0].Enemy },
                setup.Enemies[0].Policy,
                context.PartyTuning,
                partyCards: null,
                fateEnergyPerTurn: context.FateEnergyPerTurn,
                seed: SeedDerivation.Stream(nodeSeed, SeedStream.Combat));

            return new CombatNode(run, context, nodeIndex, nodeSeed, session);
        }

        public void Conclude()
        {
            if (Phase != CombatNodePhase.Combat || !Session.IsComplete)
            {
                throw new InvalidOperationException("Conclude requires a finished combat in the combat phase.");
            }

            if (Session.Outcome == Outcome.Lose)
            {
                _run.SetOutcome(RunOutcome.Defeat);
                Phase = CombatNodePhase.Defeated;
                return;
            }

            Offer = BuildOffer();
            Phase = CombatNodePhase.Reward;
        }

        public void Choose(int index)
        {
            RequireRewardPhase();
            if (index < 0 || index >= Offer.Candidates.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var candidate = Offer.Candidates[index];
            _run.Party.First(member => member.Id == candidate.OwnerId).Cards.Add(candidate.Card);
            Phase = CombatNodePhase.Done;
        }

        public void Skip()
        {
            RequireRewardPhase();
            Phase = CombatNodePhase.Done;
        }

        private void RequireRewardPhase()
        {
            if (Phase != CombatNodePhase.Reward)
            {
                throw new InvalidOperationException("Rewards can only be chosen in the reward phase.");
            }
        }

        /// <summary>생존 캐릭터 풀의 (카드, 소유자) 쌍에서 카드가 겹치지 않게 뽑는다. 소유자는 쌍에 이미
        /// 있으므로 따로 뽑지 않는다. 뽑기 한 번 = RNG 한 번이고, 뽑힌 카드의 다른 소유자 쌍은 지운다 —
        /// 카드마다 풀이 하나뿐인 진짜 데이터에서는 뽑힌 쌍 하나만 지워진다.</summary>
        private RewardOffer BuildOffer()
        {
            var rng = new Random(SeedDerivation.Stream(NodeSeed, SeedStream.Reward));
            var living = Session.State.Party.Where(member => member.IsAlive).Select(member => member.Id).ToList();
            var pairs = new List<RewardCandidate>(_context.RewardCandidates.Eligible(living));
            var distinct = pairs.Select(pair => pair.Card.Id).Distinct().Count();
            var count = Math.Min(_context.RewardChoices, distinct);

            var picked = new List<RewardCandidate>();
            for (int i = 0; i < count; i++)
            {
                var pick = pairs[rng.Next(pairs.Count)];
                picked.Add(pick);
                pairs.RemoveAll(pair => pair.Card.Id == pick.Card.Id);
            }

            return new RewardOffer(picked);
        }
    }
}
```

**`Begin`의 순서:** 편성을 뽑고 적 수를 검사한 **뒤에** `EnterNode`를 부른다. 편성이 잘못돼 예외가 날 때 노드 순번이 헛되이 늘지 않게 하려는 것이며, 편성 스트림은 `NodeSeed(run.RunSeed, run.NodesEntered)` — 곧 들어갈 순번 — 에서 파생되므로 시드 결과는 같다. 설계 상세 1.3도 이 순서로 적혀 있다(2026-09-15 계획 작성 때 맞춤).

- [ ] **Step 4: 통과를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter FullyQualifiedName~CombatNodeTests`
Expected: `Passed: 5`. `Defeat_...`의 전제 단언이 실패하면(적 공격이 파티원에 닿지 않음) 구현을 고치지 말고 `CardFixtures.EnemyAttack`의 기본 대상(`TargetSelector.FrontOne`)과 파티 대형을 확인한 뒤 멈추고 보고한다.

- [ ] **Step 5: 보상 테스트를 더한다**

`CombatNodeTests.cs`의 `// --- 잘못된 호출 ---` 절 **위에** 추가:

```csharp
        // --- 보상 ------------------------------------------------------------

        [Test]
        public void Same_node_and_survivors_give_the_same_offer_however_the_fight_went()
        {
            var fast = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(fast, turn: 0);
            fast.Conclude();

            var slow = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(slow, turn: 2);
            slow.Conclude();

            Assert.AreEqual(CombatNodePhase.Reward, fast.Phase);
            CollectionAssert.AreEqual(Describe(fast.Offer), Describe(slow.Offer));
        }

        [Test]
        public void A_different_node_index_gives_a_different_offer()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var context = Context(Dummy(), SixEach());

            var first = CombatNode.Begin(run, context);
            WinOnTurn(first, 0);
            first.Conclude();
            first.Skip();

            var second = CombatNode.Begin(run, context);
            WinOnTurn(second, 0);
            second.Conclude();

            Assert.AreEqual(1, second.NodeIndex);
            // 우연히 같을 확률은 12·10·8분의 1 수준이다. 같게 나오면 알고리즘이 아니라 시드 우연이므로
            // 런 시드를 12로 바꾸고 그 사실을 이 주석에 적는다.
            CollectionAssert.AreNotEqual(Describe(first.Offer), Describe(second.Offer));
        }

        [Test]
        public void The_offer_matches_a_hand_rolled_reward_stream()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();

            var rng = new Random(SeedDerivation.Stream(node.NodeSeed, SeedStream.Reward));
            var pairs = SixEach().Eligible(new[] { "a", "b" }).ToList();
            var expected = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var pick = pairs[rng.Next(pairs.Count)];
                expected.Add(pick.Card.Id + "@" + pick.OwnerId);
                pairs.RemoveAll(pair => pair.Card.Id == pick.Card.Id);
            }

            CollectionAssert.AreEqual(expected, Describe(node.Offer));
        }

        [Test]
        public void Dead_characters_are_never_owners()
        {
            // a는 카드가 없고 앞줄이라 적의 999 피해에 먼저 죽는다. b의 hit이 적을 잡아 이긴다.
            var run = new RunState(new[] { Member("a", 0), Member("b", 6) }, 11);
            var node = CombatNode.Begin(run, Context(Dummy(5, Smash()), SixEach()));
            WinOnTurn(node, 0);
            Assert.IsFalse(node.Session.State.Party.First(m => m.Id == "a").IsAlive, "전제: a가 죽어야 한다.");

            node.Conclude();

            Assert.AreEqual(3, node.Offer.Candidates.Count);
            Assert.IsTrue(node.Offer.Candidates.All(c => c.OwnerId == "b"));
            Assert.IsTrue(node.Offer.Candidates.All(c => c.Card.Id.StartsWith("b")));
        }

        [Test]
        public void Overlapping_pools_still_offer_distinct_cards()
        {
            var shared = new FakePools(new Dictionary<string, string[]>
            {
                { "a", new[] { "c1", "c2", "c3" } },
                { "b", new[] { "c1", "c2", "c3" } }
            });
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), shared));
            WinOnTurn(node, 0);
            node.Conclude();

            CollectionAssert.AreEquivalent(
                new[] { "c1", "c2", "c3" }, node.Offer.Candidates.Select(c => c.Card.Id).ToArray());
        }

        [Test]
        public void Fewer_distinct_cards_than_choices_offers_what_exists()
        {
            var tiny = new FakePools(new Dictionary<string, string[]>
            {
                { "a", new[] { "only_a" } },
                { "b", new[] { "only_b" } }
            });
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6), Member("b", 6) }, 11), Context(Dummy(), tiny));
            WinOnTurn(node, 0);
            node.Conclude();

            Assert.AreEqual(2, node.Offer.Candidates.Count);
        }

        [Test]
        public void Choose_adds_the_card_to_its_owner_only()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var node = CombatNode.Begin(run, Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();
            var chosen = node.Offer.Candidates[1];
            var other = chosen.OwnerId == "a" ? "b" : "a";

            node.Choose(1);

            Assert.AreEqual(CombatNodePhase.Done, node.Phase);
            Assert.AreEqual(7, run.Party.First(m => m.Id == chosen.OwnerId).Cards.Count);
            Assert.AreSame(chosen.Card, run.Party.First(m => m.Id == chosen.OwnerId).Cards.Last());
            Assert.AreEqual(6, run.Party.First(m => m.Id == other).Cards.Count);
            Assert.Throws<InvalidOperationException>(() => node.Choose(0));
        }

        [Test]
        public void Choose_rejects_an_out_of_range_index()
        {
            var node = CombatNode.Begin(new RunState(new[] { Member("a", 6) }, 11), Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();

            Assert.Throws<ArgumentOutOfRangeException>(() => node.Choose(3));
            Assert.AreEqual(CombatNodePhase.Reward, node.Phase);
        }

        [Test]
        public void Skip_leaves_every_deck_unchanged()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var node = CombatNode.Begin(run, Context(Dummy(), SixEach()));
            WinOnTurn(node, 0);
            node.Conclude();

            node.Skip();

            Assert.AreEqual(CombatNodePhase.Done, node.Phase);
            Assert.IsTrue(run.Party.All(m => m.Cards.Count == 6));
        }

        [Test]
        public void The_next_node_starts_with_the_chosen_card_in_the_deck()
        {
            var run = new RunState(new[] { Member("a", 6), Member("b", 6) }, 11);
            var context = Context(Dummy(), SixEach());
            var first = CombatNode.Begin(run, context);
            WinOnTurn(first, 0);
            first.Conclude();
            var chosenId = first.Offer.Candidates[0].Card.Id;
            first.Choose(0);

            var next = CombatNode.Begin(run, context);

            Assert.AreEqual(1, next.NodeIndex);
            Assert.AreEqual(13, next.Session.AllDeckCards.Count);
            Assert.AreEqual(1, next.Session.AllDeckCards.Count(card => card.Def.Id == chosenId));
        }
```

- [ ] **Step 6: 통과를 확인한다**

Run: `Tools/verify.sh --quick`
Expected: `모두 통과`, `CombatNodeTests` 15개 포함. `Dead_characters_are_never_owners`의 전제가 깨지면(a가 안 죽음) 멈추고 보고한다 — 대형·대상 규칙 확인이 먼저다.

- [ ] **Step 7: 커밋한다**

```bash
git add Assets/Core/Simulation/Run/CombatNode.cs Assets/Core/Tests/EditMode/CombatNodeTests.cs
git commit -m "feat(core): 전투 한 판의 승패와 생존 캐릭터 풀 기반 보상을 다루는 전투 노드를 만든다"
```

본문에 보상 추첨이 소유자를 따로 뽑지 않고 쌍을 뽑는 이유(설계 결정 3·11)를 적는다.

---

### Task 8: Unity 뷰 둘 — `RewardChoiceView`·`CombatResultView`

**Files:**
- Modify: `Assets/Unity/Scripts/Battle/BattleUiKit.cs` (`LabeledButton` 추가)
- Create: `Assets/Unity/Scripts/Battle/RewardChoiceView.cs`
- Create: `Assets/Unity/Scripts/Battle/CombatResultView.cs`
- Test: `Assets/Tests/UnityEditMode/CombatNodeViewsTests.cs`

프리팹 에셋은 Task 9의 씬 빌더가 `EditorCreate`로 저장한다(기존 `FloatingNumberView` 관례). 이 태스크의 테스트는 `EditorCreate`로 만든 객체를 직접 검사한다.

**Interfaces:**
- Consumes: `RewardCandidate`(T6), `BattlePresenter.For(OwnedCard)`(`BattlePresenter.cs:32`), `CardPrefabCatalog.Create(CardPresentation, RectTransform)`(`CardPrefabCatalog.cs:37`)
- Produces: `RewardChoiceView.SlotCount = 3`, `bool IsBound`, `void Show(IReadOnlyList<RewardCandidate> candidates, Func<string, string> ownerName, Action<int> onChoose, Action onSkip)`, `void ShowNextButton(Action onNext)`, `void Hide()`, `static RewardChoiceView EditorCreate(RectTransform parent)`, `void EditorBind(CardPrefabCatalog cards, BattlePresenter presenter)`
- Produces: `CombatResultView`: `bool IsBound`, `void ShowDefeat(Action onRestart)`, `void Hide()`, `static CombatResultView EditorCreate(RectTransform parent)`
- Produces: `BattleUiKit.LabeledButton(RectTransform parent, string name, string label, float fontSize)`

**Unity 컴파일 상태:** Task 4에서 `ContentLoadouts`가 지워져 `BattleScreenController`가 컴파일되지 않는다. **이 태스크에서는 Unity를 돌리지 않고** 코드만 쓴다. 테스트 실행과 `.meta` 생성은 Task 9 끝의 배치 실행에서 한다. 이 태스크의 커밋은 코드와 테스트뿐이다.

- [ ] **Step 1: 테스트를 쓴다**

`Assets/Tests/UnityEditMode/CombatNodeViewsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Simulation.Run;
using FateWeaver.Unity;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CombatNodeViewsTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        private RectTransform Canvas()
        {
            _root = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            return (RectTransform)_root.transform;
        }

        private RewardChoiceView BoundReward()
        {
            var parent = Canvas();
            var view = RewardChoiceView.EditorCreate(parent);
            var presenter = new GameObject("Presenter").AddComponent<BattlePresenter>();
            presenter.transform.SetParent(parent, false);
            presenter.Initialize(id => id, KoreanDescriptionCatalog.CreateDefault(UnityTestContent.Statuses()));
            var cards = AssetDatabase.LoadAssetAtPath<CardPrefabCatalog>(CardPrefabCatalogTests.CatalogPath);
            view.EditorBind(cards, presenter);
            return view;
        }

        private static IReadOnlyList<RewardCandidate> Candidates(params (string cardId, string owner)[] items)
        {
            var content = UnityTestContent.Content();
            return items.Select(item => new RewardCandidate(content.Cards.Get(item.cardId), item.owner)).ToList();
        }

        [Test]
        public void Reward_view_authors_three_slots_and_both_buttons()
        {
            var view = RewardChoiceView.EditorCreate(Canvas());

            Assert.AreEqual(3, RewardChoiceView.SlotCount);
            Assert.AreEqual(3, CardPrefabCatalogTests.Field<RectTransform[]>(view, "_slots").Count(s => s != null));
            Assert.AreEqual(3, CardPrefabCatalogTests.Field<TMP_Text[]>(view, "_ownerLabels").Count(l => l != null));
            Assert.AreEqual(3, CardPrefabCatalogTests.Field<Button[]>(view, "_slotButtons").Count(b => b != null));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<Button>(view, "_skipButton"));
            Assert.IsNotNull(CardPrefabCatalogTests.Field<Button>(view, "_nextButton"));
            Assert.IsFalse(view.IsBound, "프리팹 단계에서는 presenter·catalog가 비어 있다 — 씬이 채운다.");
            Assert.IsFalse(view.gameObject.activeSelf, "기본 비활성이어야 한다.");
        }

        [Test]
        public void Show_fills_only_as_many_slots_as_candidates_and_reports_the_index()
        {
            var view = BoundReward();
            int chosen = -1;

            view.Show(Candidates(("hasten", "member_a"), ("breather", "member_b")),
                id => id == "member_a" ? "파티원 A" : "파티원 B", i => chosen = i, () => { });

            var slots = CardPrefabCatalogTests.Field<RectTransform[]>(view, "_slots");
            var buttons = CardPrefabCatalogTests.Field<Button[]>(view, "_slotButtons");
            var labels = CardPrefabCatalogTests.Field<TMP_Text[]>(view, "_ownerLabels");
            Assert.IsTrue(view.gameObject.activeSelf);
            Assert.AreEqual(1, slots[0].GetComponentsInChildren<CardView>(true).Length);
            Assert.AreEqual(1, slots[1].GetComponentsInChildren<CardView>(true).Length);
            Assert.AreEqual(0, slots[2].GetComponentsInChildren<CardView>(true).Length);
            Assert.IsTrue(buttons[1].gameObject.activeSelf);
            Assert.IsFalse(buttons[2].gameObject.activeSelf);
            Assert.AreEqual("파티원 B", labels[1].text);

            buttons[1].onClick.Invoke();

            Assert.AreEqual(1, chosen);
        }

        [Test]
        public void ShowNextButton_locks_the_choice_and_Hide_clears_the_cards()
        {
            var view = BoundReward();
            bool skipped = false, next = false;
            view.Show(Candidates(("hasten", "member_a")), id => id, _ => { }, () => skipped = true);
            CardPrefabCatalogTests.Field<Button>(view, "_skipButton").onClick.Invoke();
            Assert.IsTrue(skipped);

            view.ShowNextButton(() => next = true);

            Assert.IsFalse(CardPrefabCatalogTests.Field<Button[]>(view, "_slotButtons")[0].interactable);
            Assert.IsFalse(CardPrefabCatalogTests.Field<Button>(view, "_skipButton").gameObject.activeSelf);
            var nextButton = CardPrefabCatalogTests.Field<Button>(view, "_nextButton");
            Assert.IsTrue(nextButton.gameObject.activeSelf);
            nextButton.onClick.Invoke();
            Assert.IsTrue(next);

            view.Hide();

            Assert.IsFalse(view.gameObject.activeSelf);
            Assert.AreEqual(0, view.GetComponentsInChildren<CardView>(true).Length);
        }

        [Test]
        public void Showing_again_replaces_the_previous_cards()
        {
            var view = BoundReward();
            view.Show(Candidates(("hasten", "member_a"), ("breather", "member_a")), id => id, _ => { }, () => { });

            view.Show(Candidates(("hasten", "member_a")), id => id, _ => { }, () => { });

            Assert.AreEqual(1, view.GetComponentsInChildren<CardView>(true).Length);
        }

        [Test]
        public void Result_view_shows_defeat_and_reports_restart()
        {
            var view = CombatResultView.EditorCreate(Canvas());
            bool restarted = false;

            Assert.IsTrue(view.IsBound);
            Assert.IsFalse(view.gameObject.activeSelf);
            view.ShowDefeat(() => restarted = true);
            Assert.IsTrue(view.gameObject.activeSelf);
            CardPrefabCatalogTests.Field<Button>(view, "_restartButton").onClick.Invoke();
            Assert.IsTrue(restarted);

            view.Hide();
            Assert.IsFalse(view.gameObject.activeSelf);
        }
    }
}
```

(`hasten`·`breather`는 `Pools/starter.json`에 있는 실제 카드다. `UnityTestContent.Content()`는 `Assets/Tests/UnityEditMode/UnityTestContent.cs:22`.)

- [ ] **Step 2: `BattleUiKit`에 버튼 헬퍼를 더한다**

`BattleUiKit.cs`의 `Text` 메서드 아래에:

```csharp
        /// <summary>배경 이미지와 가운데 라벨을 가진 버튼. 라벨 문구는 에디터 저작 시점에 굽는다.</summary>
        public static Button LabeledButton(RectTransform parent, string name, string label, float fontSize)
        {
            var root = Rect(parent, name);
            var background = Image(root, "Background", new Color(0.22f, 0.28f, 0.42f, 1f));
            Stretch(background.rectTransform);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var text = Text(root, "Label", fontSize, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            text.text = label;
            return button;
        }
```

- [ ] **Step 3: `RewardChoiceView`를 구현한다**

`Assets/Unity/Scripts/Battle/RewardChoiceView.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>보상 후보를 보여주고 고른 번호를 알린다. 후보가 어떻게 뽑혔는지, 선택이 어디
    /// 반영되는지는 모른다(전투 노드 설계 1.6).</summary>
    public sealed class RewardChoiceView : MonoBehaviour
    {
        public const int SlotCount = 3;

        [SerializeField] private RectTransform[] _slots = new RectTransform[SlotCount];
        [SerializeField] private TMP_Text[] _ownerLabels = new TMP_Text[SlotCount];
        [SerializeField] private Button[] _slotButtons = new Button[SlotCount];
        [SerializeField] private Button _skipButton;
        [SerializeField] private Button _nextButton;

        [Tooltip("씬 인스턴스에서 채운다 — 프리팹은 씬 객체를 참조할 수 없다.")]
        [SerializeField] private CardPrefabCatalog _cards;
        [SerializeField] private BattlePresenter _presenter;

        private readonly List<CardView> _spawned = new List<CardView>();

        public bool IsBound
            => _cards != null && _presenter != null && _skipButton != null && _nextButton != null
                && AllSet(_slots) && AllSet(_ownerLabels) && AllSet(_slotButtons);

        public void Show(
            IReadOnlyList<RewardCandidate> candidates,
            Func<string, string> ownerName,
            Action<int> onChoose,
            Action onSkip)
        {
            ClearSpawned();
            gameObject.SetActive(true);
            for (int i = 0; i < SlotCount; i++)
            {
                var hasCandidate = i < candidates.Count;
                _slotButtons[i].onClick.RemoveAllListeners();
                _slotButtons[i].interactable = hasCandidate;
                _slotButtons[i].gameObject.SetActive(hasCandidate);
                _ownerLabels[i].text = hasCandidate ? ownerName(candidates[i].OwnerId) : string.Empty;
                if (!hasCandidate)
                {
                    continue;
                }

                var candidate = candidates[i];
                _spawned.Add(_cards.Create(
                    _presenter.For(new OwnedCard(candidate.Card, candidate.OwnerId)), _slots[i]));
                var index = i;
                _slotButtons[i].onClick.AddListener(() => onChoose(index));
            }

            _skipButton.onClick.RemoveAllListeners();
            _skipButton.onClick.AddListener(() => onSkip());
            _skipButton.gameObject.SetActive(true);
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.gameObject.SetActive(false);
        }

        public void ShowNextButton(Action onNext)
        {
            foreach (var button in _slotButtons)
            {
                button.interactable = false;
            }

            _skipButton.gameObject.SetActive(false);
            _nextButton.onClick.RemoveAllListeners();
            _nextButton.onClick.AddListener(() => onNext());
            _nextButton.gameObject.SetActive(true);
        }

        public void Hide()
        {
            ClearSpawned();
            gameObject.SetActive(false);
        }

        private void ClearSpawned()
        {
            foreach (var view in _spawned)
            {
                if (view == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(view.gameObject);
                }
                else
                {
                    DestroyImmediate(view.gameObject);
                }
            }

            _spawned.Clear();
        }

        private static bool AllSet<T>(T[] items) where T : UnityEngine.Object
        {
            if (items == null || items.Length != SlotCount)
            {
                return false;
            }

            foreach (var item in items)
            {
                if (item == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>씬 빌더가 인스턴스에 씬 참조를 채울 때 쓴다.</summary>
        public void EditorBind(CardPrefabCatalog cards, BattlePresenter presenter)
        {
            _cards = cards;
            _presenter = presenter;
        }

        /// <summary>프리팹 저작용 훅. BattleSceneBuilder가 부른다(FloatingNumberView.EditorCreate와 같은 관례).
        /// 위치·크기·색은 사용자가 조정한다(규칙 17).</summary>
        public static RewardChoiceView EditorCreate(RectTransform parent)
        {
            var root = BattleUiKit.Rect(parent, "RewardChoiceView");
            BattleUiKit.Stretch(root);
            var dim = BattleUiKit.Image(root, "Dim", new Color(0f, 0f, 0f, 0.7f));
            BattleUiKit.Stretch(dim.rectTransform);

            var title = BattleUiKit.Text(root, "Title", 32f, TextAlignmentOptions.Center);
            title.text = "보상";
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(400f, 50f));

            var view = root.gameObject.AddComponent<RewardChoiceView>();
            for (int i = 0; i < SlotCount; i++)
            {
                var x = (i - 1) * 260f;
                var slot = BattleUiKit.Rect(root, "Slot" + i);
                Place(slot, new Vector2(0.5f, 0.5f), new Vector2(x, 40f), new Vector2(220f, 300f));
                view._slots[i] = slot;

                var owner = BattleUiKit.Text(root, "Owner" + i, 18f, TextAlignmentOptions.Center);
                Place(owner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, -130f), new Vector2(220f, 30f));
                view._ownerLabels[i] = owner;

                var select = BattleUiKit.LabeledButton(root, "Select" + i, "선택", 20f);
                Place((RectTransform)select.transform, new Vector2(0.5f, 0.5f), new Vector2(x, -175f), new Vector2(140f, 44f));
                view._slotButtons[i] = select;
            }

            view._skipButton = BattleUiKit.LabeledButton(root, "SkipButton", "건너뛰기", 20f);
            Place((RectTransform)view._skipButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(180f, 48f));
            view._nextButton = BattleUiKit.LabeledButton(root, "NextButton", "다음 전투", 20f);
            Place((RectTransform)view._nextButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(180f, 48f));
            view._nextButton.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
            return view;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
```

- [ ] **Step 4: `CombatResultView`를 구현한다**

`Assets/Unity/Scripts/Battle/CombatResultView.cs`:

```csharp
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FateWeaver.Unity
{
    /// <summary>패배 결과를 보여주고 다시 시작을 알린다. 무엇이 다시 시작되는지는 모른다.</summary>
    public sealed class CombatResultView : MonoBehaviour
    {
        [SerializeField] private Button _restartButton;

        public bool IsBound => _restartButton != null;

        public void ShowDefeat(Action onRestart)
        {
            gameObject.SetActive(true);
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => onRestart());
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>프리팹 저작용 훅. BattleSceneBuilder가 부른다.</summary>
        public static CombatResultView EditorCreate(RectTransform parent)
        {
            var root = BattleUiKit.Rect(parent, "CombatResultView");
            BattleUiKit.Stretch(root);
            var dim = BattleUiKit.Image(root, "Dim", new Color(0f, 0f, 0f, 0.7f));
            BattleUiKit.Stretch(dim.rectTransform);

            var title = BattleUiKit.Text(root, "Title", 44f, TextAlignmentOptions.Center);
            title.text = "패배";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 60f);
            titleRect.sizeDelta = new Vector2(400f, 70f);

            var view = root.gameObject.AddComponent<CombatResultView>();
            view._restartButton = BattleUiKit.LabeledButton(root, "RestartButton", "처음부터", 22f);
            var buttonRect = (RectTransform)view._restartButton.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -40f);
            buttonRect.sizeDelta = new Vector2(200f, 52f);

            root.gameObject.SetActive(false);
            return view;
        }
    }
}
```

- [ ] **Step 5: 코어 회귀만 확인하고 커밋한다**

Run: `Tools/verify.sh --lint`
Expected: `모두 통과`(규칙 3·4 검사가 새 Unity 파일에도 걸린다 — `public` 필드 없음, 문자열 탐색 없음)

```bash
git add Assets/Unity/Scripts/Battle/BattleUiKit.cs Assets/Unity/Scripts/Battle/RewardChoiceView.cs \
  Assets/Unity/Scripts/Battle/CombatResultView.cs Assets/Tests/UnityEditMode/CombatNodeViewsTests.cs
git commit -m "feat(ui): 보상 선택과 패배 결과 패널 뷰를 만든다"
```

본문: Unity 테스트 실행은 컨트롤러 분리(다음 커밋) 뒤 배치에서 한다는 점을 적는다.

---

### Task 9: 컨트롤러 분리, `CombatNodeFlow`, 씬 재생성

**Files:**
- Modify: `Assets/Unity/Scripts/Battle/BattleScreenController.cs`
- Create: `Assets/Unity/Scripts/Battle/CombatNodeFlow.cs`
- Modify: `Assets/Unity/Editor/BattleSceneBuilder.cs`
- Regenerate: `Assets/Scenes/FateWeaverBattle.unity`, Create: `Assets/Unity/Prefabs/RewardChoiceView.prefab`, `CombatResultView.prefab`
- Test: `Assets/Tests/UnityEditMode/CombatNodeFlowSceneTests.cs`

**Interfaces:**
- Consumes: `CombatNode`·`CombatNodeContext`·`CombatNodePhase`(T7), `RunSetup`(T4), `GoblinEncounterSource`(T5), `CharacterPoolRewardSource`(T6), 뷰 둘(T8)
- Produces: `BattleScreenController.Initialize(Action onRestart, Action onCombatFinished) : bool`, `Bind(DeckCombatSession session, GameContent content)`, `ShowMessage(string message)`

**규칙 30 판정(설계 승인 사항):** 컨트롤러는 "주어진 전투 세션 하나를 화면에 붙여 입력을 전달한다"로 줄고, 흐름은 `CombatNodeFlow`가 맡는다. 이 분리 외의 기능을 컨트롤러에 얹지 않는다.

- [ ] **Step 1: 씬 배선 테스트를 쓴다**

`Assets/Tests/UnityEditMode/CombatNodeFlowSceneTests.cs`:

```csharp
using System.Linq;
using System.Reflection;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CombatNodeFlowSceneTests
    {
        [Test]
        public void Battle_scene_wires_the_combat_node_flow()
        {
            var scene = EditorSceneManager.OpenScene(CardPrefabCatalogTests.BattleScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                T Single<T>() where T : UnityEngine.Component
                    => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

                var flow = Single<CombatNodeFlow>();
                var controller = Single<BattleScreenController>();
                var reward = Single<RewardChoiceView>();
                var result = Single<CombatResultView>();
                var presenter = Single<BattlePresenter>();

                Assert.AreSame(controller, CardPrefabCatalogTests.Field<BattleScreenController>(flow, "_battle"));
                Assert.AreSame(reward, CardPrefabCatalogTests.Field<RewardChoiceView>(flow, "_reward"));
                Assert.AreSame(result, CardPrefabCatalogTests.Field<CombatResultView>(flow, "_result"));
                Assert.AreEqual(2, CardPrefabCatalogTests.Field<CharacterAsset[]>(flow, "_party").Length);
                Assert.IsTrue(reward.IsBound, "씬 인스턴스는 presenter·catalog까지 채워져야 한다.");
                Assert.AreSame(presenter, CardPrefabCatalogTests.Field<BattlePresenter>(reward, "_presenter"));
                Assert.IsTrue(result.IsBound);
                Assert.IsFalse(reward.gameObject.activeSelf);
                Assert.IsFalse(result.gameObject.activeSelf);
                Assert.IsNull(
                    typeof(BattleScreenController).GetField("_party", BindingFlags.Instance | BindingFlags.NonPublic),
                    "파티 구성은 컨트롤러가 아니라 흐름이 든다.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
```

- [ ] **Step 2: `BattleScreenController`를 줄인다**

다음을 순서대로 적용한다(줄 번호는 2026-09-15 master 기준).

(a) 필드 — `[Header("Data")]`와 `_party` 필드(21-22행), `FateEnergyPerTurn`·`Seed` 상수(34-35행)를 지운다. `_content` 필드 주석을 "전투 화면에 바인딩된 콘텐츠. 소유는 CombatNodeFlow다."로 바꾼다. 필드 두 개를 더한다:

```csharp
        private Action _onCombatFinished;
```

(b) `Start()`(45-60행)를 `Initialize`로 교체한다:

```csharp
        /// <summary>CombatNodeFlow가 한 번 부른다. 배선이 비었으면 false — 그때는 콘솔로만 보고한다
        /// (_hud가 비어 있으면 메시지도 못 쓴다, 설계 §6).</summary>
        public bool Initialize(Action onRestart, Action onCombatFinished)
        {
            if (!IsWired())
            {
                Debug.LogError("전투 화면 컴포넌트 배선이 비어 있습니다.");
                return false;
            }

            _onCombatFinished = onCombatFinished;
            _hud.Initialize(
                OnTurnButton, () => onRestart(), _playback.Skip, speed => _playback.Speed = speed);
            _playback.Speed = _hud.Speed;
            _selection.Initialize(TryApplySelection, CurrentValidTargets, RefreshAll);
            return true;
        }

        public void ShowMessage(string message) => SetMessage(message);
```

원래 `Start()`에서 `_selection.Initialize(...)` 뒤, `StartSession()` 앞에 다른 호출이 있었다면 그것도 `Initialize` 안에 옮긴다(`StartSession()` 호출만 뺀다).

(c) `StartSession()`(70-125행)을 `Bind`로 교체한다:

```csharp
        /// <summary>전투 세션 하나를 화면에 붙인다. 파티·적·수치 구성은 모른다 — CombatNode가 만든다.</summary>
        public void Bind(DeckCombatSession session, GameContent content)
        {
            _selection.CancelSelection();
            _session = session;
            _content = content;
            _korean = KoreanDescriptionCatalog.CreateDefault(_content.Statuses);
            _presenter.Initialize(OwnerNameOf, _korean);
            _units.Spawn(
                _session.State,
                _presenter.OwnerColor,
                EnemyNameOf,
                key => _content.Statuses.DisplayNameOf(key));
            _piles.Bind(
                () => Presentations(_session.DrawPile)
                    .OrderBy(presentation => presentation.DisplayName, StringComparer.Ordinal)
                    .ToList(),
                () => Presentations(_session.DiscardPile),
                () => Presentations(_session.AllDeckCards));
            SetMessage("전투 시작.");
            RefreshAll();
        }

        /// <summary>적 이름은 전투 안 id가 아니라 정의 id(SpecId)로 찾는다 — 같은 적이 여럿이어도 이름은 같다.</summary>
        private string EnemyNameOf(string combatId)
        {
            foreach (var enemy in _session.State.Enemies)
            {
                if (enemy.Id == combatId)
                {
                    return PlaytestKoreanText.EnemyName(enemy.SpecId, enemy.SpecId);
                }
            }

            return combatId;
        }
```

원래 `StartSession()`의 `_units.Spawn`·`_piles.Bind` 인자가 위와 다르면 **원래 인자를 그대로 두고** 적 이름 람다만 `EnemyNameOf`로 바꾼다.

(d) `OnPlaybackComplete()`(332-338행)를 교체한다:

```csharp
        private void OnPlaybackComplete()
        {
            SetMessage(_session.IsComplete
                ? "전투 결과: " + PlaytestKoreanText.OutcomeName(_session.Outcome)
                : "턴 해석 완료.");
            RefreshAll();
            if (_session.IsComplete)
            {
                _onCombatFinished?.Invoke();
            }
        }
```

(e) 사용하지 않게 된 `using`(예: `FateWeaver.Core.Authoring`은 `GameContent` 때문에 **남는다**)을 정리하되, 컴파일에 필요한 것은 지우지 않는다. 클래스 요약 주석을 "주어진 전투 세션 하나를 화면에 붙여 입력을 전달한다. 전투 구성과 전투 뒤의 흐름은 CombatNodeFlow가 맡는다."로 바꾼다.

- [ ] **Step 3: `CombatNodeFlow`를 만든다**

`Assets/Unity/Scripts/Battle/CombatNodeFlow.cs`:

```csharp
using System;
using System.Linq;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>런 상태를 들고 전투 노드를 시작·종료시키는 호출 순서를 정한다. 승패·보상 후보·덱
    /// 반영은 전부 CombatNode가 답하고, 이 객체는 누구를 언제 부를지만 안다(규칙 30).</summary>
    public sealed class CombatNodeFlow : MonoBehaviour
    {
        [SerializeField] private BattleScreenController _battle;
        [SerializeField] private RewardChoiceView _reward;
        [SerializeField] private CombatResultView _result;

        [Tooltip("시작 파티 순서. 색은 BattlePresenter가 따로 든다.")]
        [SerializeField] private CharacterAsset[] _party = Array.Empty<CharacterAsset>();

        [Tooltip("런 시드. 같은 시드 + 같은 행동 = 같은 결과.")]
        [SerializeField] private int _runSeed = 1;

        [Tooltip("1단계 임시 튜닝. 2단계에서 combat_rules.json으로 옮긴다.")]
        [SerializeField] private int _fateEnergyPerTurn = 3;
        [SerializeField] private int _rewardChoices = 3;

        private GameContent _content;
        private CombatNodeContext _context;
        private RunState _run;
        private CombatNode _node;

        private void Start()
        {
            if (_battle == null || _reward == null || _result == null)
            {
                Debug.LogError("전투 노드 흐름 배선이 비어 있습니다.");
                return;
            }

            if (!_battle.Initialize(NewRun, OnCombatFinished))
            {
                return;
            }

            var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
            if (!loaded.Succeeded)
            {
                var reasons = string.Join("\n", loaded.Errors);
                _battle.ShowMessage("콘텐츠 로드 실패:\n" + reasons);
                Debug.LogError("콘텐츠 로드 실패:\n" + reasons);
                return;
            }

            _content = loaded.Content;
            _context = new CombatNodeContext(
                _content.Statuses,
                PartyPrototypeRoster.Tuning,
                _fateEnergyPerTurn,
                _rewardChoices,
                new GoblinEncounterSource(),
                new CharacterPoolRewardSource(_content));
            NewRun();
        }

        private void NewRun()
        {
            if (_content == null)
            {
                return;
            }

            if (_party == null || _party.Length == 0 || _party.Any(member => member == null))
            {
                _battle.ShowMessage("파티 CharacterAsset이 연결되지 않았습니다.");
                return;
            }

            _run = RunSetup.NewRun(
                _content, _party.Select(member => member.Id).ToList(), PartyPrototypeRoster.Tuning, _runSeed);
            BeginNode();
        }

        private void BeginNode()
        {
            _reward.Hide();
            _result.Hide();
            _node = CombatNode.Begin(_run, _context);
            _battle.Bind(_node.Session, _content);
        }

        private void OnCombatFinished()
        {
            _node.Conclude();
            if (_node.Phase == CombatNodePhase.Reward)
            {
                _reward.Show(_node.Offer.Candidates, OwnerName, OnChoose, OnSkip);
            }
            else if (_node.Phase == CombatNodePhase.Defeated)
            {
                _result.ShowDefeat(NewRun);
            }
        }

        private void OnChoose(int index)
        {
            _node.Choose(index);
            _reward.ShowNextButton(BeginNode);
        }

        private void OnSkip()
        {
            _node.Skip();
            _reward.ShowNextButton(BeginNode);
        }

        private string OwnerName(string ownerId)
            => _run.Party.First(member => member.Id == ownerId).Name;
    }
}
```

("콘텐츠 로드 실패"·"파티 CharacterAsset이 연결되지 않았습니다."는 기존 컨트롤러에서 옮긴 문구다.)

- [ ] **Step 4: 씬 빌더를 고친다**

`BattleSceneBuilder.cs`:

(a) 상수 영역(17-20행 아래)에:

```csharp
        private const string RewardChoicePrefabPath = "Assets/Unity/Prefabs/RewardChoiceView.prefab";
        private const string CombatResultPrefabPath = "Assets/Unity/Prefabs/CombatResultView.prefab";
```

(b) `EnsureFloatingNumberPrefab();` 호출 아래에:

```csharp
            EnsureRewardChoicePrefab();
            EnsureCombatResultPrefab();
```

(c) `EnsureFloatingNumberPrefab` 메서드 아래에 두 메서드:

```csharp
        private static void EnsureRewardChoicePrefab()
        {
            var temporaryRoot = new GameObject("RewardChoicePrefabBuilder", typeof(RectTransform));
            try
            {
                var view = RewardChoiceView.EditorCreate((RectTransform)temporaryRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, RewardChoicePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static void EnsureCombatResultPrefab()
        {
            var temporaryRoot = new GameObject("CombatResultPrefabBuilder", typeof(RectTransform));
            try
            {
                var view = CombatResultView.EditorCreate((RectTransform)temporaryRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(view.gameObject, CombatResultPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temporaryRoot);
            }
        }
```

(d) 컨트롤러 배선 블록(`var serializedParty = so.FindProperty("_party");`부터 그 `for` 루프 끝까지, 224-229행)을 **지운다**.

(e) `so.ApplyModifiedPropertiesWithoutUndo();`(컨트롤러 배선의 마지막 줄) 바로 아래, `EditorSceneManager.SaveScene` 위에:

```csharp
            // --- 전투 노드 흐름: 보상·패배 패널은 overlay 위, 흐름은 관리자 객체 ---
            var rewardObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(RewardChoicePrefabPath), overlay);
            var reward = rewardObject.GetComponent<RewardChoiceView>();
            reward.EditorBind(cardPrefabs, presenter);
            EditorUtility.SetDirty(reward);
            PrefabUtility.RecordPrefabInstancePropertyModifications(reward);

            var resultObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(CombatResultPrefabPath), overlay);
            var result = resultObject.GetComponent<CombatResultView>();

            var flowGo = new GameObject("CombatNodeFlow");
            var flow = flowGo.AddComponent<CombatNodeFlow>();
            var flowSo = new SerializedObject(flow);
            flowSo.FindProperty("_battle").objectReferenceValue = controller;
            flowSo.FindProperty("_reward").objectReferenceValue = reward;
            flowSo.FindProperty("_result").objectReferenceValue = result;
            var flowParty = flowSo.FindProperty("_party");
            flowParty.arraySize = party.Length;
            for (int i = 0; i < party.Length; i++)
            {
                flowParty.GetArrayElementAtIndex(i).objectReferenceValue = party[i];
            }

            flowSo.ApplyModifiedPropertiesWithoutUndo();
            overlay.SetAsLastSibling();
```

(`cardPrefabs`·`presenter`·`overlay`·`controller`·`party`는 `Build()` 안에 이미 있는 지역 변수다.)

- [ ] **Step 5: Unity를 닫은 상태로 씬을 재생성한다(배치)**

메인 체크아웃의 에디터와 무관하게 **워크트리**를 대상으로 돈다(규칙 17·26). 좀비 라이선싱 클라이언트로 멈추면 `docs/agents/unity-batch-runs.md` 절차를 따른다.

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath <WT> \
  -executeMethod FateWeaver.Unity.Editor.BattleSceneBuilder.Build \
  -logFile /private/tmp/combat-node-build.log
```

(`-quit` 함정은 `-runTests`와 함께 줄 때만이다. `-executeMethod`에는 `-quit`가 필요하다.)
첫 실행은 워크트리의 `Library/` 임포트로 오래 걸린다.
Expected: 로그에 `BattleSceneBuilder: saved Assets/Scenes/FateWeaverBattle.unity`, `error CS` 없음.

```bash
grep -n "BattleSceneBuilder: saved\|error CS" /private/tmp/combat-node-build.log
```

컴파일 오류가 있으면 로그의 파일·줄을 고치고 다시 실행한다.

- [ ] **Step 6: EditMode 전체를 배치로 돌린다**

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath <WT> \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/combat-node-editmode.xml \
  -logFile /private/tmp/combat-node-editmode.log
```

```bash
head -3 /private/tmp/combat-node-editmode.xml | grep -o 'result="[^"]*" total="[^"]*" passed="[^"]*" failed="[^"]*"'
grep -o 'name="[^"]*" [^>]*result="Failed"' /private/tmp/combat-node-editmode.xml | head
```

Expected: `failed="0"`. `CombatNodeViewsTests` 5개와 `CombatNodeFlowSceneTests` 1개가 포함된다. 기존 테스트 중 `BattleScreenController`의 `_party`나 `StartSession`을 리플렉션으로 찾는 것이 실패하면 새 구조(`CombatNodeFlow._party`, `Bind`)에 맞춰 이 태스크 안에서 고친다.

- [ ] **Step 7: 헤드리스·규칙 검사를 확인하고 커밋한다**

Run: `Tools/verify.sh`
Expected: `모두 통과`

`git status`로 확인하고 **의도한 것만** 스테이징한다. 배치 실행이 남긴 런타임 부산물(예: `KoreanTMP.asset` 폰트 아틀라스 변경)은 커밋하지 않고 `git checkout -- <경로>`로 되돌린다(`unity-batch-runs.md`). Task 1~8에서 만든 `.cs`의 `.meta`는 이번에 생성됐으므로 함께 커밋한다.

```bash
git add Assets/Unity/Scripts/Battle/BattleScreenController.cs Assets/Unity/Scripts/Battle/CombatNodeFlow.cs \
  Assets/Unity/Editor/BattleSceneBuilder.cs Assets/Scenes/FateWeaverBattle.unity \
  Assets/Unity/Prefabs/RewardChoiceView.prefab Assets/Unity/Prefabs/RewardChoiceView.prefab.meta \
  Assets/Unity/Prefabs/CombatResultView.prefab Assets/Unity/Prefabs/CombatResultView.prefab.meta \
  Assets/Tests/UnityEditMode/CombatNodeFlowSceneTests.cs
git add $(git ls-files --others --exclude-standard -- 'Assets/**/*.cs.meta')
git status --short
git commit -m "feat(ui): 전투 노드 흐름을 컨트롤러에서 분리하고 보상·패배 패널을 씬에 배선한다"
```

`git status --short`에 커밋 후에도 `Assets/` 변경이 남으면 무엇인지 확인하고, 부산물이면 되돌리고 의도한 변경이면 추가 커밋한다(규칙 18).

---

### Task 10: 사용자 확인과 문서 마무리

**Files:**
- Modify: `docs/superpowers/specs/2026-09-15-combat-node-cycle-design.md` (상태 줄)
- Modify: `docs/superpowers/README.md` (전투 노드 행 상태 문구)
- Modify: `Assets/Unity/PLAYTEST.md` (확인 항목 추가)

- [ ] **Step 1: 전체 검증**

Run: `Tools/verify.sh`
Expected: `모두 통과`. Task 9 Step 6의 EditMode 결과가 `failed="0"`이었는지 다시 확인한다.

- [ ] **Step 2: `PLAYTEST.md`에 확인 항목을 더한다**

`Assets/Unity/PLAYTEST.md` 끝에:

```markdown
## 전투 노드 한 사이클 (2026-09-15)

1. 고블린을 이기면 보상 패널이 뜨고 카드가 1~3장, 카드마다 소유 파티원 이름이 보인다.
2. 카드의 `선택`을 누르면 선택 버튼이 잠기고 `다음 전투`가 나타난다. `건너뛰기`도 같다.
3. `다음 전투`를 누르면 새 전투가 시작되고, `전체 덱`에 방금 고른 카드가 들어 있다.
4. 지면 패배 패널이 뜨고 `처음부터`로 처음 덱의 새 런이 시작된다.
5. HUD의 `초기화`는 어느 단계에서 눌러도 처음 덱의 새 런을 시작하고 패널을 닫는다.
6. 같은 런 시드에서 같은 순서로 플레이하면 첫 보상 후보가 매번 같다.
```

- [ ] **Step 3: 사용자에게 맡긴다 (멈춤)**

사용자에게 보고하고 기다린다: 워크트리 경로, 위 6개 확인 항목, **두 패널의 위치·크기·색 조정은 사용자 몫**(규칙 17). 사용자가 워크트리를 Unity로 열어 Play로 확인한다. 조정한 프리팹·씬 변경이 생기면 사용자 승인 후 커밋한다.

- [ ] **Step 4: 문서 상태를 갱신하고 커밋한다**

설계 `.md` 머리의 `**상태:**` 줄을 `` `active` — 1단계 구현 완료(날짜), 2단계(구성 저작) 착수 전. `` 으로 바꾼다.
`README.md` 전투 노드 행의 "설계 승인 대기"를 "1단계 구현 완료, 2단계 착수 전"으로 바꾼다.
이 계획 문서는 **2단계 계획이 따로 생기므로 완료 시 아카이브한다**: `docs/superpowers/plans/2026-09-15-combat-node-stage1.{md,html}`을 `docs/superpowers/.archive/plans/`로 옮기고, README 「활성 계획과 로드맵」에서 이 계획 행을 지우고, `.archive/README.md` 보관 색인에 행을 더한다(규칙 20, `docs/agents/doc-lifecycle.md`의 순서).

```bash
git add Assets/Unity/PLAYTEST.md docs/superpowers
git commit -m "docs: 전투 노드 1단계 완료를 기록하고 계획을 보관한다"
```

- [ ] **Step 5: 머지 승인을 요청한다 (멈춤)**

master 머지는 사용자 승인 후에만 한다(규칙 19). 승인되면 `superpowers:finishing-a-development-branch` 흐름으로 머지하고, **Unity를 돌린 워크트리이므로 머지 후 워크트리를 제거한다**(`docs/agents/worktrees.md` 「워크트리 비용과 정리」).
