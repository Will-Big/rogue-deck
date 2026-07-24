# 전투 노드(Combat Node) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 런의 전투 노드 — `RunState`에서 전투(`DeckCombatSession`)를 만들고(HP 이월·합성 덱·파생 시드), 끝난 전투를 `RunState`에 되반영(생존자 HP, 사망자 제거, 보스 승리/전멸 패배)한다.

**Architecture:** 스펙 [2026-07-20-run-cycle-skeleton-design.md](../specs/2026-07-20-run-cycle-skeleton-design.md) §3.3, §3.5. `CombatNodeHandler`는 `IRunNodeHandler`(run-core-foundation 산출)를 구현하고 일반/엘리트/보스 세 키에 각각 인스턴스로 등록된다(보스 여부는 생성자 플래그 — 중앙 switch 없음). 전투 자체는 상호작용형이므로 핸들러는 `CreateSession`/`ApplyResult` 두 진입점을 제공하고, 그 사이는 UI(또는 테스트)가 세션을 구동한다.

**Tech Stack:** C# 9, NUnit, `dotnet test` 헤드리스 하니스.

## Global Constraints

- **선행 조건: `feat/run-core`(run-core-foundation 계획)가 master에 머지된 뒤 시작한다.**
- 헤드리스 테스트 실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
- LangVersion 9. UnityEngine 참조 금지 (`Assets/Core/**`).
- 무작위는 `RunState.Rng`·`CombatState.Rng`만 경유 (규칙 7). 전투 시드는 반드시 `run.NextCombatSeed()`로 파생.
- 튜닝 수치 하드코딩 금지 (규칙 8) — 운명력/인카운터 HP는 생성자·데이터로.
- 작업은 전용 워크트리에서: `git worktree add ../rogue-deck-run-combat-node -b feat/run-combat-node`
- `Assets/` 아래 새 파일의 `.meta`는 병합 전 Unity `-batchmode` 1회 실행으로 생성해 함께 커밋 (규칙 16·17).
- 병렬 주의: 이 계획은 `Assets/Core/Simulation/PartyMemberLoadout.cs`와 `DeckCombatSession.cs`를 수정하는 **유일한** 계획이다. 다른 병렬 계획(고용·회복, 보상)과 파일이 겹치지 않는다.

---

### Task 1: PartyMemberLoadout 시작 HP + DeckCombatSession 반영 (HP 이월 씨앗)

**Files:**
- Modify: `Assets/Core/Simulation/PartyMemberLoadout.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs` (파티 분기의 `PartyMember` 생성부, 약 115~122행)
- Test: `Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs` (신규 — 이후 태스크가 같은 파일에 테스트를 추가)

**Interfaces:**
- Consumes: 기존 `PartyMemberLoadout(string id, string name, int maxHp, IReadOnlyList<CardDefinition> cards)`, `DeckCombatSession` 파티 생성자
- Produces: `PartyMemberLoadout`에 5번째 선택 인자 `int startingHp = PartyMemberLoadout.StartAtMaxHp`(상수 `-1` = 최대 HP로 시작), 속성 `int StartingHp`, `int EffectiveStartingHp`. 기존 호출부는 변경 불필요(기본값 유지).

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Combat;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class CombatNodeHandlerTests
    {
        [Test]
        public void Session_StartsPartyAtCarriedHp()
        {
            var tuning = PartyTuning.Prototype;
            var loadout = new PartyMemberLoadout(
                "member_a", "파티원 A", tuning.DefaultMemberMaxHp, StarterDeck.Build(), startingHp: 7);
            var session = new DeckCombatSession(
                new[] { loadout },
                new[] { new Enemy(GoblinDeck.EnemyId, GoblinDeck.StartingHp) },
                GoblinDeck.Policy(),
                tuning,
                seed: 1);

            var member = session.State.Party.Single(m => m.Id == "member_a");
            Assert.That(member.Hp, Is.EqualTo(7));
            Assert.That(member.MaxHp, Is.EqualTo(tuning.DefaultMemberMaxHp));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CombatNodeHandlerTests`
Expected: 컴파일 에러 (`startingHp` 인자 없음)

- [ ] **Step 3: 구현**

`PartyMemberLoadout.cs` 전체 교체:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>One party member and the cards they contribute to the combined combat deck.
    /// StartingHp lets the run layer carry HP between combats; StartAtMaxHp (default) keeps the
    /// pre-run behavior of starting at full HP.</summary>
    public sealed class PartyMemberLoadout
    {
        public const int StartAtMaxHp = -1;

        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int StartingHp { get; }
        public IReadOnlyList<CardDefinition> Cards { get; }

        public int EffectiveStartingHp => StartingHp == StartAtMaxHp ? MaxHp : StartingHp;

        public PartyMemberLoadout(
            string id,
            string name,
            int maxHp,
            IReadOnlyList<CardDefinition> cards,
            int startingHp = StartAtMaxHp)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Cards = cards;
            StartingHp = startingHp;
        }
    }
}
```

`DeckCombatSession.cs` — 파티 분기(현재 115행 근처)의 멤버 생성:

```csharp
// 변경 전
_state.Party.Add(new PartyMember(
    loadout.Id,
    loadout.Name,
    loadout.MaxHp,
    partyTuning.SurviveChargesPerCombat));
```

```csharp
// 변경 후
var member = new PartyMember(
    loadout.Id,
    loadout.Name,
    loadout.MaxHp,
    partyTuning.SurviveChargesPerCombat);
member.Hp = loadout.EffectiveStartingHp;
_state.Party.Add(member);
```

- [ ] **Step 4: 통과 확인 (전체 실행 — 기존 동작 회귀 없음 확인)**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 PASS (기존 파티 테스트 포함)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/PartyMemberLoadout.cs Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs
git commit -m "feat(run): carry starting HP into combat via PartyMemberLoadout"
```

---

### Task 2: EncounterDefinition·CombatNodePayload·EnemyKind 레지스트리

**Files:**
- Create: `Assets/Core/Simulation/Run/EncounterDefinition.cs`
- Create: `Assets/Core/Simulation/Run/CombatNodePayload.cs`
- Create: `Assets/Core/Simulation/Run/EnemyKind.cs`
- Test: `Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: `IRunNodePayload`(foundation), 기존 `GoblinDeck`·`WardenDeck`(EnemyId, Policy), `IEnemyTurnPolicy`
- Produces:
  - `EncounterDefinition(string id, string enemyKindId, int enemyMaxHp)` — 단일 적 인카운터(스탯 변주). 필드: `Id`, `EnemyKindId`, `EnemyMaxHp`
  - `CombatNodePayload(EncounterDefinition encounter) : IRunNodePayload` — 필드 `Encounter`
  - `EnemyKind(string id, Func<IEnemyTurnPolicy> policyFactory)` — `Id`, `CreatePolicy()`
  - `EnemyKindRegistry { Register, Contains, Resolve }` (EffectRegistry 패턴)
  - `EnemyKinds.Default()` — goblin·warden 등록된 레지스트리

- [ ] **Step 1: 실패하는 테스트 작성 (CombatNodeHandlerTests에 추가)**

```csharp
[Test]
public void EnemyKinds_Default_ResolvesGoblinAndWarden_AndThrowsOnUnknown()
{
    var kinds = EnemyKinds.Default();
    Assert.That(kinds.Resolve(GoblinDeck.EnemyId).CreatePolicy(), Is.Not.Null);
    Assert.That(kinds.Resolve(WardenDeck.EnemyId).CreatePolicy(), Is.Not.Null);
    Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => kinds.Resolve("slime"));
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CombatNodeHandlerTests`
Expected: 컴파일 에러

- [ ] **Step 3: 구현**

`EncounterDefinition.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>A combat node's enemy setup: one existing enemy kind at an authored HP.
    /// Normal/elite/boss are stat variants of existing kinds (run-cycle skeleton spec §1) —
    /// no new mechanics here.</summary>
    public sealed class EncounterDefinition
    {
        public string Id { get; }
        public string EnemyKindId { get; }
        public int EnemyMaxHp { get; }

        public EncounterDefinition(string id, string enemyKindId, int enemyMaxHp)
        {
            Id = id;
            EnemyKindId = enemyKindId;
            EnemyMaxHp = enemyMaxHp;
        }
    }
}
```

`CombatNodePayload.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    public sealed class CombatNodePayload : IRunNodePayload
    {
        public EncounterDefinition Encounter { get; }

        public CombatNodePayload(EncounterDefinition encounter) => Encounter = encounter;
    }
}
```

`EnemyKind.cs` (레지스트리·기본 등록 포함):

```csharp
using System;
using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    /// <summary>One reusable enemy archetype: knows how to build its turn policy. New kinds extend
    /// by registering one entry (AGENTS.md rule 9) — no central switch.</summary>
    public sealed class EnemyKind
    {
        private readonly Func<IEnemyTurnPolicy> _policyFactory;

        public string Id { get; }

        public EnemyKind(string id, Func<IEnemyTurnPolicy> policyFactory)
        {
            Id = id;
            _policyFactory = policyFactory;
        }

        public IEnemyTurnPolicy CreatePolicy() => _policyFactory();
    }

    public sealed class EnemyKindRegistry
    {
        private readonly Dictionary<string, EnemyKind> _kinds = new();

        public void Register(EnemyKind kind) => _kinds[kind.Id] = kind;

        public bool Contains(string id) => _kinds.ContainsKey(id);

        public EnemyKind Resolve(string id)
            => _kinds.TryGetValue(id, out var kind)
                ? kind
                : throw new KeyNotFoundException($"No enemy kind registered for '{id}'");
    }

    public static class EnemyKinds
    {
        public static EnemyKindRegistry Default()
        {
            var registry = new EnemyKindRegistry();
            registry.Register(new EnemyKind(GoblinDeck.EnemyId, GoblinDeck.Policy));
            registry.Register(new EnemyKind(WardenDeck.EnemyId, WardenDeck.Policy));
            return registry;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CombatNodeHandlerTests`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs
git commit -m "feat(run): add encounter definition and enemy kind registry"
```

---

### Task 3: CombatNodeHandler.CreateSession

**Files:**
- Create: `Assets/Core/Simulation/Run/CombatNodeHandler.cs`
- Test: `Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: foundation의 `RunState`·`RunMember`·`RunNodeData`·`IRunNodeHandler`, Task 1·2 산출물, `DeckCombatSession` 파티 생성자
- Produces:
  - `CombatNodeHandler(RunNodeKey key, bool isBossNode, EnemyKindRegistry enemyKinds, int fateEnergyPerTurn)` : `IRunNodeHandler`
  - `DeckCombatSession CreateSession(RunState run, RunNodeData node)` — 생존 멤버만 로드아웃으로(HP 이월), 인카운터 적 1기, 시드 = `run.NextCombatSeed()`

- [ ] **Step 1: 실패하는 테스트 작성 (추가)**

```csharp
private static RunState RunWithCombatNode(int seed, out RunNodeData node)
{
    var tuning = PartyTuning.Prototype;
    node = new RunNodeData(
        RunNodeKeys.NormalCombat,
        new CombatNodePayload(new EncounterDefinition("normal_goblin", GoblinDeck.EnemyId, GoblinDeck.StartingHp)));
    var run = new RunState(
        new RunDefinition(new[] { node }),
        new[]
        {
            new RunMember("member_a", "파티원 A", tuning.DefaultMemberMaxHp, StarterDeck.Build()),
            new RunMember("member_b", "파티원 B", tuning.DefaultMemberMaxHp, PartyPrototypeDeck.Build())
        },
        tuning,
        seed);
    return run;
}

private static CombatNodeHandler NormalHandler() => new CombatNodeHandler(
    RunNodeKeys.NormalCombat, isBossNode: false, EnemyKinds.Default(), fateEnergyPerTurn: 3);

[Test]
public void CreateSession_CarriesHpAndExcludesDeadMembers()
{
    var run = RunWithCombatNode(seed: 7, out var node);
    run.Party[0].Hp = 5;   // 이월된 부상
    run.Party[1].Hp = 0;   // 사망자 — 전투에서 제외

    var session = NormalHandler().CreateSession(run, node);

    Assert.That(session.State.Party.Count, Is.EqualTo(1));
    Assert.That(session.State.Party[0].Id, Is.EqualTo("member_a"));
    Assert.That(session.State.Party[0].Hp, Is.EqualTo(5));
    Assert.That(session.State.Enemies.Count, Is.EqualTo(1));
    Assert.That(session.State.Enemies[0].Hp, Is.EqualTo(GoblinDeck.StartingHp));
}

[Test]
public void CreateSession_DerivesCombatSeedFromRunSeed_Deterministically()
{
    var runA = RunWithCombatNode(seed: 7, out var nodeA);
    var runB = RunWithCombatNode(seed: 7, out var nodeB);

    var sessionA = NormalHandler().CreateSession(runA, nodeA);
    var sessionB = NormalHandler().CreateSession(runB, nodeB);

    Assert.That(sessionA.State.RngSeed, Is.EqualTo(sessionB.State.RngSeed));
    // 같은 시드 → 같은 초기 손패 (결정론)
    Assert.That(
        sessionA.Hand.Select(c => c.Def.Id).ToList(),
        Is.EqualTo(sessionB.Hand.Select(c => c.Def.Id).ToList()));
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CombatNodeHandlerTests`
Expected: 컴파일 에러 (`CombatNodeHandler` 미정의)

- [ ] **Step 3: 구현**

`CombatNodeHandler.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation.Run
{
    public sealed class CombatNodeResult
    {
        public Outcome CombatOutcome { get; }
        public IReadOnlyList<string> DeadMemberIds { get; }

        public CombatNodeResult(Outcome combatOutcome, IReadOnlyList<string> deadMemberIds)
        {
            CombatOutcome = combatOutcome;
            DeadMemberIds = deadMemberIds;
        }
    }

    /// <summary>Bridges RunState and one combat. CreateSession builds an interactive
    /// DeckCombatSession from the living party (HP carried over, combat seed derived from the run
    /// RNG); the UI (or a test) drives the session; ApplyResult then writes survivors' HP back,
    /// removes the dead (their cards perish with them — skeleton has no inheritance), and decides
    /// run victory (boss node won) or defeat (party wiped). Registered once per combat node key;
    /// boss behavior is a constructor flag, not a switch.</summary>
    public sealed class CombatNodeHandler : IRunNodeHandler
    {
        private readonly bool _isBossNode;
        private readonly EnemyKindRegistry _enemyKinds;
        private readonly int _fateEnergyPerTurn;

        public CombatNodeHandler(
            RunNodeKey key,
            bool isBossNode,
            EnemyKindRegistry enemyKinds,
            int fateEnergyPerTurn)
        {
            Key = key;
            _isBossNode = isBossNode;
            _enemyKinds = enemyKinds;
            _fateEnergyPerTurn = fateEnergyPerTurn;
        }

        public RunNodeKey Key { get; }

        public DeckCombatSession CreateSession(RunState run, RunNodeData node)
        {
            var payload = (CombatNodePayload)node.Payload;
            var kind = _enemyKinds.Resolve(payload.Encounter.EnemyKindId);
            var loadouts = run.LivingMembers
                .Select(m => new PartyMemberLoadout(m.Id, m.Name, m.MaxHp, m.Cards, m.Hp))
                .ToList();
            var enemies = new[] { new Enemy(payload.Encounter.EnemyKindId, payload.Encounter.EnemyMaxHp) };
            return new DeckCombatSession(
                loadouts,
                enemies,
                kind.CreatePolicy(),
                run.Tuning,
                partyCards: null,
                fateEnergyPerTurn: _fateEnergyPerTurn,
                seed: run.NextCombatSeed());
        }

        public CombatNodeResult ApplyResult(RunState run, DeckCombatSession session)
            => ApplyResult(run, session.State.Party, session.Outcome);

        /// <summary>Testable seam: applies a finished combat's party state and outcome to the run.</summary>
        public CombatNodeResult ApplyResult(
            RunState run,
            IReadOnlyList<PartyMember> combatParty,
            Outcome combatOutcome)
        {
            var dead = new List<string>();
            foreach (var combatMember in combatParty)
            {
                var runMember = run.Party.FirstOrDefault(m => m.Id == combatMember.Id);
                if (runMember == null)
                {
                    continue;
                }

                runMember.Hp = combatMember.Hp;
                if (!combatMember.IsAlive)
                {
                    dead.Add(runMember.Id);
                }
            }

            // Dead members leave the run with their cards (spec §3.5: perish only, no inheritance).
            run.Party.RemoveAll(m => dead.Contains(m.Id));

            if (combatOutcome == Outcome.Lose || run.Party.Count == 0)
            {
                run.SetOutcome(RunOutcome.Defeat);
            }
            else if (combatOutcome == Outcome.Win && _isBossNode)
            {
                run.SetOutcome(RunOutcome.Victory);
            }

            return new CombatNodeResult(combatOutcome, dead);
        }
    }
}
```

(주: `ApplyResult`는 Task 4에서 테스트한다 — 이 파일은 한 번에 작성하고, Task 3에서는 `CreateSession` 테스트만 통과시키면 된다.)

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CombatNodeHandlerTests`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/CombatNodeHandler.cs Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs
git commit -m "feat(run): combat node handler creates sessions with carried HP and derived seed"
```

---

### Task 4: CombatNodeHandler.ApplyResult 검증

**Files:**
- Modify: `Assets/Core/Simulation/Run/CombatNodeHandler.cs` (Task 3에서 이미 작성 — 테스트가 요구하는 동작과 다르면 수정)
- Test: `Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: Task 3의 `ApplyResult(RunState, IReadOnlyList<PartyMember>, Outcome)` 심(seam)
- Produces: 검증된 되반영 규칙 — 이후 Unity 흐름 계획이 `ApplyResult(run, session)` 오버로드를 호출한다

- [ ] **Step 1: 실패하는(또는 이미 통과하는) 테스트 작성 (추가)**

```csharp
private static PartyMember CombatMember(string id, string name, int maxHp, int hp)
{
    var member = new PartyMember(id, name, maxHp);
    member.Hp = hp;
    return member;
}

[Test]
public void ApplyResult_WritesSurvivorHp_AndRemovesDeadWithTheirCards()
{
    var run = RunWithCombatNode(seed: 7, out _);
    var combatParty = new[]
    {
        CombatMember("member_a", "파티원 A", 25, 12),
        CombatMember("member_b", "파티원 B", 25, 0)
    };

    var result = NormalHandler().ApplyResult(run, combatParty, Outcome.Win);

    Assert.That(run.Party.Count, Is.EqualTo(1));
    Assert.That(run.Party[0].Id, Is.EqualTo("member_a"));
    Assert.That(run.Party[0].Hp, Is.EqualTo(12));
    Assert.That(result.DeadMemberIds, Is.EqualTo(new[] { "member_b" }));
    Assert.That(run.Outcome, Is.EqualTo(RunOutcome.InProgress)); // 일반 노드 승리는 런을 끝내지 않는다
}

[Test]
public void ApplyResult_BossWin_SetsVictory()
{
    var run = RunWithCombatNode(seed: 7, out _);
    var boss = new CombatNodeHandler(
        RunNodeKeys.BossCombat, isBossNode: true, EnemyKinds.Default(), fateEnergyPerTurn: 3);

    boss.ApplyResult(run, new[] { CombatMember("member_a", "파티원 A", 25, 3) }, Outcome.Win);

    Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Victory));
}

[Test]
public void ApplyResult_WipeOrLoss_SetsDefeat()
{
    var run = RunWithCombatNode(seed: 7, out _);
    run.Party.RemoveAt(1); // member_a만 남김

    NormalHandler().ApplyResult(
        run, new[] { CombatMember("member_a", "파티원 A", 25, 0) }, Outcome.Lose);

    Assert.That(run.Party.Count, Is.EqualTo(0));
    Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Defeat));
}
```

주의: `PartyMember` 생성자가 `(id, name, maxHp, surviveCharges = 0)`이므로 `CombatMember` 헬퍼가 그대로 컴파일되는지 확인한다 ([Assets/Core/Combat/PartyMember.cs](../../Assets/Core/Combat/PartyMember.cs)).

- [ ] **Step 2: 실행해 결과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter CombatNodeHandlerTests`
Expected: Task 3에서 구현을 미리 작성했으므로 PASS가 정상. 실패하면 구현을 테스트에 맞게 수정한다 (테스트가 스펙이다).

- [ ] **Step 3: 전체 테스트 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 PASS

- [ ] **Step 4: `.meta` 생성 후 커밋**

Unity `-batchmode` 1회 실행으로 새 파일 `.meta` 생성·스테이징 (규칙 16·17).

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/CombatNodeHandlerTests.cs
git commit -m "test(run): verify combat result write-back, deaths, and run outcome rules"
```

---

## 완료 기준

- 전체 헤드리스 테스트 통과.
- 머지는 사용자 승인 후 (규칙 19). 머지 순서는 [플랜 인덱스](2026-07-20-run-cycle-plan-index.md) 참고 — 고용·회복/보상 계획과는 순서 무관하게 병렬 머지 가능(파일 겹침 없음).
