# 런 코어 기반(Run Core Foundation) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 런 원 사이클의 공유 기반 — `RunState`·`RunDefinition`·노드 키/레지스트리 — 를 순수 C#으로 만든다. 다른 4개 계획(전투 노드, 고용·회복 노드, 전투 보상, Unity 흐름)이 전부 이 계획의 산출 타입에 의존하므로 **가장 먼저 구현·머지해야 한다**.

**Architecture:** 스펙 [2026-07-20-run-cycle-skeleton-design.md](../specs/2026-07-20-run-cycle-skeleton-design.md) §3.1~3.3. 런 레이어는 `Assets/Core/Simulation/Run/`(asmdef `FateWeaver.Simulation`, 네임스페이스 `FateWeaver.Simulation.Run`)에 둔다 — `DeckCombatSession` 등 시뮬레이션 타입을 쓰기 때문에 `FateWeaver.Core`가 아니라 Simulation 어셈블리다. UnityEngine 참조 금지는 동일하게 적용된다(헤드리스 csproj가 이 폴더를 컴파일한다).

**Tech Stack:** C# 9 (Unity 6 호환), NUnit, `dotnet test` 헤드리스 하니스.

## Global Constraints

- 헤드리스 테스트 실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0` (로컬 SDK가 .NET 5뿐이라 TargetFramework 오버라이드 필수)
- LangVersion 9 — `record struct`, file-scoped namespace 금지. `init`, `??=`는 가능.
- UnityEngine 참조 금지 (`Assets/Core/**`는 헤드리스로 컴파일된다)
- 무작위는 전부 `RunState.Rng` 경유 (AGENTS.md 규칙 7). `System.Random` 즉석 생성·`DateTime`·`Guid.NewGuid()` 금지.
- 튜닝 수치 하드코딩 금지 (규칙 8) — 수치는 생성자/데이터로 받는다.
- 작업은 전용 워크트리에서: `git worktree add ../rogue-deck-run-core -b feat/run-core`
- `Assets/` 아래 새 파일의 `.meta`는 병합 전 Unity `-batchmode` 1회 실행으로 생성해 함께 커밋한다 (규칙 16·17). 실행은 워크트리를 `-projectPath`로 사용.
- 테스트 파일은 `Assets/Core/Tests/EditMode/`에, 네임스페이스는 기존 관례대로 `FateWeaver.Tests`.

---

### Task 1: 노드 키·페이로드·RunDefinition 데이터 타입

**Files:**
- Create: `Assets/Core/Simulation/Run/RunNodeKey.cs`
- Create: `Assets/Core/Simulation/Run/IRunNodePayload.cs`
- Create: `Assets/Core/Simulation/Run/RunNodeData.cs`
- Create: `Assets/Core/Simulation/Run/RunDefinition.cs`
- Test: `Assets/Core/Tests/EditMode/RunNodeKeyTests.cs`

**Interfaces:**
- Consumes: 없음 (기반 타입)
- Produces: `RunNodeKey`(값 타입 키), `RunNodeKeys.{NormalCombat, EliteCombat, BossCombat, RecruitHeal}`, `IRunNodePayload`(마커), `RunNodeData { RunNodeKey Key; IRunNodePayload Payload; }`, `RunDefinition { IReadOnlyList<RunNodeData> Nodes; }`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunNodeKeyTests
    {
        [Test]
        public void SameId_AreEqual()
        {
            Assert.That(new RunNodeKey("combat_normal"), Is.EqualTo(RunNodeKeys.NormalCombat));
            Assert.That(RunNodeKeys.NormalCombat == new RunNodeKey("combat_normal"), Is.True);
            Assert.That(RunNodeKeys.NormalCombat != RunNodeKeys.BossCombat, Is.True);
        }

        [Test]
        public void RunDefinition_ExposesNodesInOrder()
        {
            var nodes = new[]
            {
                new RunNodeData(RunNodeKeys.NormalCombat, null),
                new RunNodeData(RunNodeKeys.RecruitHeal, null)
            };
            var definition = new RunDefinition(nodes);
            Assert.That(definition.Nodes.Count, Is.EqualTo(2));
            Assert.That(definition.Nodes[1].Key, Is.EqualTo(RunNodeKeys.RecruitHeal));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunNodeKeyTests`
Expected: 컴파일 에러 (`RunNodeKey` 미정의)

- [ ] **Step 3: 구현**

`RunNodeKey.cs` — 기존 `EffectKey`([Assets/Core/Effects/EffectKey.cs](../../Assets/Core/Effects/EffectKey.cs))와 동일 패턴:

```csharp
using System;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Typed wrapper over a run-map node type id (open set, type-safe) —
    /// same pattern as EffectKey. Plain readonly struct to stay within Unity 6's C# 9.</summary>
    public readonly struct RunNodeKey : IEquatable<RunNodeKey>
    {
        public string Id { get; }

        public RunNodeKey(string id) => Id = id;

        public bool Equals(RunNodeKey other) => Id == other.Id;
        public override bool Equals(object obj) => obj is RunNodeKey other && Equals(other);
        public override int GetHashCode() => Id == null ? 0 : Id.GetHashCode();
        public override string ToString() => Id;

        public static bool operator ==(RunNodeKey a, RunNodeKey b) => a.Equals(b);
        public static bool operator !=(RunNodeKey a, RunNodeKey b) => !a.Equals(b);
    }

    public static class RunNodeKeys
    {
        public static readonly RunNodeKey NormalCombat = new RunNodeKey("combat_normal");
        public static readonly RunNodeKey EliteCombat = new RunNodeKey("combat_elite");
        public static readonly RunNodeKey BossCombat = new RunNodeKey("combat_boss");
        public static readonly RunNodeKey RecruitHeal = new RunNodeKey("recruit_heal");
    }
}
```

`IRunNodePayload.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>Marker for per-node authored data (encounter refs, recruit candidates, …).
    /// Concrete payloads live with their node handlers (CombatNodePayload, RecruitHealPayload).</summary>
    public interface IRunNodePayload
    {
    }
}
```

`RunNodeData.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>One authored node on the linear run map: a node type key plus that type's payload.</summary>
    public sealed class RunNodeData
    {
        public RunNodeKey Key { get; }
        public IRunNodePayload Payload { get; }

        public RunNodeData(RunNodeKey key, IRunNodePayload payload)
        {
            Key = key;
            Payload = payload;
        }
    }
}
```

`RunDefinition.cs`:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Fixed linear node sequence for one run. Authored in the Unity layer (SO) and
    /// converted to this pure data on load, like the card SO pipeline.</summary>
    public sealed class RunDefinition
    {
        public IReadOnlyList<RunNodeData> Nodes { get; }

        public RunDefinition(IReadOnlyList<RunNodeData> nodes) => Nodes = nodes;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunNodeKeyTests`
Expected: PASS (2 tests)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/RunNodeKeyTests.cs
git commit -m "feat(run): add run node key, payload marker, and RunDefinition data types"
```

---

### Task 2: RunMember·RunOutcome·RunState

**Files:**
- Create: `Assets/Core/Simulation/Run/RunMember.cs`
- Create: `Assets/Core/Simulation/Run/RunOutcome.cs`
- Create: `Assets/Core/Simulation/Run/RunState.cs`
- Test: `Assets/Core/Tests/EditMode/RunStateTests.cs`

**Interfaces:**
- Consumes: Task 1의 `RunDefinition`, `RunNodeData`; 기존 `PartyTuning`([Assets/Core/Simulation/PartyTuning.cs](../../Assets/Core/Simulation/PartyTuning.cs)), `CardDefinition`
- Produces:
  - `RunMember(string id, string name, int maxHp, IEnumerable<CardDefinition> cards)` — `Hp`(set 가능, 생성 시 MaxHp), `List<CardDefinition> Cards`, `bool IsAlive`
  - `enum RunOutcome { InProgress, Victory, Defeat }`
  - `RunState(RunDefinition definition, IReadOnlyList<RunMember> startingParty, PartyTuning tuning, int runSeed)` — `Nodes`, `CurrentNodeIndex`, `CurrentNode`, `List<RunMember> Party`, `PartyTuning Tuning`, `RunOutcome Outcome`, `Random Rng`, `IReadOnlyList<RunMember> LivingMembers`, `int NextCombatSeed()`, `bool AdvanceToNextNode()`, `void SetOutcome(RunOutcome)`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunStateTests
    {
        private static RunDefinition TwoNodes() => new RunDefinition(new[]
        {
            new RunNodeData(RunNodeKeys.NormalCombat, null),
            new RunNodeData(RunNodeKeys.BossCombat, null)
        });

        private static RunState NewRun(int seed) => new RunState(
            TwoNodes(),
            new[] { new RunMember("member_a", "파티원 A", PartyTuning.Prototype.DefaultMemberMaxHp, null) },
            PartyTuning.Prototype,
            seed);

        [Test]
        public void SameSeed_ProducesSameCombatSeedSequence()
        {
            var a = NewRun(seed: 41);
            var b = NewRun(seed: 41);
            Assert.That(a.NextCombatSeed(), Is.EqualTo(b.NextCombatSeed()));
            Assert.That(a.NextCombatSeed(), Is.EqualTo(b.NextCombatSeed()));
        }

        [Test]
        public void Advance_WalksNodesAndStopsAtEnd()
        {
            var run = NewRun(seed: 1);
            Assert.That(run.CurrentNode.Key, Is.EqualTo(RunNodeKeys.NormalCombat));
            Assert.That(run.AdvanceToNextNode(), Is.True);
            Assert.That(run.CurrentNode.Key, Is.EqualTo(RunNodeKeys.BossCombat));
            Assert.That(run.AdvanceToNextNode(), Is.False);
            Assert.That(run.CurrentNodeIndex, Is.EqualTo(1));
        }

        [Test]
        public void LivingMembers_ExcludesDead()
        {
            var run = NewRun(seed: 1);
            run.Party.Add(new RunMember("member_b", "파티원 B", PartyTuning.Prototype.DefaultMemberMaxHp, null));
            run.Party[0].Hp = 0;
            Assert.That(run.LivingMembers.Count, Is.EqualTo(1));
            Assert.That(run.LivingMembers[0].Id, Is.EqualTo("member_b"));
        }

        [Test]
        public void Outcome_StartsInProgress_AndIsSettable()
        {
            var run = NewRun(seed: 1);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.InProgress));
            run.SetOutcome(RunOutcome.Victory);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Victory));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunStateTests`
Expected: 컴파일 에러 (`RunState` 미정의)

- [ ] **Step 3: 구현**

`RunOutcome.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    public enum RunOutcome { InProgress, Victory, Defeat }
}
```

`RunMember.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>One party member's run-persistent state: HP carried between combats and the cards
    /// this character owns (party-foundation rule: every card belongs to a character).</summary>
    public sealed class RunMember
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Hp { get; set; }
        public List<CardDefinition> Cards { get; } = new();
        public bool IsAlive => Hp > 0;

        public RunMember(string id, string name, int maxHp, IEnumerable<CardDefinition> cards)
        {
            Id = id;
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            if (cards != null)
            {
                Cards.AddRange(cards);
            }
        }
    }
}
```

`RunState.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Run-persistent state between combats: node progress, party, seeded run-level RNG.
    /// All run-level randomness (combat seed derivation, reward rolls) must go through Rng
    /// (AGENTS.md rule 7) so the same run seed replays the same run.</summary>
    public sealed class RunState
    {
        private Random _rng;

        public RunState(
            RunDefinition definition,
            IReadOnlyList<RunMember> startingParty,
            PartyTuning tuning,
            int runSeed)
        {
            Nodes = definition.Nodes;
            Party = new List<RunMember>(startingParty);
            Tuning = tuning;
            RunSeed = runSeed;
        }

        public IReadOnlyList<RunNodeData> Nodes { get; }
        public int CurrentNodeIndex { get; private set; }
        public RunNodeData CurrentNode => Nodes[CurrentNodeIndex];
        public List<RunMember> Party { get; }
        public PartyTuning Tuning { get; }
        public RunOutcome Outcome { get; private set; } = RunOutcome.InProgress;
        public int RunSeed { get; }

        /// <summary>Seeded run-level RNG (lazy, same pattern as CombatState.Rng).</summary>
        public Random Rng => _rng ??= new Random(RunSeed);

        public IReadOnlyList<RunMember> LivingMembers => Party.Where(m => m.IsAlive).ToList();

        /// <summary>Draws the next combat's seed from the run RNG —
        /// same run seed ⇒ same combat seed sequence (spec §3.1).</summary>
        public int NextCombatSeed() => Rng.Next();

        /// <summary>Returns false when already on the last node.</summary>
        public bool AdvanceToNextNode()
        {
            if (CurrentNodeIndex + 1 >= Nodes.Count)
            {
                return false;
            }

            CurrentNodeIndex++;
            return true;
        }

        public void SetOutcome(RunOutcome outcome) => Outcome = outcome;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunStateTests`
Expected: PASS (4 tests)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/RunStateTests.cs
git commit -m "feat(run): add RunState, RunMember, RunOutcome with seeded run RNG"
```

---

### Task 3: 노드 핸들러 레지스트리 + 부팅 검증

**Files:**
- Create: `Assets/Core/Simulation/Run/IRunNodeHandler.cs`
- Create: `Assets/Core/Simulation/Run/RunNodeRegistry.cs`
- Create: `Assets/Core/Simulation/Run/RunDefinitionValidator.cs`
- Test: `Assets/Core/Tests/EditMode/RunNodeRegistryTests.cs`

**Interfaces:**
- Consumes: Task 1의 `RunNodeKey`, `RunNodeData`, `RunDefinition`
- Produces:
  - `IRunNodeHandler { RunNodeKey Key { get; } }` — 전투/고용·회복 핸들러(별도 계획)가 구현
  - `RunNodeRegistry { void Register(IRunNodeHandler); bool Contains(RunNodeKey); IRunNodeHandler Resolve(RunNodeKey); }`
  - `RunDefinitionValidator.Validate(RunDefinition, RunNodeRegistry) → IReadOnlyList<string>` (빈 목록 = 통과)
- 참고: 핸들러들을 실제로 등록하는 `RunRegistries` 부팅 코드는 핸들러 계획들이 머지된 뒤 **Unity 흐름 계획(run-unity-flow)** 에서 만든다. 이 계획은 등록 인프라만 제공한다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RunNodeRegistryTests
    {
        private sealed class FakeHandler : IRunNodeHandler
        {
            public FakeHandler(RunNodeKey key) => Key = key;
            public RunNodeKey Key { get; }
        }

        [Test]
        public void Resolve_ReturnsRegisteredHandler_AndThrowsOnUnknown()
        {
            var registry = new RunNodeRegistry();
            var handler = new FakeHandler(RunNodeKeys.RecruitHeal);
            registry.Register(handler);

            Assert.That(registry.Contains(RunNodeKeys.RecruitHeal), Is.True);
            Assert.That(registry.Resolve(RunNodeKeys.RecruitHeal), Is.SameAs(handler));
            Assert.Throws<KeyNotFoundException>(() => registry.Resolve(RunNodeKeys.BossCombat));
        }

        [Test]
        public void Validator_FlagsUnregisteredKeyAndNullPayload()
        {
            var registry = new RunNodeRegistry();
            registry.Register(new FakeHandler(RunNodeKeys.NormalCombat));

            var definition = new RunDefinition(new[]
            {
                new RunNodeData(RunNodeKeys.NormalCombat, null),      // payload 없음 → 에러
                new RunNodeData(RunNodeKeys.RecruitHeal, new DummyPayload()) // 핸들러 미등록 → 에러
            });

            var errors = RunDefinitionValidator.Validate(definition, registry);
            Assert.That(errors.Count, Is.EqualTo(2));
        }

        private sealed class DummyPayload : IRunNodePayload
        {
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunNodeRegistryTests`
Expected: 컴파일 에러

- [ ] **Step 3: 구현**

`IRunNodeHandler.cs`:

```csharp
namespace FateWeaver.Simulation.Run
{
    /// <summary>A run-map node type handler. New node types extend the run by adding one handler
    /// and registering its key (AGENTS.md rule 9) — never by growing a central switch.
    /// Concrete handlers expose their own entry points (an instant Resolve, or a combat
    /// CreateSession/ApplyResult pair); callers resolve by key and use the concrete type.</summary>
    public interface IRunNodeHandler
    {
        RunNodeKey Key { get; }
    }
}
```

`RunNodeRegistry.cs` — 기존 `EffectRegistry`와 동일 패턴:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    public sealed class RunNodeRegistry
    {
        private readonly Dictionary<RunNodeKey, IRunNodeHandler> _handlers = new();

        public void Register(IRunNodeHandler handler) => _handlers[handler.Key] = handler;

        public bool Contains(RunNodeKey key) => _handlers.ContainsKey(key);

        public IRunNodeHandler Resolve(RunNodeKey key)
            => _handlers.TryGetValue(key, out var h)
                ? h
                : throw new KeyNotFoundException($"No run node handler registered for '{key}'");
    }
}
```

`RunDefinitionValidator.cs`:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Boot-time validation (AGENTS.md rule 9): every authored node needs a registered
    /// handler and a payload. Returns an empty list when the definition is valid.</summary>
    public static class RunDefinitionValidator
    {
        public static IReadOnlyList<string> Validate(RunDefinition definition, RunNodeRegistry registry)
        {
            var errors = new List<string>();
            for (int i = 0; i < definition.Nodes.Count; i++)
            {
                var node = definition.Nodes[i];
                if (!registry.Contains(node.Key))
                {
                    errors.Add($"node[{i}]: no handler registered for key '{node.Key}'");
                }

                if (node.Payload == null)
                {
                    errors.Add($"node[{i}] ('{node.Key}'): payload is null");
                }
            }

            return errors;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RunNodeRegistryTests`
Expected: PASS (2 tests). 이어서 전체 테스트도 확인: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0` → 전부 PASS.

- [ ] **Step 5: `.meta` 생성 후 커밋**

워크트리에서 Unity `-batchmode`를 1회 실행해 새 파일들의 `.meta`를 생성하고 함께 스테이징한다 (명령·라이선스 이슈는 [메모리: unity-licensing-client-zombie] 참고 — 라이선스 505 거부 메시지는 정상).

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/RunNodeRegistryTests.cs
git commit -m "feat(run): add run node handler registry and boot validation"
```

---

## 완료 기준

- 전체 헤드리스 테스트 통과.
- 이 브랜치가 master에 머지되어야 나머지 4개 계획이 시작할 수 있다 (머지는 사용자 승인 후 — 규칙 19).
