# 고용·회복 노드(Recruit/Heal Node) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 런의 고용·회복 노드 — 생존 파티원 회복(회복량은 데이터) + 후보 캐릭터의 기본 키트 합류(파티에 자리가 있을 때, 무료)를 즉시 처리하는 핸들러를 만든다.

**Architecture:** 스펙 [2026-07-20-run-cycle-skeleton-design.md](../specs/2026-07-20-run-cycle-skeleton-design.md) §3.3. `RecruitHealNodeHandler`는 `IRunNodeHandler`(run-core-foundation 산출)를 구현하는 **즉시형** 핸들러다 — 전투 노드와 달리 `Resolve(run, node)` 한 번으로 끝난다. 경력 드래프트·재화는 범위 외(스펙 §6).

**Tech Stack:** C# 9, NUnit, `dotnet test` 헤드리스 하니스.

## Global Constraints

- **선행 조건: `feat/run-core`(run-core-foundation 계획)가 master에 머지된 뒤 시작한다.**
- 헤드리스 테스트 실행: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
- LangVersion 9. UnityEngine 참조 금지 (`Assets/Core/**`).
- 튜닝 수치 하드코딩 금지 (규칙 8) — 회복량은 페이로드 데이터(`HealPercent`), 신입 HP·파티 상한은 `RunState.Tuning` 경유.
- 작업은 전용 워크트리에서: `git worktree add ../rogue-deck-run-recruit-heal -b feat/run-recruit-heal-node`
- `Assets/` 아래 새 파일의 `.meta`는 병합 전 Unity `-batchmode` 1회 실행으로 생성해 함께 커밋 (규칙 16·17).
- 병렬 주의: 이 계획이 만드는 파일은 아래 3개뿐이며 다른 병렬 계획과 겹치지 않는다.

---

### Task 1: RecruitCandidate·RecruitHealPayload 데이터

**Files:**
- Create: `Assets/Core/Simulation/Run/RecruitHealPayload.cs`
- Test: `Assets/Core/Tests/EditMode/RecruitHealNodeHandlerTests.cs` (신규 — Task 2가 같은 파일에 테스트 추가)

**Interfaces:**
- Consumes: `IRunNodePayload`(foundation), `CardDefinition`
- Produces:
  - `RecruitCandidate(string id, string name, IReadOnlyList<CardDefinition> baseKit)` — `Id`, `Name`, `BaseKit`
  - `RecruitHealPayload(RecruitCandidate candidate, int healPercent) : IRunNodePayload` — `Candidate`(null 허용 = 순수 회복 노드), `HealPercent`(MaxHp 대비 %, 100 = 전체 회복)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Run;

namespace FateWeaver.Tests
{
    public class RecruitHealNodeHandlerTests
    {
        [Test]
        public void Payload_HoldsCandidateAndHealPercent()
        {
            var candidate = new RecruitCandidate("member_b", "파티원 B", PartyPrototypeDeck.Build());
            var payload = new RecruitHealPayload(candidate, healPercent: 100);
            Assert.That(payload.Candidate.Id, Is.EqualTo("member_b"));
            Assert.That(payload.HealPercent, Is.EqualTo(100));

            var healOnly = new RecruitHealPayload(null, healPercent: 50);
            Assert.That(healOnly.Candidate, Is.Null);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RecruitHealNodeHandlerTests`
Expected: 컴파일 에러

- [ ] **Step 3: 구현**

`RecruitHealPayload.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Run
{
    /// <summary>A recruitable character: joins with a fixed base kit. Career-draft recruiting is
    /// out of scope for the run-cycle skeleton (spec §6).</summary>
    public sealed class RecruitCandidate
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<CardDefinition> BaseKit { get; }

        public RecruitCandidate(string id, string name, IReadOnlyList<CardDefinition> baseKit)
        {
            Id = id;
            Name = name;
            BaseKit = baseKit;
        }
    }

    /// <summary>Recruit/heal node payload. Candidate may be null — a pure heal stop.
    /// HealPercent is the authored heal amount as a percentage of each member's MaxHp
    /// (100 = full heal; tuning data, AGENTS.md rule 8).</summary>
    public sealed class RecruitHealPayload : IRunNodePayload
    {
        public RecruitCandidate Candidate { get; }
        public int HealPercent { get; }

        public RecruitHealPayload(RecruitCandidate candidate, int healPercent)
        {
            Candidate = candidate;
            HealPercent = healPercent;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RecruitHealNodeHandlerTests`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Run/RecruitHealPayload.cs Assets/Core/Tests/EditMode/RecruitHealNodeHandlerTests.cs
git commit -m "feat(run): add recruit candidate and recruit/heal payload data"
```

---

### Task 2: RecruitHealNodeHandler

**Files:**
- Create: `Assets/Core/Simulation/Run/RecruitHealNodeHandler.cs`
- Test: `Assets/Core/Tests/EditMode/RecruitHealNodeHandlerTests.cs` (테스트 추가)

**Interfaces:**
- Consumes: foundation의 `RunState`·`RunMember`·`RunNodeData`·`IRunNodeHandler`·`RunNodeKeys.RecruitHeal`, Task 1의 페이로드, `PartyTuning.MaxPartySize`·`DefaultMemberMaxHp`
- Produces:
  - `RecruitHealNodeHandler() : IRunNodeHandler` — `Key == RunNodeKeys.RecruitHeal`
  - `RecruitHealResult Resolve(RunState run, RunNodeData node)` — `RecruitedMemberId`(null = 미고용), `IReadOnlyDictionary<string,int> HealedByMemberId`(멤버별 실제 회복량)
- 규칙 (테스트가 스펙):
  1. 회복 먼저, 고용 나중 — 신입은 만피로 합류하므로 회복 대상이 아니다.
  2. 회복량 = `MaxHp * HealPercent / 100`, 결과는 MaxHp를 넘지 않는다.
  3. 사망자(Hp 0)는 회복하지 않는다 — 부활 없음 (party-foundation 죽음 규칙).
  4. 고용 조건: 후보 존재 + `Party.Count < Tuning.MaxPartySize` + 동일 Id 부재. 신입 MaxHp는 `Tuning.DefaultMemberMaxHp`, 카드는 BaseKit 복사.

- [ ] **Step 1: 실패하는 테스트 작성 (추가)**

```csharp
private static RunNodeData Node(RecruitCandidate candidate, int healPercent) => new RunNodeData(
    RunNodeKeys.RecruitHeal, new RecruitHealPayload(candidate, healPercent));

private static RunState OneMemberRun()
{
    var tuning = PartyTuning.Prototype;
    var definition = new RunDefinition(new[] { Node(null, 100) });
    return new RunState(
        definition,
        new[] { new RunMember("member_a", "파티원 A", tuning.DefaultMemberMaxHp, StarterDeck.Build()) },
        tuning,
        seed: 1);
}

[Test]
public void Resolve_HealsLivingMembers_CappedAtMaxHp()
{
    var run = OneMemberRun();
    run.Party[0].Hp = 20; // MaxHp 25 기준 5만 회복 가능

    var result = new RecruitHealNodeHandler().Resolve(run, Node(null, 100));

    Assert.That(run.Party[0].Hp, Is.EqualTo(run.Party[0].MaxHp));
    Assert.That(result.HealedByMemberId["member_a"], Is.EqualTo(5));
    Assert.That(result.RecruitedMemberId, Is.Null);
}

[Test]
public void Resolve_DoesNotHealOrReviveDead()
{
    var run = OneMemberRun();
    run.Party.Add(new RunMember("member_b", "파티원 B", 25, null));
    run.Party[1].Hp = 0;

    var result = new RecruitHealNodeHandler().Resolve(run, Node(null, 100));

    Assert.That(run.Party[1].Hp, Is.EqualTo(0));
    Assert.That(result.HealedByMemberId.ContainsKey("member_b"), Is.False);
}

[Test]
public void Resolve_RecruitsCandidateWithBaseKit_AtFullHp()
{
    var run = OneMemberRun();
    run.Party[0].Hp = 10;
    var kit = PartyPrototypeDeck.Build();

    var result = new RecruitHealNodeHandler().Resolve(
        run, Node(new RecruitCandidate("member_b", "파티원 B", kit), 100));

    Assert.That(result.RecruitedMemberId, Is.EqualTo("member_b"));
    Assert.That(run.Party.Count, Is.EqualTo(2));
    var recruit = run.Party[1];
    Assert.That(recruit.MaxHp, Is.EqualTo(run.Tuning.DefaultMemberMaxHp));
    Assert.That(recruit.Hp, Is.EqualTo(recruit.MaxHp));
    Assert.That(recruit.Cards.Count, Is.EqualTo(kit.Count));
    Assert.That(result.HealedByMemberId.ContainsKey("member_b"), Is.False); // 회복 먼저, 고용 나중
}

[Test]
public void Resolve_SkipsRecruit_WhenPartyFullOrDuplicate()
{
    var run = OneMemberRun();
    run.Party.Add(new RunMember("member_b", "파티원 B", 25, null));
    run.Party.Add(new RunMember("member_c", "파티원 C", 25, null)); // MaxPartySize = 3 도달

    var full = new RecruitHealNodeHandler().Resolve(
        run, Node(new RecruitCandidate("member_d", "파티원 D", StarterDeck.Build()), 100));
    Assert.That(full.RecruitedMemberId, Is.Null);
    Assert.That(run.Party.Count, Is.EqualTo(3));

    run.Party.RemoveAt(2);
    var duplicate = new RecruitHealNodeHandler().Resolve(
        run, Node(new RecruitCandidate("member_b", "파티원 B", StarterDeck.Build()), 100));
    Assert.That(duplicate.RecruitedMemberId, Is.Null);
    Assert.That(run.Party.Count, Is.EqualTo(2));
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter RecruitHealNodeHandlerTests`
Expected: 컴파일 에러 (`RecruitHealNodeHandler` 미정의)

- [ ] **Step 3: 구현**

`RecruitHealNodeHandler.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Simulation.Run
{
    public sealed class RecruitHealResult
    {
        public string RecruitedMemberId { get; }
        public IReadOnlyDictionary<string, int> HealedByMemberId { get; }

        public RecruitHealResult(string recruitedMemberId, IReadOnlyDictionary<string, int> healedByMemberId)
        {
            RecruitedMemberId = recruitedMemberId;
            HealedByMemberId = healedByMemberId;
        }
    }

    /// <summary>Instant node: heals every living member by HealPercent of MaxHp (capped, dead stay
    /// dead — no revival), then recruits the candidate with their base kit when the party has room
    /// and the id is not already present. Free — the skeleton has no currency. Heal runs first so
    /// the recruit (joining at full HP) is never double-healed.</summary>
    public sealed class RecruitHealNodeHandler : IRunNodeHandler
    {
        private const int PercentDenominator = 100;

        public RunNodeKey Key => RunNodeKeys.RecruitHeal;

        public RecruitHealResult Resolve(RunState run, RunNodeData node)
        {
            var payload = (RecruitHealPayload)node.Payload;

            var healed = new Dictionary<string, int>();
            foreach (var member in run.Party.Where(m => m.IsAlive))
            {
                int amount = member.MaxHp * payload.HealPercent / PercentDenominator;
                int before = member.Hp;
                member.Hp = Math.Min(member.MaxHp, member.Hp + amount);
                healed[member.Id] = member.Hp - before;
            }

            string recruitedId = null;
            var candidate = payload.Candidate;
            if (candidate != null
                && run.Party.Count < run.Tuning.MaxPartySize
                && run.Party.All(m => m.Id != candidate.Id))
            {
                run.Party.Add(new RunMember(
                    candidate.Id, candidate.Name, run.Tuning.DefaultMemberMaxHp, candidate.BaseKit));
                recruitedId = candidate.Id;
            }

            return new RecruitHealResult(recruitedId, healed);
        }
    }
}
```

- [ ] **Step 4: 전체 테스트 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전부 PASS

- [ ] **Step 5: `.meta` 생성 후 커밋**

Unity `-batchmode` 1회 실행으로 새 파일 `.meta` 생성·스테이징 (규칙 16·17).

```bash
git add Assets/Core/Simulation/Run/ Assets/Core/Tests/EditMode/RecruitHealNodeHandlerTests.cs
git commit -m "feat(run): recruit/heal node handler with capped heal and base-kit recruiting"
```

---

## 완료 기준

- 전체 헤드리스 테스트 통과.
- 머지는 사용자 승인 후 (규칙 19). 전투 노드/보상 계획과 파일 겹침 없음 — 병렬 머지 가능.
