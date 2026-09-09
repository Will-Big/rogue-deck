# 상태 훅 확장·독 시스템·시작 카드 풀 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 날짜: 2026-07-29
- 상태: `active`
- 근거 스펙: [캐릭터 및 카드풀 설계 규칙 §3](../../specs/2026-07-20-character-card-pools-design.md) (독 규칙),
  [위치 대상과 카드 텍스트](../../specs/2026-07-27-position-targeting-card-text-design.md),
  [대상 선택 메타데이터 P0-C](../../specs/2026-07-28-p0c-targeting-metadata-design.md)
- 카드 원본: `Tools/card-idea-notebook/시작 카드 풀.md` (22장)

**Goal:** 상태이상마다 서로 다른 행동(턴 종료 발동, 사망 반응, 중첩 규칙)을 레지스트리 훅으로 구현할 수 있게 `IStatusBehavior` 표면을 확장하고, 그 위에 독 시스템과 신규 효과·조건·대상 선택·개입 제약을 얹어 시작 카드 풀 22장을 헤드리스로 동작시킨다.

**Architecture:** 모든 확장은 기존 레지스트리 패턴(핸들러 클래스 1개 + 키 등록, AGENTS.md 규칙 9)을 따른다. 상태 행동은 `IStatusBehavior`에 훅 3개(`StacksMagnitude`, `OnTurnEnd`, `OnHolderDied`)를 추가해 표현하고, TurnResolver가 훅을 배선하며 결과는 이벤트 타임라인으로만 나간다(규칙 11). 카드 콘텐츠는 `StarterDeckSpecs` 전례를 따라 순수 C# `CardSpec` 팩토리로 저작한다(SO 미러링은 병합 후 메인 체크아웃에서 별도 진행).

**Tech Stack:** 순수 C# (LangVersion 9, UnityEngine 참조 금지), NUnit, `dotnet test` 헤드리스 하니스.

## Global Constraints

- 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0` (로컬 SDK가 .NET 5뿐이라 TargetFramework 재정의 필수)
- LangVersion 9: `record struct`, file-scoped namespace 사용 금지. `record`(class), `init`, `with`는 사용 가능
- `FateWeaver.Core`에서 UnityEngine 참조 금지 (규칙 6)
- 무작위는 `CombatState.Rng`만 사용 (규칙 7)
- 튜닝 수치는 데이터/생성자 파라미터로 (규칙 8). 독 성장량 +1은 §3.2의 규칙 수치이며 등록 시점에 명명 파라미터로 주입
- 독 규칙 권위: 카드풀 스펙 §3.2 — "행동 턴 종료에 X 피해를 주고 독이 1 증가. 이번 턴에 새로 부여된 독도 이번 턴 종료에 발동. 이미 사망한 대상은 발동에서 제외"
- **방어 중첩 = 합산** (2026-07-29 사용자 확정, 스펙 §3.1의 미결 항목 해소). 독도 합산
- 독 틱 피해는 `ModifyIncomingDamage`(방어/취약)를 경유하지 않는 직접 피해
- 새 효과·상태는 설명 핸들러를 `KoreanDescriptionCatalog.CreateDefault`에 함께 등록 (규칙 10)
- 커밋 메시지 접두사: `feat(core):`, `test(core):`, `docs:`

## 최종 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Core/Status/IStatusBehavior.cs` (수정) | 훅 표면: `StacksMagnitude`/`OnTurnEnd`/`OnHolderDied` + `StatusTickContext`/`StatusDeathContext` |
| `Assets/Core/Status/StatusBag.cs` (수정) | `Stack()` — 수치 합산 적용 |
| `Assets/Core/Status/PoisonBehavior.cs` (신규) | 독 §3.2 기본 규칙 + 잠복/안정 마커 존중 |
| `Assets/Core/Status/PoisonDormantBehavior.cs`, `PoisonStasisBehavior.cs` (신규) | 변이형 마커(발동 금지 / 성장 금지) |
| `Assets/Core/Status/ContagionBehavior.cs` (신규) | 사망 시 독 이전 |
| `Assets/Core/Combat/TurnResolver.cs` (수정) | 턴 종료 틱 파이프라인, 적 사망 스윕, 훅 디스패치, SkipOnBasic |
| `Assets/Core/Combat/EnemyTargeting.cs` (신규) | 적 대형 위치 선택 (아군 `PartyTargeting`의 대칭) |
| `Assets/Core/Events/ResolutionEvent.cs` (수정) | `StatusTicked`/`EnemyDied`/`StatusTransferred` |
| `Assets/Core/Effects/ConsumeStatusHandler.cs`, `TriggerStatusHandler.cs`, `GrantNextTurnFateHandler.cs` (신규) | 소비형/변이형/운명력 효과 |
| `Assets/Core/Simulation/Authoring/Specs/…` (신규 3종) | 위 효과의 저작 스펙 |
| `Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs` (신규) | 시작 카드 풀 22장 |

---

### Task 1: 상태 훅 표면 확장 + StatusBag.Stack

**Files:**
- Modify: `Assets/Core/Status/IStatusBehavior.cs`
- Modify: `Assets/Core/Status/StatusBag.cs`
- Test: `Assets/Core/Tests/EditMode/StatusHookSurfaceTests.cs` (신규)

**Interfaces:**
- Consumes: 기존 `StatusInstance`, `StatusLifetime`, `ResolutionEvent`
- Produces: `IStatusBehavior.StacksMagnitude : bool`, `OnTurnEnd(StatusTickContext)`, `OnHolderDied(StatusDeathContext)`, `StatusBag.Stack(StatusKey, StatusLifetime, int) : StatusInstance`. 이후 모든 태스크가 이 시그니처를 사용한다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class StatusHookSurfaceTests
    {
        private sealed class InertBehavior : StatusBehavior
        {
            public override StatusKey Key => new StatusKey("inert_test");
            public override StatusScope Scope => StatusScope.Entity;
        }

        [Test]
        public void Base_behavior_defaults_are_no_ops()
        {
            var behavior = new InertBehavior();
            Assert.IsFalse(behavior.StacksMagnitude);
            // 기본 구현이 아무것도 하지 않고 예외 없이 통과해야 한다.
            behavior.OnTurnEnd(new StatusTickContext());
            behavior.OnHolderDied(new StatusDeathContext());
        }

        [Test]
        public void Stack_creates_then_accumulates_magnitude()
        {
            var bag = new StatusBag();
            var key = new StatusKey("poison_test");

            var first = bag.Stack(key, StatusLifetime.Permanent, 2);
            Assert.AreEqual(2, first.Magnitude);

            var second = bag.Stack(key, StatusLifetime.Permanent, 3);
            Assert.AreSame(first, second);          // 같은 인스턴스에 누적
            Assert.AreEqual(5, bag.Get(key).Magnitude);
            Assert.AreEqual(1, CountOf(bag, key));  // 인스턴스는 키당 하나
        }

        [Test]
        public void Stack_keeps_first_lifetime_kind()
        {
            var bag = new StatusBag();
            var key = new StatusKey("block_test");
            bag.Stack(key, StatusLifetime.ThisTurn, 3);
            bag.Stack(key, StatusLifetime.ThisTurn, 1);

            Assert.AreEqual(StatusLifetimeKind.ThisTurn, bag.Get(key).Kind);
            Assert.AreEqual(4, bag.Get(key).Magnitude);
        }

        private static int CountOf(StatusBag bag, StatusKey key)
        {
            var count = 0;
            foreach (var status in bag.All)
            {
                if (status.Key == key) count++;
            }
            return count;
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~StatusHookSurfaceTests"`
Expected: 컴파일 실패 — `StatusTickContext`, `StatusDeathContext`, `StacksMagnitude`, `Stack` 미정의.

- [ ] **Step 3: 구현**

`IStatusBehavior.cs`의 기존 `StatusContext` 아래에 컨텍스트 2종을 추가하고 인터페이스·베이스를 확장한다:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Status
{
    /// <summary>Inputs a status behavior may read when a hook fires.</summary>
    public sealed class StatusContext
    {
        public StatusInstance Instance;
    }

    /// <summary>턴 종료 틱 훅 입력. DealDamage는 보유자에게 직접 피해를 주는 배선(파티원은
    /// TakeDamage, 적은 Hp 차감)이며 ModifyIncomingDamage를 경유하지 않는다. Events에 추가한
    /// 이벤트는 타임라인의 현재 위치에 이어 붙는다.</summary>
    public sealed class StatusTickContext
    {
        public StatusInstance Instance;
        public StatusBag HolderBag;
        public string HolderId;
        public Action<int> DealDamage;
        public List<ResolutionEvent> Events;
    }

    /// <summary>보유자 사망 훅 입력. State는 이전 대상 탐색 등 규칙 판단에 쓴다.</summary>
    public sealed class StatusDeathContext
    {
        public StatusInstance Instance;
        public StatusBag HolderBag;
        public string HolderId;
        public Combat.CombatState State;
        public List<ResolutionEvent> Events;
    }

    /// <summary>Behavior for a status key. Implement only the relevant hooks (defaults are no-ops).
    /// Behavior lives here (code, registered); the StatusInstance on a holder is just data.</summary>
    public interface IStatusBehavior
    {
        StatusKey Key { get; }
        StatusScope Scope { get; }

        /// <summary>재부여 시 수치를 교체하지 않고 합산할지 (방어·독 = true; §3.1/§3.2).</summary>
        bool StacksMagnitude { get; }

        /// <summary>Entity-scoped: fold into damage the holder is about to RECEIVE.</summary>
        int ModifyIncomingDamage(int damage, StatusContext ctx);

        /// <summary>Card-scoped: return true to nullify/skip the card's resolution (e.g. stun).</summary>
        bool InterceptCardResolve(StatusContext ctx);

        /// <summary>Entity-scoped: fold into the executionOrder of a card owned by the holder (e.g. slow/haste).</summary>
        int ModifyExecutionOrder(int executionOrder, StatusContext ctx);

        /// <summary>행동 턴 종료(수명 만료 전)에 보유자 단위로 발동하는 틱 (예: 독 피해+성장).</summary>
        void OnTurnEnd(StatusTickContext ctx);

        /// <summary>보유자가 사망한 직후 발동 (예: 남은 독 이전).</summary>
        void OnHolderDied(StatusDeathContext ctx);
    }

    public abstract class StatusBehavior : IStatusBehavior
    {
        public abstract StatusKey Key { get; }
        public abstract StatusScope Scope { get; }

        public virtual bool StacksMagnitude => false;
        public virtual int ModifyIncomingDamage(int damage, StatusContext ctx) => damage;
        public virtual bool InterceptCardResolve(StatusContext ctx) => false;
        public virtual int ModifyExecutionOrder(int executionOrder, StatusContext ctx) => executionOrder;
        public virtual void OnTurnEnd(StatusTickContext ctx) { }
        public virtual void OnHolderDied(StatusDeathContext ctx) { }
    }
}
```

`StatusBag.cs`의 `Consume` 아래에 추가:

```csharp
/// <summary>수치 합산 적용: 같은 키가 있으면 Magnitude만 더하고(최초 적용의 수명 유지),
/// 없으면 새로 추가한다. StacksMagnitude를 선언한 상태(방어·독)에 사용한다.</summary>
public StatusInstance Stack(StatusKey key, StatusLifetime lifetime, int magnitude)
{
    var existing = Get(key);
    if (existing != null)
    {
        existing.Magnitude += magnitude;
        return existing;
    }

    var created = new StatusInstance(key, lifetime, magnitude);
    _statuses.Add(created);
    return created;
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 신규 3개 포함 전체 PASS (기존 상태 테스트 회귀 없음 — 인터페이스 구현체는 모두 `StatusBehavior` 베이스 상속이라 컴파일 유지).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Status/IStatusBehavior.cs Assets/Core/Status/StatusBag.cs Assets/Core/Tests/EditMode/StatusHookSurfaceTests.cs
git commit -m "feat(core): add per-status hook surface (stack/turn-end/on-died)"
```

---

### Task 2: 턴 종료 틱 파이프라인 + StatusTicked 이벤트

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs:203-214` (`EndOfTurnMaintenance`)
- Test: `Assets/Core/Tests/EditMode/StatusTickPipelineTests.cs` (신규)

**Interfaces:**
- Consumes: Task 1의 `OnTurnEnd(StatusTickContext)`
- Produces: `StatusTicked(string HolderId, string StatusId, int Damage, int Magnitude)` 이벤트. 틱 실행 순서 보장: **파티 대형 순 → 적 대형 순, 틱 전체가 수명 만료(EndOfTurn)보다 먼저**. 이 순서는 Task 5(독)와 Task 13(카드 검증)이 전제한다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class StatusTickPipelineTests
    {
        private static readonly StatusKey TickKey = new StatusKey("tick_test");
        private static readonly StatusKey MarkerKey = new StatusKey("tick_marker_test");

        /// <summary>독과 같은 모양의 테스트 전용 틱: 마커가 있을 때만 Magnitude만큼 피해.</summary>
        private sealed class MarkerGatedTickBehavior : StatusBehavior
        {
            public override StatusKey Key => TickKey;
            public override StatusScope Scope => StatusScope.Entity;

            public override void OnTurnEnd(StatusTickContext ctx)
            {
                if (!ctx.HolderBag.Has(MarkerKey)) return;
                ctx.DealDamage(ctx.Instance.Magnitude);
                ctx.Events.Add(new StatusTicked(
                    ctx.HolderId, Key.Id, ctx.Instance.Magnitude, ctx.Instance.Magnitude));
            }
        }

        private sealed class MarkerBehavior : StatusBehavior
        {
            public override StatusKey Key => MarkerKey;
            public override StatusScope Scope => StatusScope.Entity;
        }

        private static StatusRegistry Registry()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new MarkerGatedTickBehavior());
            statuses.Register(new MarkerBehavior());
            return statuses;
        }

        [Test]
        public void Turn_end_tick_damages_enemy_and_emits_event_before_turn_ended()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            state.Enemies[0].Statuses.Add(TickKey, StatusLifetime.Permanent, 3);
            // ThisTurn 마커가 틱 시점에 아직 살아 있어야 한다 (틱이 수명 만료보다 먼저).
            state.Enemies[0].Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);

            var events = new TurnResolver(new EffectRegistry(), Registry()).Resolve(state, 0);

            Assert.AreEqual(7, state.Enemies[0].Hp);
            var tick = events.OfType<StatusTicked>().Single();
            Assert.AreEqual("goblin", tick.HolderId);
            Assert.AreEqual(3, tick.Damage);
            Assert.Less(events.IndexOf(tick), events.FindIndex(e => e is TurnEnded));
            // 수명 만료는 틱 이후: 마커는 턴이 끝난 뒤에는 제거되어 있다.
            Assert.IsFalse(state.Enemies[0].Statuses.Has(MarkerKey));
        }

        [Test]
        public void Dead_holder_is_excluded_from_ticks()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("dead", 0));   // 이미 사망
            state.Enemies[0].Statuses.Add(TickKey, StatusLifetime.Permanent, 3);
            state.Enemies[0].Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);

            var events = new TurnResolver(new EffectRegistry(), Registry()).Resolve(state, 0);

            Assert.AreEqual(0, state.Enemies[0].Hp);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
        }

        [Test]
        public void Party_ticks_run_before_enemy_ticks()
        {
            var state = new CombatState();
            var member = state.AddSoloPlayer(20);
            member.Statuses.Add(TickKey, StatusLifetime.Permanent, 1);
            member.Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);
            state.Enemies.Add(new Enemy("goblin", 10));
            state.Enemies[0].Statuses.Add(TickKey, StatusLifetime.Permanent, 1);
            state.Enemies[0].Statuses.Add(MarkerKey, StatusLifetime.ThisTurn);

            var ticks = new TurnResolver(new EffectRegistry(), Registry())
                .Resolve(state, 0).OfType<StatusTicked>().ToList();

            CollectionAssert.AreEqual(
                new[] { CombatState.SoloPlayerId, "goblin" },
                ticks.Select(t => t.HolderId).ToArray());
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~StatusTickPipelineTests"`
Expected: 컴파일 실패 — `StatusTicked` 미정의.

- [ ] **Step 3: 구현**

`ResolutionEvent.cs`의 `DeathsDoorSurvived` 아래에 추가:

```csharp
/// <summary>상태 행동의 턴 종료 틱이 보유자에게 발동했다 (예: 독 피해). Damage는 이번 틱이 준
/// 피해, Magnitude는 틱 이후의 상태 수치다.</summary>
public sealed record StatusTicked(
    string HolderId, string StatusId, int Damage, int Magnitude) : ResolutionEvent;
```

`TurnResolver.cs` — `Resolve`의 호출부를 `EndOfTurnMaintenance(state, events);`로 바꾸고, 기존 static `EndOfTurnMaintenance`를 다음 인스턴스 메서드들로 교체:

```csharp
private void EndOfTurnMaintenance(CombatState state, List<ResolutionEvent> events)
{
    RunTurnEndTicks(state, events);

    foreach (var member in state.Party)
    {
        member.Statuses.EndOfTurn();
    }

    foreach (var enemy in state.Enemies)
    {
        enemy.Statuses.EndOfTurn();
    }
}

/// <summary>행동 턴 종료 틱: 파티 대형 순 → 적 대형 순. 보유자별로 발동 직전에 생존을 확인하므로
/// 앞선 틱으로 이미 사망한 대상은 제외된다(카드풀 스펙 §3.2).</summary>
private void RunTurnEndTicks(CombatState state, List<ResolutionEvent> events)
{
    if (_statuses == null)
    {
        return;
    }

    foreach (var member in state.Party)
    {
        if (!member.IsAlive) continue;
        var target = member;
        TickHolder(target.Statuses, target.Id, damage => target.TakeDamage(damage), events);
    }

    foreach (var enemy in state.Enemies)
    {
        if (enemy.Hp <= 0) continue;
        var target = enemy;
        TickHolder(target.Statuses, target.Id, damage => target.Hp -= damage, events);
    }
}

private void TickHolder(
    StatusBag bag, string holderId, Action<int> dealDamage, List<ResolutionEvent> events)
{
    // Snapshot: a hook may modify the bag mid-iteration.
    var snapshot = new List<StatusInstance>(bag.All);
    foreach (var status in snapshot)
    {
        if (_statuses.TryResolve(status.Key, out var behavior))
        {
            behavior.OnTurnEnd(new StatusTickContext
            {
                Instance = status,
                HolderBag = bag,
                HolderId = holderId,
                DealDamage = dealDamage,
                Events = events
            });
        }
    }
}
```

파일 상단에 `using System;` 추가 (Action 사용).

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS. 기존 타임라인 테스트는 훅 미등록 시 이벤트가 추가되지 않으므로 불변.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs Assets/Core/Combat/TurnResolver.cs Assets/Core/Tests/EditMode/StatusTickPipelineTests.cs
git commit -m "feat(core): run status turn-end ticks before lifetime expiry"
```

---

### Task 3: 사망 스윕 확장 — EnemyDied 이벤트 + OnHolderDied 디스패치

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs` (`ResolveCard`, `CollectDeathSweepEvents`, `EndOfTurnMaintenance`)
- Test: `Assets/Core/Tests/EditMode/DeathSweepHookTests.cs` (신규)

**Interfaces:**
- Consumes: Task 1의 `OnHolderDied(StatusDeathContext)`, Task 2의 틱 파이프라인
- Produces: `EnemyDied(string EnemyId)` 이벤트. 카드 효과 중 사망과 턴 종료 틱 사망 모두에서 (1) 사망 이벤트 방출 (2) 죽은 보유자의 모든 상태에 `OnHolderDied` 디스패치. Task 6(전염)이 이 훅에 얹힌다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class DeathSweepHookTests
    {
        private static readonly StatusKey RecorderKey = new StatusKey("death_recorder_test");

        private sealed class DeathRecorderBehavior : StatusBehavior
        {
            public readonly List<string> DiedHolders = new List<string>();
            public override StatusKey Key => RecorderKey;
            public override StatusScope Scope => StatusScope.Entity;
            public override void OnHolderDied(StatusDeathContext ctx)
                => DiedHolders.Add(ctx.HolderId);
        }

        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            return effects;
        }

        [Test]
        public void Enemy_killed_by_card_emits_enemy_died_and_dispatches_hook()
        {
            var recorder = new DeathRecorderBehavior();
            var statuses = new StatusRegistry();
            statuses.Register(recorder);

            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 3));
            state.Enemies[0].Statuses.Add(RecorderKey, StatusLifetime.Permanent);

            var def = new CardDefinition("slash", "베기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), statuses).Resolve(state, 0);

            var died = events.OfType<EnemyDied>().Single();
            Assert.AreEqual("goblin", died.EnemyId);
            // CardResolved 다음에 사망 이벤트가 따른다.
            Assert.Greater(events.IndexOf(died), events.FindIndex(e => e is CardResolved));
            CollectionAssert.AreEqual(new[] { "goblin" }, recorder.DiedHolders);
        }

        [Test]
        public void Enemy_killed_by_turn_end_tick_emits_enemy_died_before_turn_ended()
        {
            var recorder = new DeathRecorderBehavior();
            var statuses = new StatusRegistry();
            statuses.Register(recorder);
            statuses.Register(new LethalTickBehavior());

            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 2));
            state.Enemies[0].Statuses.Add(LethalTickBehavior.TickKey, StatusLifetime.Permanent, 5);
            state.Enemies[0].Statuses.Add(RecorderKey, StatusLifetime.Permanent);

            var events = new TurnResolver(new EffectRegistry(), statuses).Resolve(state, 0);

            var died = events.OfType<EnemyDied>().Single();
            Assert.Less(events.IndexOf(died), events.FindIndex(e => e is TurnEnded));
            CollectionAssert.AreEqual(new[] { "goblin" }, recorder.DiedHolders);
            // 틱 사망까지 반영된 뒤 결과가 계산된다 (마지막 적 사망 → 승리).
            Assert.AreEqual(Outcome.Win, events.OfType<TurnEnded>().Single().Outcome);
        }

        private sealed class LethalTickBehavior : StatusBehavior
        {
            public static readonly StatusKey TickKey = new StatusKey("lethal_tick_test");
            public override StatusKey Key => TickKey;
            public override StatusScope Scope => StatusScope.Entity;
            public override void OnTurnEnd(StatusTickContext ctx)
                => ctx.DealDamage(ctx.Instance.Magnitude);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~DeathSweepHookTests"`
Expected: 컴파일 실패 — `EnemyDied` 미정의.

- [ ] **Step 3: 구현**

`ResolutionEvent.cs`에 추가:

```csharp
/// <summary>An enemy's HP reached zero or below (from card effects or a status tick).</summary>
public sealed record EnemyDied(string EnemyId) : ResolutionEvent;
```

`TurnResolver.cs` 변경:

1. 적 스냅샷/스윕 유틸 추가:

```csharp
private static Dictionary<string, bool> SnapshotEnemies(CombatState state)
{
    var snapshot = new Dictionary<string, bool>();
    foreach (var enemy in state.Enemies)
    {
        snapshot[enemy.Id] = enemy.Hp > 0;
    }

    return snapshot;
}

/// <summary>Diffs enemies against a pre-effect snapshot; a newly-dead enemy emits EnemyDied and
/// dispatches OnHolderDied on every status it carried.</summary>
private void CollectEnemyDeathEvents(
    CombatState state, Dictionary<string, bool> before, List<ResolutionEvent> pending)
{
    foreach (var enemy in state.Enemies)
    {
        if (before.TryGetValue(enemy.Id, out var wasAlive) && wasAlive && enemy.Hp <= 0)
        {
            pending.Add(new EnemyDied(enemy.Id));
            DispatchHolderDied(state, enemy.Statuses, enemy.Id, pending);
        }
    }
}

private void DispatchHolderDied(
    CombatState state, StatusBag bag, string holderId, List<ResolutionEvent> events)
{
    if (_statuses == null)
    {
        return;
    }

    var snapshot = new List<StatusInstance>(bag.All);
    foreach (var status in snapshot)
    {
        if (_statuses.TryResolve(status.Key, out var behavior))
        {
            behavior.OnHolderDied(new StatusDeathContext
            {
                Instance = status,
                HolderBag = bag,
                HolderId = holderId,
                State = state,
                Events = events
            });
        }
    }
}
```

2. `CollectDeathSweepEvents`를 인스턴스 메서드로 바꾸고 파티 사망에도 훅을 디스패치 (`PartyMemberDied` 추가 지점 바로 뒤에 `DispatchHolderDied(state, member.Statuses, member.Id, pending);` — 시그니처에 `CombatState state` 파라미터 추가).

3. `ResolveCard`의 효과 루프에서 적 스냅샷을 함께 뜨고 스윕:

```csharp
var beforeSnapshot = SnapshotParty(state);
var enemiesBefore = SnapshotEnemies(state);
// ... _effects.Resolve(effect.Key).Apply(ctx); ...
CollectDeathSweepEvents(state, beforeSnapshot, pendingDeathEvents);
CollectEnemyDeathEvents(state, enemiesBefore, pendingDeathEvents);
```

4. `EndOfTurnMaintenance`에서 틱 전 스냅샷 → 틱 → 사망 스윕 → 수명 만료 순으로 재배열:

```csharp
private void EndOfTurnMaintenance(CombatState state, List<ResolutionEvent> events)
{
    var partyBefore = SnapshotParty(state);
    var enemiesBefore = SnapshotEnemies(state);

    RunTurnEndTicks(state, events);

    CollectDeathSweepEvents(state, partyBefore, events);
    CollectEnemyDeathEvents(state, enemiesBefore, events);

    foreach (var member in state.Party)
    {
        member.Statuses.EndOfTurn();
    }

    foreach (var enemy in state.Enemies)
    {
        enemy.Statuses.EndOfTurn();
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS. 기존 콘텐츠에는 `OnHolderDied` 구현이 없어 타임라인에 `EnemyDied`만 늘어난다 — 이벤트 시퀀스를 통째로 비교하는 기존 테스트가 있으면 기대값에 `EnemyDied`를 추가로 반영한다(예: `ScenarioRunnerTests`류가 적 처치 타임라인을 고정해 두었다면 그 기대 배열에 삽입).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Events/ResolutionEvent.cs Assets/Core/Combat/TurnResolver.cs Assets/Core/Tests/EditMode/DeathSweepHookTests.cs
git commit -m "feat(core): emit EnemyDied and dispatch OnHolderDied on death sweeps"
```

---

### Task 4: EnemyTargeting + TargetSelector.All + 핸들러 대상 배선 + 방어 합산

**Files:**
- Create: `Assets/Core/Combat/EnemyTargeting.cs`
- Modify: `Assets/Core/Cards/TargetSelector.cs`, `Assets/Core/Effects/DamageHandler.cs`, `Assets/Core/Effects/ApplyStatusHandler.cs`, `Assets/Core/Status/BlockBehavior.cs`, `Assets/Core/Simulation/Authoring/EffectSpec.cs` (`TargetSelectorRef`/`ToSelector`)
- Test: `Assets/Core/Tests/EditMode/EnemyTargetingTests.cs` (신규)

**Interfaces:**
- Consumes: Task 1의 `StacksMagnitude`, `StatusBag.Stack`
- Produces:
  - `EnemyTargeting.Select(CombatState, TargetSelector) : Enemy` (생존 대형 기준; `All`은 null)
  - `EnemyTargeting.SelectAll(CombatState) : List<Enemy>` (생존 전원)
  - `EnemyTargeting.ByIdOrFront(CombatState, string) : Enemy` (기존 레거시 선택 통합 — 백로그 §12.3 중복 해소)
  - `TargetSelector.All`, `TargetSelectorRef.All`
  - `StatusApplyTarget.PartyBySelector` (아군 위치 범위 상태 부여)
  - 플레이어 카드의 `EffectData.TargetSelector`가 적 대형 위치 선택으로 동작 (2026-07-27 스펙 §3: 실행 시작 시 생존 대형에서 확정)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class EnemyTargetingTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new BlockBehavior());
            return statuses;
        }

        private static CombatState TwoEnemies()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("front", 10));
            state.Enemies.Add(new Enemy("back", 10));
            return state;
        }

        [Test]
        public void BackMost_selector_hits_the_living_back_enemy()
        {
            var state = TwoEnemies();
            var def = new CardDefinition("back_hit", "후열 타격", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 3) { TargetSelector = TargetSelector.BackMost } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(10, state.Enemies[0].Hp);
            Assert.AreEqual(7, state.Enemies[1].Hp);
        }

        [Test]
        public void All_selector_damages_every_living_enemy_and_sums_damage_dealt()
        {
            var state = TwoEnemies();
            state.Enemies.Add(new Enemy("dead", 0)); // 생존 대형에서 제외
            var def = new CardDefinition("sweep", "휩쓸기", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 2) { TargetSelector = TargetSelector.All } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(8, state.Enemies[0].Hp);
            Assert.AreEqual(8, state.Enemies[1].Hp);
            Assert.AreEqual(0, state.Enemies[2].Hp);   // 시체는 건드리지 않음
            Assert.AreEqual(4, events.OfType<CardResolved>().Single().DamageDealt);
        }

        [Test]
        public void Apply_status_with_selector_targets_back_enemy()
        {
            var state = TwoEnemies();
            var def = new CardDefinition("back_status", "후열 부여", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.TargetEnemy, 2)
                    with { TargetSelector = TargetSelector.BackMost } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Block));
            // ThisTurn 상태는 턴 종료에 만료되므로 부여 사실은 수명 만료 이전 세맨틱으로 검증할 수
            // 없다 — 대신 만료 전 수치를 남기는 Permanent로 재검증한다.
            var state2 = TwoEnemies();
            var def2 = new CardDefinition("back_status2", "후열 부여2", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Permanent, StatusApplyTarget.TargetEnemy, 2)
                    with { TargetSelector = TargetSelector.BackMost } });
            state2.Zone.Add(new ExecutionCardInstance(def2) { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(state2, 0);
            Assert.AreEqual(2, state2.Enemies[1].Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Party_by_selector_applies_status_to_front_ally()
        {
            var state = new CombatState();
            state.Party.Add(new PartyMember("a", "A", 20));
            state.Party.Add(new PartyMember("b", "B", 20));
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("cover_front", "전열 엄호", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Permanent, StatusApplyTarget.PartyBySelector, 4)
                    with { TargetSelector = TargetSelector.FrontMost } });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = "b" });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(4, state.Party[0].Statuses.Get(StatusKeys.Block).Magnitude);
            Assert.IsFalse(state.Party[1].Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Block_applications_stack_within_a_turn()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var block3 = new CardDefinition("b3", "방어3", Side.Player, 4,
                new[] { EffectData.ApplyStatus(
                    StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 3) });
            var block1 = new CardDefinition("b1", "방어1", Side.Player, 5,
                new[] { EffectData.ApplyStatus(
                    StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 1) });
            var enemyHit = new CardDefinition("jab", "찌르기", Side.Enemy, 6,
                new[] { new EffectData(EffectKeys.Damage, 4) });
            state.Zone.Add(new ExecutionCardInstance(block3) { OwnerId = CombatState.SoloPlayerId });
            state.Zone.Add(new ExecutionCardInstance(block1) { OwnerId = CombatState.SoloPlayerId });
            state.Zone.Add(new ExecutionCardInstance(enemyHit) { OwnerId = "goblin" });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 합산 방어 4가 피해 4를 전부 흡수한다 (교체였다면 방어 1만 남아 3 피해).
            Assert.AreEqual(20, state.Party[0].Hp);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~EnemyTargetingTests"`
Expected: 컴파일 실패 — `TargetSelector.All`, `StatusApplyTarget.PartyBySelector` 미정의.

- [ ] **Step 3: 구현**

`TargetSelector.cs`에 `All` 추가 (문서 주석에 "생존 유닛 전부, 다중 대상 효과 전용" 명시). `EffectSpec.cs`의 `TargetSelectorRef`에 `All` 추가 + `ToSelector`에 `case TargetSelectorRef.All: return TargetSelector.All;`.

`EnemyTargeting.cs` 신규 (PartyTargeting의 대칭):

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Resolves enemy targets by living-formation position (Enemies index 0 = front, dead
    /// skipped, never reindexed) — the enemy-side mirror of PartyTargeting. ByIdOrFront preserves the
    /// legacy player-card selection (explicit id, else raw first enemy) so pre-selector content and
    /// timelines stay identical.</summary>
    public static class EnemyTargeting
    {
        public static Enemy Select(CombatState state, TargetSelector selector)
        {
            var living = SelectAll(state);
            switch (selector)
            {
                case TargetSelector.FrontMost: return living.Count > 0 ? living[0] : null;
                case TargetSelector.SecondFromFront: return living.Count > 1 ? living[1] : null;
                case TargetSelector.BackMost: return living.Count > 0 ? living[living.Count - 1] : null;
                case TargetSelector.Random:
                    return living.Count > 0 ? living[state.Rng.Next(living.Count)] : null;
                default: return null; // All은 다중 대상 — SelectAll을 쓴다.
            }
        }

        public static List<Enemy> SelectAll(CombatState state)
        {
            var living = new List<Enemy>();
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Hp > 0)
                {
                    living.Add(enemy);
                }
            }

            return living;
        }

        /// <summary>Legacy selection: explicit id (missing id = no target), else the first enemy
        /// regardless of HP — exactly the pre-selector behavior of DamageHandler.SelectEnemy.</summary>
        public static Enemy ByIdOrFront(CombatState state, string targetId)
        {
            if (!string.IsNullOrEmpty(targetId))
            {
                foreach (var enemy in state.Enemies)
                {
                    if (enemy.Id == targetId)
                    {
                        return enemy;
                    }
                }

                return null;
            }

            return state.Enemies.Count > 0 ? state.Enemies[0] : null;
        }
    }
}
```

`DamageHandler.cs` — 플레이어 분기를 selector 인지로 교체하고 `SelectEnemy`를 삭제(`EnemyTargeting.ByIdOrFront`로 대체):

```csharp
if (ctx.Card.Def.Side == Side.Player)
{
    if (ctx.Effect?.TargetSelector == Cards.TargetSelector.All)
    {
        var targets = EnemyTargeting.SelectAll(ctx.State);
        if (targets.Count == 0)
        {
            ctx.Cancel(CardCancellationReason.NoValidTarget);
            return;
        }

        var total = 0;
        foreach (var each in targets)
        {
            var dealt = FoldIncoming(ctx, each.Statuses, amount);
            each.Hp -= dealt;
            total += dealt;
        }

        ctx.DamageDealt = total;
        return;
    }

    var target = ctx.Effect?.TargetSelector is Cards.TargetSelector selector
        ? EnemyTargeting.Select(ctx.State, selector)
        : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
    if (target == null)
    {
        ctx.Cancel(CardCancellationReason.NoValidTarget);
        return;
    }

    var damage = FoldIncoming(ctx, target.Statuses, amount);
    target.Hp -= damage;
    ctx.DamageDealt = damage;
    ctx.TargetId = target.Id;
}
```

적 분기(파티 공격)에도 `All` 지원: selector가 `All`이면 생존 파티원 전원에 `TakeDamage` 루프(합산 `DamageDealt`), 그 외는 기존 `SelectPartyTarget` 유지.

`ApplyStatusHandler.cs`:

1. `StatusApplyTarget`에 `PartyBySelector` 추가 (주석: "아군 위치 범위 — effect.TargetSelector로 확정, null이면 FrontMost").
2. 모든 `bag.Add(payload.Key, payload.Lifetime, ctx.EffectValue)` 호출을 스택 인지 헬퍼로 교체:

```csharp
private static void ApplyTo(EffectContext ctx, ApplyStatusPayload payload, StatusBag bag)
{
    if (ctx.StatusRegistry != null
        && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior)
        && behavior.StacksMagnitude)
    {
        bag.Stack(payload.Key, payload.Lifetime, ctx.EffectValue);
        return;
    }

    bag.Add(payload.Key, payload.Lifetime, ctx.EffectValue);
}
```

(EffectContext에 이미 `StatusRegistry`가 있다.)

3. `ApplyTargetEnemy`를 selector 인지로:

```csharp
private static void ApplyTargetEnemy(EffectContext ctx, ApplyStatusPayload payload)
{
    if (ctx.Effect?.TargetSelector == Cards.TargetSelector.All)
    {
        var targets = EnemyTargeting.SelectAll(ctx.State);
        if (targets.Count == 0)
        {
            ctx.Cancel(CardCancellationReason.NoValidTarget);
            return;
        }

        foreach (var each in targets)
        {
            ApplyTo(ctx, payload, each.Statuses);
        }

        return;
    }

    var enemy = ctx.Effect?.TargetSelector is Cards.TargetSelector selector
        ? EnemyTargeting.Select(ctx.State, selector)
        : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
    if (enemy == null)
    {
        ctx.Cancel(CardCancellationReason.NoValidTarget);
        return;
    }

    ApplyTo(ctx, payload, enemy.Statuses);
}
```

(기존 `SelectTargetEnemy` 삭제 — `ByIdOrFront`로 통합.)

4. `PartyBySelector` 분기 추가:

```csharp
case StatusApplyTarget.PartyBySelector:
    ApplyPartyBySelector(ctx, payload);
    break;
```

```csharp
private static void ApplyPartyBySelector(EffectContext ctx, ApplyStatusPayload payload)
{
    var selector = ctx.Effect?.TargetSelector ?? Cards.TargetSelector.FrontMost;
    var member = PartyTargeting.Select(ctx.State, selector);
    if (member == null)
    {
        ctx.Cancel(CardCancellationReason.NoValidTarget);
        return;
    }

    ApplyTo(ctx, payload, member.Statuses);
}
```

`BlockBehavior.cs`에 `public override bool StacksMagnitude => true;` 추가.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS. 방어가 교체에서 합산으로 바뀌므로 같은 턴에 방어를 두 번 부여하던 기존 테스트가 있으면(검색: `guard` 두 장 시나리오) 기대값을 합산 기준으로 수정하고 커밋 메시지에 명시한다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Combat/EnemyTargeting.cs Assets/Core/Cards/TargetSelector.cs Assets/Core/Effects/DamageHandler.cs Assets/Core/Effects/ApplyStatusHandler.cs Assets/Core/Status/BlockBehavior.cs Assets/Core/Simulation/Authoring/EffectSpec.cs Assets/Core/Tests/EditMode/EnemyTargetingTests.cs
git commit -m "feat(core): positional enemy targeting, all-target effects, block stacking"
```

---

### Task 5: 독 상태 + 잠복·안정 마커

**Files:**
- Create: `Assets/Core/Status/PoisonBehavior.cs`, `Assets/Core/Status/PoisonDormantBehavior.cs`, `Assets/Core/Status/PoisonStasisBehavior.cs`
- Modify: `Assets/Core/Status/StatusKey.cs`, `Assets/Core/Simulation/CombatRegistries.cs`, `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Test: `Assets/Core/Tests/EditMode/PoisonStatusTests.cs` (신규)

**Interfaces:**
- Consumes: Task 1 훅, Task 2 틱 파이프라인, Task 4 `StatusBag.Stack` 적용 경로
- Produces: `StatusKeys.Poison`("poison"), `StatusKeys.PoisonDormant`("poison_dormant"), `StatusKeys.PoisonStasis`("poison_stasis"). 등록: `new PoisonBehavior(growthPerTurn: 1)`. 이후 태스크가 이 키들을 사용한다.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class PoisonStatusTests
    {
        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            return statuses;
        }

        private static CombatState OneEnemy(int hp = 20)
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", hp));
            return state;
        }

        [Test]
        public void Poison_ticks_at_turn_end_dealing_magnitude_then_growing_by_one()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);

            var events = new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(17, state.Enemies[0].Hp);   // 피해 3
            Assert.AreEqual(4, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude); // 그 후 +1
            var tick = events.OfType<StatusTicked>().Single();
            Assert.AreEqual(3, tick.Damage);
            Assert.AreEqual(4, tick.Magnitude);
        }

        [Test]
        public void Dormant_marker_skips_this_turns_tick_entirely()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 3);
            state.Enemies[0].Statuses.Add(StatusKeys.PoisonDormant, StatusLifetime.ThisTurn);

            var events = new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
            // 마커는 이번 턴로 소멸 — 다음 턴에는 정상 발동한다.
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.PoisonDormant));
        }

        [Test]
        public void Stasis_marker_deals_damage_but_suppresses_growth()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 2);
            state.Enemies[0].Statuses.Add(StatusKeys.PoisonStasis, StatusLifetime.ThisTurn);

            new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(18, state.Enemies[0].Hp);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Zero_magnitude_poison_does_not_tick()
        {
            var state = OneEnemy();
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 0);

            var events = new TurnResolver(new EffectRegistry(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
        }

        [Test]
        public void Default_registries_resolve_poison_and_markers()
        {
            var context = FateWeaver.Simulation.Authoring.AuthoringContext.Default();
            Assert.IsTrue(context.HasStatus(StatusKeys.Poison));
            Assert.IsTrue(context.HasStatus(StatusKeys.PoisonDormant));
            Assert.IsTrue(context.HasStatus(StatusKeys.PoisonStasis));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~PoisonStatusTests"`
Expected: 컴파일 실패 — `StatusKeys.Poison`, `PoisonBehavior` 미정의.

- [ ] **Step 3: 구현**

`StatusKey.cs`의 `StatusKeys`에 추가:

```csharp
public static readonly StatusKey Poison = new StatusKey("poison");
public static readonly StatusKey PoisonDormant = new StatusKey("poison_dormant");
public static readonly StatusKey PoisonStasis = new StatusKey("poison_stasis");
public static readonly StatusKey Contagion = new StatusKey("contagion"); // Task 6에서 행동 등록
```

`PoisonBehavior.cs`:

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>독 X (카드풀 스펙 §3.2): 행동 턴 종료에 X만큼 피해를 주고 1 증가한다. 이번 턴에
    /// 부여된 독도 이번 턴 종료에 발동하며, 이미 사망한 대상은 틱 파이프라인이 제외한다.
    /// 잠복(PoisonDormant) 마커는 이번 턴 발동 자체를, 안정(PoisonStasis) 마커는 성장만 금지한다
    /// (§3.3 우선순위 1층 '금지·고정'). 독 피해는 방어(ModifyIncomingDamage)를 경유하지 않는다.
    /// 성장량은 규칙 수치라 등록 시점에 주입한다(매직 넘버 금지).</summary>
    public sealed class PoisonBehavior : StatusBehavior
    {
        private readonly int _growthPerTurn;

        public PoisonBehavior(int growthPerTurn)
        {
            _growthPerTurn = growthPerTurn;
        }

        public override StatusKey Key => StatusKeys.Poison;
        public override StatusScope Scope => StatusScope.Entity;
        public override bool StacksMagnitude => true;

        public override void OnTurnEnd(StatusTickContext ctx)
        {
            if (ctx.HolderBag.Has(StatusKeys.PoisonDormant))
            {
                return;
            }

            var damage = ctx.Instance.Magnitude;
            if (damage <= 0)
            {
                return;
            }

            ctx.DealDamage(damage);
            if (!ctx.HolderBag.Has(StatusKeys.PoisonStasis))
            {
                ctx.Instance.Magnitude += _growthPerTurn;
            }

            ctx.Events.Add(new Events.StatusTicked(
                ctx.HolderId, Key.Id, damage, ctx.Instance.Magnitude));
        }
    }
}
```

`PoisonDormantBehavior.cs` / `PoisonStasisBehavior.cs` (각각 별도 파일, 훅 없는 마커):

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>독 잠복 (조기 발병): 이번 턴 종료에 독이 발동하지 않는다. ThisTurn 수명으로 부여.</summary>
    public sealed class PoisonDormantBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.PoisonDormant;
        public override StatusScope Scope => StatusScope.Entity;
    }
}
```

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>독 안정 (안정 배양): 이번 턴 종료 독 피해는 그대로, 성장만 금지한다. ThisTurn 수명.</summary>
    public sealed class PoisonStasisBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.PoisonStasis;
        public override StatusScope Scope => StatusScope.Entity;
    }
}
```

`CombatRegistries.Statuses()`에 등록 추가:

```csharp
statuses.Register(new PoisonBehavior(growthPerTurn: 1)); // §3.2: 행동 턴 종료 1 증가
statuses.Register(new PoisonDormantBehavior());
statuses.Register(new PoisonStasisBehavior());
```

`KoreanDescriptionCatalog.CreateDefault()`의 상태 등록에 추가:

```csharp
statuses.Register(StatusKeys.Poison, "독");
statuses.Register(StatusKeys.PoisonDormant, "독 잠복");
statuses.Register(StatusKeys.PoisonStasis, "독 안정");
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS. `StatusContentTests`/`DescriptionRegistryTests`가 등록 상태 집합을 고정해 두었다면 새 키 3종을 기대 집합에 추가.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Status/PoisonBehavior.cs Assets/Core/Status/PoisonDormantBehavior.cs Assets/Core/Status/PoisonStasisBehavior.cs Assets/Core/Status/StatusKey.cs Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs Assets/Core/Tests/EditMode/PoisonStatusTests.cs
git commit -m "feat(core): poison status with dormant/stasis mutation markers"
```

---

### Task 6: 전염 상태 (사후 전염) + StatusTransferred 이벤트

**Files:**
- Create: `Assets/Core/Status/ContagionBehavior.cs`
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Simulation/CombatRegistries.cs`, `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Test: `Assets/Core/Tests/EditMode/ContagionStatusTests.cs` (신규)

**Interfaces:**
- Consumes: Task 3 `OnHolderDied` 디스패치, Task 4 `EnemyTargeting.Select`, Task 5 `StatusKeys.Poison`/`StatusKeys.Contagion`
- Produces: `StatusTransferred(string FromHolderId, string ToHolderId, string StatusId, int Magnitude)` 이벤트, `ContagionBehavior`. 유효 기간은 부여 수명(`Turns(2)` = 이번+다음 턴)으로 표현.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class ContagionStatusTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new ContagionBehavior());
            return statuses;
        }

        [Test]
        public void Killing_a_contagious_poisoned_enemy_transfers_poison_to_front_living_enemy()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("victim", 2));
            state.Enemies.Add(new Enemy("next", 10));
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 4);
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var def = new CardDefinition("finisher", "마무리", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("victim", transfer.FromHolderId);
            Assert.AreEqual("next", transfer.ToHolderId);
            Assert.AreEqual(4, transfer.Magnitude);
            // 이전받은 독은 이번 턴 종료에 발동한다 (§3.2: 행동 중 부여된 독도 발동).
            Assert.AreEqual(4, events.OfType<StatusTicked>().Single(t => t.HolderId == "next").Damage);
        }

        [Test]
        public void Contagion_without_poison_does_nothing()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("victim", 2));
            state.Enemies.Add(new Enemy("next", 10));
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var def = new CardDefinition("finisher", "마무리", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusTransferred>().ToList());
            Assert.IsFalse(state.Enemies[1].Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void No_living_recipient_means_no_transfer()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("victim", 2));   // 유일한 적
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 4);
            state.Enemies[0].Statuses.Add(StatusKeys.Contagion, StatusLifetime.Turns(2));

            var def = new CardDefinition("finisher", "마무리", Side.Player, 4,
                new[] { new EffectData(EffectKeys.Damage, 5) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<StatusTransferred>().ToList());
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~ContagionStatusTests"`
Expected: 컴파일 실패 — `StatusTransferred`, `ContagionBehavior` 미정의.

- [ ] **Step 3: 구현**

`ResolutionEvent.cs`에 추가:

```csharp
/// <summary>사망한 보유자의 상태가 다른 보유자에게 이전되었다 (예: 사후 전염의 독 이전).</summary>
public sealed record StatusTransferred(
    string FromHolderId, string ToHolderId, string StatusId, int Magnitude) : ResolutionEvent;
```

`ContagionBehavior.cs`:

```csharp
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Status
{
    /// <summary>전염 (사후 전염): 보유자가 독 상태로 사망하면 남은 독 전량을 현재 적군 앞 하나
    /// (생존)에게 이전한다. 유효 기간은 부여 수명(Turns)으로 표현한다. 죽은 보유자는 생존 대형에서
    /// 이미 빠져 있으므로 EnemyTargeting.Select(FrontMost)가 곧 '다음 전열'이다.</summary>
    public sealed class ContagionBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Contagion;
        public override StatusScope Scope => StatusScope.Entity;

        public override void OnHolderDied(StatusDeathContext ctx)
        {
            var poison = ctx.HolderBag.Get(StatusKeys.Poison);
            if (poison == null || poison.Magnitude <= 0)
            {
                return;
            }

            var recipient = EnemyTargeting.Select(ctx.State, TargetSelector.FrontMost);
            if (recipient == null)
            {
                return;
            }

            recipient.Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, poison.Magnitude);
            ctx.Events.Add(new Events.StatusTransferred(
                ctx.HolderId, recipient.Id, StatusKeys.Poison.Id, poison.Magnitude));
            ctx.HolderBag.Remove(StatusKeys.Poison);
        }
    }
}
```

`CombatRegistries.Statuses()`에 `statuses.Register(new ContagionBehavior());`, `KoreanDescriptionCatalog`에 `statuses.Register(StatusKeys.Contagion, "전염");` 추가.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Status/ContagionBehavior.cs Assets/Core/Events/ResolutionEvent.cs Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs Assets/Core/Tests/EditMode/ContagionStatusTests.cs
git commit -m "feat(core): contagion status transfers poison on holder death"
```

---

### Task 7: consume_status 효과 + ConsumedStatusAtLeast 조건 + SkipOnBasic

**Files:**
- Create: `Assets/Core/Effects/ConsumeStatusHandler.cs`, `Assets/Core/Effects/ConsumeStatusPayload.cs`
- Modify: `Assets/Core/Effects/EffectKey.cs`, `Assets/Core/Combat/ExecutionCardInstance.cs`, `Assets/Core/Conditions/Condition.cs`, `Assets/Core/Conditions/ConditionEvaluator.cs`, `Assets/Core/Cards/CardDefinition.cs`, `Assets/Core/Combat/TurnResolver.cs`, `Assets/Core/Simulation/CombatRegistries.cs`
- Test: `Assets/Core/Tests/EditMode/ConsumeStatusTests.cs` (신규)

**Interfaces:**
- Consumes: Task 4 `EnemyTargeting`, Task 5 독 키
- Produces:
  - `EffectKeys.ConsumeStatus`("consume_status"), `ConsumeStatusPayload(StatusKey Key, int MaxAmount, int DamageBonusPerConsumed)`
  - `ExecutionCardInstance.ConsumedStatusAmount : int` (public get) — 같은 카드의 뒤 효과가 읽음
  - `Condition` 신규: `ConsumedStatusAtLeast(int N)`
  - `EffectData.SkipOnBasic : bool` — 조건 실패(Basic) 시 효과를 통째로 건너뜀 ("~했다면 X" 문법)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class ConsumeStatusTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new DamageHandler());
            effects.Register(new ApplyStatusHandler());
            effects.Register(new ConsumeStatusHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new BlockBehavior());
            return statuses;
        }

        private static CombatState OneEnemy(int hp, int poison)
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", hp));
            if (poison > 0)
            {
                state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, poison);
            }
            return state;
        }

        [Test]
        public void Consume_clamps_to_available_magnitude_and_records_on_card()
        {
            var state = OneEnemy(20, 2);
            var def = new CardDefinition("drain", "흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 0) }
            });
            var card = new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
            state.Zone.Add(card);

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(2, card.ConsumedStatusAmount);
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison));
        }

        [Test]
        public void Consumed_stacks_feed_pending_damage_bonus_into_a_later_damage_effect()
        {
            // 응축 파열 모양: 독 최대 3 소비 → 피해 2 + 소비×2.
            var state = OneEnemy(20, 3);
            var def = new CardDefinition("burst", "파열", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 3, 2) },
                new EffectData(EffectKeys.Damage, 2)
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(12, state.Enemies[0].Hp);   // 2 + 3×2 = 8 피해
            Assert.AreEqual(8, events.OfType<CardResolved>().Single().DamageDealt);
        }

        [Test]
        public void SkipOnBasic_effect_fires_only_when_condition_succeeds()
        {
            // 독성 환원 모양: 독 1 소비 → 소비했다면 자신에게 방어 4.
            EffectData[] BuildEffects() => new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 1, 0) },
                EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Permanent, StatusApplyTarget.Self, 4)
                    with { Condition = new ConsumedStatusAtLeast(1), SuccessEffectValue = 4, SkipOnBasic = true }
            };

            // 독이 있으면: 소비 성공 → 방어 4.
            var withPoison = OneEnemy(20, 1);
            withPoison.Zone.Add(new ExecutionCardInstance(
                new CardDefinition("reclaim", "환원", Side.Player, 4, BuildEffects()))
                { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(withPoison, 0);
            Assert.AreEqual(4, withPoison.Party[0].Statuses.Get(StatusKeys.Block).Magnitude);

            // 독이 없으면: 소비 0 → 효과 통째로 건너뜀 (방어 상태 자체가 없음).
            var without = OneEnemy(20, 0);
            without.Zone.Add(new ExecutionCardInstance(
                new CardDefinition("reclaim", "환원", Side.Player, 4, BuildEffects()))
                { OwnerId = CombatState.SoloPlayerId });
            new TurnResolver(Effects(), Statuses()).Resolve(without, 0);
            Assert.IsFalse(without.Party[0].Statuses.Has(StatusKeys.Block));
        }

        [Test]
        public void Consuming_zero_is_not_a_cancellation()
        {
            var state = OneEnemy(20, 0);
            var def = new CardDefinition("drain", "흡수", Side.Player, 4, new[]
            {
                new EffectData(EffectKeys.ConsumeStatus, 0)
                    { Payload = new ConsumeStatusPayload(StatusKeys.Poison, 1, 0) },
                new EffectData(EffectKeys.Damage, 2)
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(1, events.OfType<CardResolved>().Count()); // 취소 아님
            Assert.AreEqual(18, state.Enemies[0].Hp);                  // 뒤 효과 정상 실행
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~ConsumeStatusTests"`
Expected: 컴파일 실패 — `ConsumeStatusHandler`, `ConsumedStatusAtLeast`, `SkipOnBasic` 미정의.

- [ ] **Step 3: 구현**

`EffectKey.cs`의 `EffectKeys`에 추가:

```csharp
public static readonly EffectKey ConsumeStatus = new EffectKey("consume_status");
public static readonly EffectKey TriggerStatus = new EffectKey("trigger_status");       // Task 8
public static readonly EffectKey GrantNextTurnFate = new EffectKey("grant_next_turn_fate"); // Task 9
```

`ConsumeStatusPayload.cs`:

```csharp
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>consume_status 파라미터: 대상 적의 상태 수치를 최대 MaxAmount만큼 제거한다.
    /// 소비량은 카드 인스턴스에 기록되고(ConsumedStatusAtLeast 조건이 읽음),
    /// 소비량 × DamageBonusPerConsumed가 이 카드의 뒤 피해 효과에 보너스로 적립된다.</summary>
    public sealed record ConsumeStatusPayload(
        StatusKey Key, int MaxAmount, int DamageBonusPerConsumed) : IEffectPayload;
}
```

`ConsumeStatusHandler.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>대상 적의 상태(예: 독)를 최대치까지 소비한다. 소비 0은 취소가 아니라 그냥 무소득
    /// (독성 환원의 첫 사용). 대상 선택은 damage와 같은 규칙: TargetSelector 지정 시 위치 선택,
    /// 아니면 레거시(TargetId → 첫 적).</summary>
    public sealed class ConsumeStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.ConsumeStatus;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (!(ctx.Effect?.Payload is ConsumeStatusPayload payload))
            {
                return;
            }

            var enemy = ctx.Effect?.TargetSelector is TargetSelector selector
                ? EnemyTargeting.Select(ctx.State, selector)
                : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var status = enemy.Statuses.Get(payload.Key);
            var consumed = status == null ? 0 : Math.Min(status.Magnitude, payload.MaxAmount);
            if (consumed > 0)
            {
                status.Magnitude -= consumed;
                if (status.Magnitude <= 0)
                {
                    enemy.Statuses.Remove(payload.Key);
                }

                ctx.Card.RecordConsumedStatus(consumed);
                if (payload.DamageBonusPerConsumed != 0)
                {
                    ctx.Card.AddPendingDamageBonus(consumed * payload.DamageBonusPerConsumed);
                }
            }

            ctx.TargetId = enemy.Id;
        }

        public IEnumerable<string> ValidateData(EffectData effect)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload))
            {
                yield return "consume_status effect requires a ConsumeStatusPayload.";
                yield break;
            }

            if (string.IsNullOrEmpty(payload.Key.Id))
            {
                yield return "consume_status payload requires a status key.";
            }

            if (payload.MaxAmount < 1)
            {
                yield return "consume_status MaxAmount must be at least 1.";
            }
        }
    }
}
```

`ExecutionCardInstance.cs`에 추가:

```csharp
/// <summary>이 카드의 해석 중 consume_status가 실제로 소비한 누적 수치.
/// ConsumedStatusAtLeast 조건이 읽는다.</summary>
public int ConsumedStatusAmount { get; private set; }

internal void RecordConsumedStatus(int amount) => ConsumedStatusAmount += amount;
```

`Condition.cs`에 추가:

```csharp
/// <summary>Success when this card has already consumed at least N magnitude of a status earlier in
/// its own resolution (consume_status가 기록한 ExecutionCardInstance.ConsumedStatusAmount 기준).</summary>
public sealed record ConsumedStatusAtLeast(int N) : Condition;
```

`ConditionEvaluator.cs`의 `AllOf` 분기 앞에 추가:

```csharp
if (condition is ConsumedStatusAtLeast consumedAtLeast)
{
    return card.ConsumedStatusAmount >= consumedAtLeast.N
        ? ConditionTier.Success
        : ConditionTier.Basic;
}
```

`CardDefinition.cs`의 `EffectData`에 추가:

```csharp
/// <summary>조건이 Basic으로 떨어지면 이 효과를 통째로 건너뛴다 — '~했다면 X' 문법
/// (기본 발동 없음, 성공 시에만 발동). Condition이 null이면 무의미.</summary>
public bool SkipOnBasic { get; init; }
```

`TurnResolver.ResolveCard`의 효과 루프에서 tier 계산 직후, 스냅샷 이전에 추가:

```csharp
if (effect.SkipOnBasic && effect.Condition != null && tier == ConditionTier.Basic)
{
    continue;
}
```

(주의: `strongestTier` 갱신은 skip 판정보다 앞에 있어도 무해 — Basic은 최저 티어다.)

`CombatRegistries.Effects()`에 `effects.Register(new ConsumeStatusHandler());` 추가.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Effects/ConsumeStatusHandler.cs Assets/Core/Effects/ConsumeStatusPayload.cs Assets/Core/Effects/EffectKey.cs Assets/Core/Combat/ExecutionCardInstance.cs Assets/Core/Conditions/Condition.cs Assets/Core/Conditions/ConditionEvaluator.cs Assets/Core/Cards/CardDefinition.cs Assets/Core/Combat/TurnResolver.cs Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Tests/EditMode/ConsumeStatusTests.cs
git commit -m "feat(core): consume_status effect, consumed-at-least condition, skip-on-basic"
```

---

### Task 8: trigger_status 효과 (조기 발병) + EffectContext.ExtraEvents

**Files:**
- Create: `Assets/Core/Effects/TriggerStatusHandler.cs`, `Assets/Core/Effects/TriggerStatusPayload.cs`
- Modify: `Assets/Core/Effects/IEffectHandler.cs` (`EffectContext.ExtraEvents`), `Assets/Core/Combat/TurnResolver.cs` (ExtraEvents 수거), `Assets/Core/Simulation/CombatRegistries.cs`
- Test: `Assets/Core/Tests/EditMode/TriggerStatusTests.cs` (신규)

**Interfaces:**
- Consumes: Task 2 `OnTurnEnd`(즉시 재사용), Task 5 독·잠복 키
- Produces: `TriggerStatusPayload(StatusKey Key, StatusKey SuppressMarkerKey)`, `EffectContext.ExtraEvents : List<ResolutionEvent>` (핸들러가 채우면 TurnResolver가 CardResolved 뒤에 붙임)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class TriggerStatusTests
    {
        private static EffectRegistry Effects()
        {
            var effects = new EffectRegistry();
            effects.Register(new ApplyStatusHandler());
            effects.Register(new TriggerStatusHandler());
            return effects;
        }

        private static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            return statuses;
        }

        private static EffectData Trigger() => new EffectData(EffectKeys.TriggerStatus, 0)
        {
            Payload = new TriggerStatusPayload(StatusKeys.Poison, StatusKeys.PoisonDormant)
        };

        [Test]
        public void Early_onset_ticks_now_and_suppresses_the_turn_end_tick()
        {
            // 조기 발병 모양: 독 1 부여 → 즉시 발동 → 이번 턴 종료에는 발동 없음.
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("early_onset", "조기 발병", Side.Player, 3, new[]
            {
                EffectData.ApplyStatus(
                    StatusKeys.Poison, StatusLifetime.Permanent, StatusApplyTarget.TargetEnemy, 1),
                Trigger()
            });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 즉시 발동: 피해 1 + 성장 → 독 2. 턴 종료 발동 없음 → HP는 19 유지, 독은 2 유지.
            Assert.AreEqual(19, state.Enemies[0].Hp);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            var tick = events.OfType<StatusTicked>().Single();  // 즉시 발동분 1회뿐
            Assert.Greater(events.IndexOf(tick), events.FindIndex(e => e is CardResolved));
            Assert.AreEqual(1, events.OfType<CardResolved>().Single().DamageDealt);
            // 다음 턴에는 정상 발동 (잠복 마커는 ThisTurn으로 소멸).
            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.PoisonDormant));
        }

        [Test]
        public void Trigger_without_the_status_only_plants_the_marker()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 20));
            var def = new CardDefinition("t", "발동", Side.Player, 3, new[] { Trigger() });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(20, state.Enemies[0].Hp);
            Assert.IsEmpty(events.OfType<StatusTicked>().ToList());
            Assert.AreEqual(1, events.OfType<CardResolved>().Count()); // 취소 아님
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~TriggerStatusTests"`
Expected: 컴파일 실패 — `TriggerStatusHandler`, `TriggerStatusPayload` 미정의.

- [ ] **Step 3: 구현**

`TriggerStatusPayload.cs`:

```csharp
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>trigger_status 파라미터: 대상 적의 상태 틱(OnTurnEnd)을 지금 발동시키고,
    /// SuppressMarkerKey를 ThisTurn으로 심어 이번 턴 종료의 같은 발동을 막는다 — 총 발동 횟수는
    /// 유지하고 시점만 앞당긴다 (조기 발병).</summary>
    public sealed record TriggerStatusPayload(
        StatusKey Key, StatusKey SuppressMarkerKey) : IEffectPayload;
}
```

`TriggerStatusHandler.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    public sealed class TriggerStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.TriggerStatus;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (!(ctx.Effect?.Payload is TriggerStatusPayload payload))
            {
                return;
            }

            var enemy = ctx.Effect?.TargetSelector is TargetSelector selector
                ? EnemyTargeting.Select(ctx.State, selector)
                : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var status = enemy.Statuses.Get(payload.Key);
            if (status != null
                && ctx.StatusRegistry != null
                && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior))
            {
                var target = enemy;
                var hpBefore = target.Hp;
                behavior.OnTurnEnd(new StatusTickContext
                {
                    Instance = status,
                    HolderBag = target.Statuses,
                    HolderId = target.Id,
                    DealDamage = damage => target.Hp -= damage,
                    Events = ctx.ExtraEvents
                });
                ctx.DamageDealt = hpBefore - target.Hp;
            }

            enemy.Statuses.Add(payload.SuppressMarkerKey, StatusLifetime.ThisTurn);
            ctx.TargetId = enemy.Id;
        }

        public IEnumerable<string> ValidateData(EffectData effect)
        {
            if (!(effect.Payload is TriggerStatusPayload payload))
            {
                yield return "trigger_status effect requires a TriggerStatusPayload.";
                yield break;
            }

            if (string.IsNullOrEmpty(payload.Key.Id))
            {
                yield return "trigger_status payload requires a status key.";
            }

            if (string.IsNullOrEmpty(payload.SuppressMarkerKey.Id))
            {
                yield return "trigger_status payload requires a suppress-marker key.";
            }
        }
    }
}
```

`IEffectHandler.cs`의 `EffectContext` 출력부에 추가:

```csharp
/// <summary>이 효과가 만든 부가 타임라인 이벤트 (예: 즉시 상태 발동의 StatusTicked).
/// TurnResolver가 CardResolved/CardCancelled 뒤에 발생 순서대로 붙인다.</summary>
public List<ResolutionEvent> ExtraEvents = new List<ResolutionEvent>();
```

(`using System.Collections.Generic;`, `using FateWeaver.Core.Events;` 추가.)

`TurnResolver.ResolveCard`의 효과 루프에서 `Apply` 직후, 사망 스윕 이전에:

```csharp
_effects.Resolve(effect.Key).Apply(ctx);
totalDamage += ctx.DamageDealt;
if (ctx.TargetId != null) targetId = ctx.TargetId;
pendingDeathEvents.AddRange(ctx.ExtraEvents);   // 틱 이벤트가 사망 이벤트보다 앞서도록

CollectDeathSweepEvents(state, beforeSnapshot, pendingDeathEvents);
CollectEnemyDeathEvents(state, enemiesBefore, pendingDeathEvents);
```

`CombatRegistries.Effects()`에 `effects.Register(new TriggerStatusHandler());` 추가.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Effects/TriggerStatusHandler.cs Assets/Core/Effects/TriggerStatusPayload.cs Assets/Core/Effects/IEffectHandler.cs Assets/Core/Combat/TurnResolver.cs Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Tests/EditMode/TriggerStatusTests.cs
git commit -m "feat(core): trigger_status fires a status tick early and suppresses turn end"
```

---

### Task 9: grant_next_turn_fate 효과 (증류)

**Files:**
- Create: `Assets/Core/Effects/GrantNextTurnFateHandler.cs`
- Modify: `Assets/Core/Combat/CombatState.cs`, `Assets/Core/Simulation/DeckCombatSession.cs:388` (`BeginTurn`), `Assets/Core/Simulation/CombatRegistries.cs`
- Test: `Assets/Core/Tests/EditMode/GrantNextTurnFateTests.cs` (신규)

**Interfaces:**
- Consumes: Task 7에서 선언한 `EffectKeys.GrantNextTurnFate`
- Produces: `CombatState.PendingNextTurnFateEnergy : int`. `BeginTurn`이 `FateEnergy = FateEnergyPerTurn + PendingNextTurnFateEnergy` 후 0으로 소거.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class GrantNextTurnFateTests
    {
        [Test]
        public void Effect_banks_fate_for_the_next_player_turn()
        {
            var state = new CombatState();
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            var def = new CardDefinition("distill", "증류", Side.Player, 5,
                new[] { new EffectData(EffectKeys.GrantNextTurnFate, 1) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var effects = new EffectRegistry();
            effects.Register(new GrantNextTurnFateHandler());
            new TurnResolver(effects).Resolve(state, 0);

            Assert.AreEqual(1, state.PendingNextTurnFateEnergy);
        }

        [Test]
        public void Next_turn_refill_includes_and_clears_the_banked_bonus()
        {
            var deck = new List<CardDefinition>
            {
                new CardDefinition("distill", "증류", Side.Player, 5,
                    new[] { new EffectData(EffectKeys.GrantNextTurnFate, 1) })
                    { EnergyCost = 1, Category = CardCategory.Execution }
            };
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", 4, 0) }
            });
            var session = new DeckCombatSession(
                deck, playerHp: 30,
                enemies: new[] { new Enemy("goblin", 100) },
                enemyPolicy: intent, fateEnergyPerTurn: 3, handSize: 5, seed: 1);

            Assert.IsTrue(session.PlayExecutionCard(0));
            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());

            Assert.AreEqual(4, session.FateEnergy);                       // 3 + 1
            Assert.AreEqual(0, session.State.PendingNextTurnFateEnergy);  // 소거

            session.ResolveTurn();
            Assert.IsTrue(session.BeginNextTurn());
            Assert.AreEqual(3, session.FateEnergy);                       // 1회성
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~GrantNextTurnFateTests"`
Expected: 컴파일 실패 — `PendingNextTurnFateEnergy`, `GrantNextTurnFateHandler` 미정의.

- [ ] **Step 3: 구현**

`CombatState.cs`의 `FateEnergyPerTurn` 아래에 추가:

```csharp
/// <summary>다음 플레이어 사용 턴의 운명력 리필에 더해지는 1회성 적립분 (grant_next_turn_fate).
/// 리필 시점에 합산 후 0으로 소거된다.</summary>
public int PendingNextTurnFateEnergy { get; set; }
```

`GrantNextTurnFateHandler.cs`:

```csharp
namespace FateWeaver.Core.Effects
{
    /// <summary>다음 플레이어 사용 턴에 운명력 EffectValue를 추가로 준다 (증류). CombatState에
    /// 적립만 하고, 실제 지급은 세션의 턴 시작 리필이 담당한다.</summary>
    public sealed class GrantNextTurnFateHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.GrantNextTurnFate;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            ctx.State.PendingNextTurnFateEnergy += ctx.EffectValue;
        }
    }
}
```

`DeckCombatSession.BeginTurn`의 리필 줄 교체:

```csharp
_state.FateEnergy = _state.FateEnergyPerTurn + _state.PendingNextTurnFateEnergy;
_state.PendingNextTurnFateEnergy = 0;
```

`CombatRegistries.Effects()`에 `effects.Register(new GrantNextTurnFateHandler());` 추가.

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS (`StarterDeck.EnemyAttack`의 실제 시그니처가 다르면 `DeckCombatSessionTests.Goblin` 헬퍼와 동일한 형태로 맞춘다).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Effects/GrantNextTurnFateHandler.cs Assets/Core/Combat/CombatState.cs Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Tests/EditMode/GrantNextTurnFateTests.cs
git commit -m "feat(core): grant_next_turn_fate banks fate energy for the next turn"
```

---

### Task 10: 개입 제약 — 대상 진영 필터 + 인접 교환

**Files:**
- Modify: `Assets/Core/Intervention/InterventionActionData.cs`, `Assets/Core/Intervention/ChangeExecutionOrderHandler.cs`, `Assets/Core/Intervention/SwapExecutionOrderHandler.cs`, `Assets/Core/Simulation/Authoring/CardSpec.cs`, `Assets/Core/Simulation/Authoring/CardSpecMapper.cs`
- Test: `Assets/Core/Tests/EditMode/InterventionConstraintTests.cs` (신규)

**Interfaces:**
- Consumes: 기존 `InterventionPlayContext` (State/Target/SecondaryTarget/Intervention)
- Produces:
  - `InterventionActionData.TargetSide : Side?` (null = 아무 진영), `RequireAdjacentTargets : bool`; 신규 5-인자 생성자 + 기존 3-인자 생성자 유지
  - `CardSpec.InterventionTargetSide : InterventionTargetSideRef` (enum `Any/Player/Enemy`), `CardSpec.InterventionRequireAdjacent : bool`
  - 거부 시 자원 불변 (기존 CanApply 경로와 동일)

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class InterventionConstraintTests
    {
        private static CombatState StateWithZone(params ExecutionCardInstance[] cards)
        {
            var state = new CombatState { FateEnergy = 10 };
            state.AddSoloPlayer(20);
            state.Enemies.Add(new Enemy("goblin", 10));
            foreach (var card in cards)
            {
                state.Zone.Add(card);
            }
            return state;
        }

        private static ExecutionCardInstance Card(string id, Side side, int order)
            => new ExecutionCardInstance(new CardDefinition(
                    id, id, side, order, new[] { new EffectData(EffectKeys.Damage, 1) }))
                { OwnerId = side == Side.Player ? CombatState.SoloPlayerId : "goblin" };

        [Test]
        public void Side_filtered_change_rejects_wrong_side_and_keeps_energy()
        {
            var playerCard = Card("mine", Side.Player, 4);
            var enemyCard = Card("theirs", Side.Enemy, 5);
            var state = StateWithZone(playerCard, enemyCard);
            var action = new InterventionActionData(
                InterventionActionKeys.ChangeExecutionOrder, 1, -1,
                targetSide: Side.Player, requireAdjacentTargets: false);
            var resolver = new InterventionPlayResolver(NewActions());

            var rejected = resolver.Resolve(state, new[] { new InterventionPlay(action, enemyCard) });
            Assert.AreEqual(0, rejected.AppliedCount);
            Assert.AreEqual(10, state.FateEnergy);
            Assert.AreEqual(5, enemyCard.ExecutionOrder);

            var applied = resolver.Resolve(state, new[] { new InterventionPlay(action, playerCard) });
            Assert.AreEqual(1, applied.AppliedCount);
            Assert.AreEqual(3, playerCard.ExecutionOrder);
        }

        [Test]
        public void Adjacent_swap_rejects_non_adjacent_targets()
        {
            var a = Card("a", Side.Player, 3);
            var b = Card("b", Side.Enemy, 5);
            var c = Card("c", Side.Player, 7);
            var state = StateWithZone(a, b, c);
            var action = new InterventionActionData(
                InterventionActionKeys.SwapExecutionOrder, 1, 0,
                targetSide: null, requireAdjacentTargets: true);
            var resolver = new InterventionPlayResolver(NewActions());

            // a(0)와 c(2)는 비인접 → 거부.
            var rejected = resolver.Resolve(state, new[] { new InterventionPlay(action, a, c) });
            Assert.AreEqual(0, rejected.AppliedCount);
            Assert.AreEqual(3, a.ExecutionOrder);
            Assert.AreEqual(10, state.FateEnergy);

            // a(0)와 b(1)는 인접 → 교환.
            var applied = resolver.Resolve(state, new[] { new InterventionPlay(action, a, b) });
            Assert.AreEqual(1, applied.AppliedCount);
            Assert.AreEqual(5, a.ExecutionOrder);
            Assert.AreEqual(3, b.ExecutionOrder);
        }

        [Test]
        public void Unconstrained_actions_keep_existing_behavior()
        {
            var a = Card("a", Side.Player, 3);
            var c = Card("c", Side.Player, 7);
            var state = StateWithZone(a, c);
            var action = new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, 1, 0);

            var applied = new InterventionPlayResolver(NewActions())
                .Resolve(state, new[] { new InterventionPlay(action, a, c) });

            Assert.AreEqual(1, applied.AppliedCount); // 기존 자리 교환은 인접 불요
        }

        private static InterventionActionRegistry NewActions()
        {
            var actions = new InterventionActionRegistry();
            actions.Register(new ChangeExecutionOrderHandler());
            actions.Register(new SwapExecutionOrderHandler());
            actions.Register(new LockHandler());
            return actions;
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~InterventionConstraintTests"`
Expected: 컴파일 실패 — `InterventionActionData` 5-인자 생성자 미정의.

- [ ] **Step 3: 구현**

`InterventionActionData.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionActionData
    {
        public InterventionActionKey Key { get; }
        public int InterventionCost { get; }
        public int EffectValue { get; }

        /// <summary>대상 레일 카드가 속해야 하는 진영 (null = 제한 없음). 재촉=Player, 유예=Enemy.</summary>
        public Side? TargetSide { get; }

        /// <summary>true면 두 대상이 실행 순서상 서로 인접해야 한다 (엇갈림).</summary>
        public bool RequireAdjacentTargets { get; }

        public InterventionActionData(InterventionActionKey key, int interventionCost, int effectValue)
            : this(key, interventionCost, effectValue, null, false)
        {
        }

        public InterventionActionData(
            InterventionActionKey key,
            int interventionCost,
            int effectValue,
            Side? targetSide,
            bool requireAdjacentTargets)
        {
            Key = key;
            InterventionCost = interventionCost;
            EffectValue = effectValue;
            TargetSide = targetSide;
            RequireAdjacentTargets = requireAdjacentTargets;
        }
    }
}
```

`ChangeExecutionOrderHandler.CanApply`에 조건 추가:

```csharp
&& (ctx.Intervention.TargetSide == null
    || ctx.Target.Def.Side == ctx.Intervention.TargetSide)
```

`SwapExecutionOrderHandler.CanApply`에 조건 추가:

```csharp
&& (ctx.Intervention.TargetSide == null
    || (ctx.Target.Def.Side == ctx.Intervention.TargetSide
        && ctx.SecondaryTarget.Def.Side == ctx.Intervention.TargetSide))
&& AreAdjacentIfRequired(ctx)
```

```csharp
private static bool AreAdjacentIfRequired(InterventionPlayContext ctx)
{
    if (!ctx.Intervention.RequireAdjacentTargets)
    {
        return true;
    }

    var order = ctx.State.Zone.ResolutionOrder();
    var first = IndexOf(order, ctx.Target);
    var second = IndexOf(order, ctx.SecondaryTarget);
    return first >= 0 && second >= 0 && (first - second == 1 || second - first == 1);
}

private static int IndexOf(
    System.Collections.Generic.IReadOnlyList<Combat.ExecutionCardInstance> order,
    Combat.ExecutionCardInstance card)
{
    for (int i = 0; i < order.Count; i++)
    {
        if (ReferenceEquals(order[i], card))
        {
            return i;
        }
    }

    return -1;
}
```

`CardSpec.cs`에 추가:

```csharp
public InterventionTargetSideRef InterventionTargetSide;
public bool InterventionRequireAdjacent;
```

같은 파일(또는 `EffectSpec.cs`의 enum 옆)에:

```csharp
public enum InterventionTargetSideRef { Any, Player, Enemy }
```

`CardSpecMapper.ToDefinition`의 개입 분기 교체:

```csharp
InterventionAction = new InterventionActionData(
    spec.Intervention.ToKey(), spec.EnergyCost, spec.InterventionEffectValue,
    ToTargetSide(spec.InterventionTargetSide), spec.InterventionRequireAdjacent)
```

```csharp
private static Side? ToTargetSide(InterventionTargetSideRef side)
{
    switch (side)
    {
        case InterventionTargetSideRef.Player: return Side.Player;
        case InterventionTargetSideRef.Enemy: return Side.Enemy;
        default: return null;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS (기존 3-인자 생성자 유지로 기존 콘텐츠 불변).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Intervention/InterventionActionData.cs Assets/Core/Intervention/ChangeExecutionOrderHandler.cs Assets/Core/Intervention/SwapExecutionOrderHandler.cs Assets/Core/Simulation/Authoring/CardSpec.cs Assets/Core/Simulation/Authoring/CardSpecMapper.cs Assets/Core/Tests/EditMode/InterventionConstraintTests.cs
git commit -m "feat(core): intervention target-side filter and adjacent-swap constraint"
```

---

### Task 11: 저작 스펙 3종 + ConditionKind 확장 + 설명 핸들러

**Files:**
- Create: `Assets/Core/Simulation/Authoring/Specs/ConsumeStatusSpec.cs`, `TriggerStatusSpec.cs`, `GrantNextTurnFateSpec.cs`
- Modify: `Assets/Core/Simulation/Authoring/EffectSpec.cs` (`ConditionKind`/`ConditionSpec`), `Assets/Core/Simulation/Authoring/EffectSpecCatalog.cs`, `Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs`, `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Test: `Assets/Core/Tests/EditMode/NewEffectSpecTests.cs` (신규)

**Interfaces:**
- Consumes: Task 7~9의 효과 키·페이로드, Task 10의 스펙 필드
- Produces:
  - `ConditionKind` 신규 값: `NoFollowingPlayerCard`(마지막 한 방울), `ConsumedStatusAtLeast`(N 사용)
  - `ConditionSpec.SkipOnBasic : bool` → `EffectData.SkipOnBasic`으로 전달
  - `ConsumeStatusSpec { StatusKeyRef Status; int MaxAmount; int DamageBonusPerConsumed; TargetSelectorRef Selector; }`
  - `TriggerStatusSpec { StatusKeyRef Status; StatusKeyRef SuppressMarker; TargetSelectorRef Selector; }`
  - `GrantNextTurnFateSpec { int Value; }`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class NewEffectSpecTests
    {
        [Test]
        public void Consume_status_spec_maps_payload_selector_and_condition()
        {
            var spec = new ConsumeStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Poison),
                MaxAmount = 3,
                DamageBonusPerConsumed = 2,
                Selector = TargetSelectorRef.FrontMost
            };
            var effect = spec.ToEffectData();

            Assert.AreEqual(EffectKeys.ConsumeStatus, effect.Key);
            var payload = (ConsumeStatusPayload)effect.Payload;
            Assert.AreEqual(StatusKeys.Poison, payload.Key);
            Assert.AreEqual(3, payload.MaxAmount);
            Assert.AreEqual(2, payload.DamageBonusPerConsumed);
            Assert.IsEmpty(spec.Validate(AuthoringContext.Default()).ToList());
        }

        [Test]
        public void Condition_spec_maps_new_kinds_and_skip_on_basic()
        {
            var noFollowing = new ConditionSpec
                { Kind = ConditionKind.NoFollowingPlayerCard, SuccessEffectValue = 2 };
            Assert.IsInstanceOf<NoFollowingCardOfSide>(noFollowing.ToCondition());
            Assert.AreEqual(FateWeaver.Core.Cards.Side.Player,
                ((NoFollowingCardOfSide)noFollowing.ToCondition()).Side);

            var consumed = new ConditionSpec
                { Kind = ConditionKind.ConsumedStatusAtLeast, N = 1, SuccessEffectValue = 4, SkipOnBasic = true };
            Assert.AreEqual(1, ((ConsumedStatusAtLeast)consumed.ToCondition()).N);

            var spec = new GrantNextTurnFateSpec { Value = 1, Condition = consumed };
            var effect = spec.ToEffectData();
            Assert.IsTrue(effect.SkipOnBasic);
            Assert.AreEqual(EffectKeys.GrantNextTurnFate, effect.Key);
        }

        [Test]
        public void Catalog_lists_the_three_new_specs()
        {
            var types = EffectSpecCatalog.All().Select(i => i.SpecType).ToList();
            CollectionAssert.Contains(types, typeof(ConsumeStatusSpec));
            CollectionAssert.Contains(types, typeof(TriggerStatusSpec));
            CollectionAssert.Contains(types, typeof(GrantNextTurnFateSpec));
        }

        [Test]
        public void Descriptions_resolve_for_all_new_effect_keys()
        {
            var catalog = FateWeaver.Simulation.Descriptions.KoreanDescriptionCatalog.CreateDefault();
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.ConsumeStatus));
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.TriggerStatus));
            Assert.IsNotNull(catalog.Effects.Resolve(EffectKeys.GrantNextTurnFate));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~NewEffectSpecTests"`
Expected: 컴파일 실패 — 신규 스펙 타입 미정의.

- [ ] **Step 3: 구현**

`EffectSpec.cs`:

1. `ConditionKind`에 `NoFollowingPlayerCard`, `ConsumedStatusAtLeast` 추가.
2. `ConditionSpec`에 `public bool SkipOnBasic;` 필드 추가, `ToCondition()` switch에:

```csharp
case ConditionKind.NoFollowingPlayerCard:
    return new NoFollowingCardOfSide(Side.Player);
case ConditionKind.ConsumedStatusAtLeast:
    return new ConsumedStatusAtLeast(N);
```

3. `ApplyCondition`을 SkipOnBasic 전달로 교체:

```csharp
protected EffectData ApplyCondition(EffectData effect)
    => Condition.Kind == ConditionKind.None
        ? effect
        : effect with
        {
            Condition = Condition.ToCondition(),
            SuccessEffectValue = Condition.SuccessEffectValue,
            SkipOnBasic = Condition.SkipOnBasic
        };
```

4. `ConditionLiteral()`에 `+ ", SkipOnBasic = " + (Condition.SkipOnBasic ? "true" : "false")` 추가.

`ConsumeStatusSpec.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>대상 적의 상태를 최대치까지 소비한다 (소비형 독 카드).</summary>
    [Serializable]
    public sealed class ConsumeStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public int MaxAmount;
        public int DamageBonusPerConsumed;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.ConsumeStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, 0)
            {
                Payload = new ConsumeStatusPayload(Status.ToKey(), MaxAmount, DamageBonusPerConsumed)
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty)
            {
                yield return "consume_status spec requires a status key.";
            }
            else if (!context.HasStatus(Status.ToKey()))
            {
                yield return "Unknown status key '" + Status.Id + "'.";
            }

            if (MaxAmount < 1)
            {
                yield return "consume_status MaxAmount must be at least 1.";
            }
        }

        public override string ToLiteral()
            => "new ConsumeStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", MaxAmount = " + MaxAmount
                + ", DamageBonusPerConsumed = " + DamageBonusPerConsumed
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
```

`TriggerStatusSpec.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>대상 적의 상태 틱을 즉시 발동시키고 이번 턴 종료 발동을 마커로 막는다 (조기 발병).</summary>
    [Serializable]
    public sealed class TriggerStatusSpec : EffectSpec
    {
        public StatusKeyRef Status;
        public StatusKeyRef SuppressMarker;
        public TargetSelectorRef Selector;

        public override EffectKey Key => EffectKeys.TriggerStatus;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, 0)
            {
                Payload = new TriggerStatusPayload(Status.ToKey(), SuppressMarker.ToKey())
            }) with { TargetSelector = ToSelector(Selector) };

        public override IEnumerable<string> Validate(AuthoringContext context)
        {
            if (Status.IsEmpty || !context.HasStatus(Status.ToKey()))
            {
                yield return "trigger_status spec requires a known status key.";
            }

            if (SuppressMarker.IsEmpty || !context.HasStatus(SuppressMarker.ToKey()))
            {
                yield return "trigger_status spec requires a known suppress-marker key.";
            }
        }

        public override string ToLiteral()
            => "new TriggerStatusSpec { Status = new StatusKeyRef { Id = " + Quote(Status.Id) + " }"
                + ", SuppressMarker = new StatusKeyRef { Id = " + Quote(SuppressMarker.Id) + " }"
                + ", Selector = TargetSelectorRef." + Selector
                + ", " + ConditionLiteral() + " }";
    }
}
```

`GrantNextTurnFateSpec.cs`:

```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>다음 플레이어 사용 턴에 운명력 Value를 준다 (증류).</summary>
    [Serializable]
    public sealed class GrantNextTurnFateSpec : EffectSpec
    {
        public int Value;

        public override EffectKey Key => EffectKeys.GrantNextTurnFate;

        public override EffectData ToEffectData()
            => ApplyCondition(new EffectData(Key, Value));

        public override string ToLiteral()
            => "new GrantNextTurnFateSpec { Value = " + Value + ", " + ConditionLiteral() + " }";
    }
}
```

`EffectSpecCatalog.All()`에 추가:

```csharp
new EffectSpecInfo("상태 소비", typeof(ConsumeStatusSpec), () => new ConsumeStatusSpec()),
new EffectSpecInfo("상태 즉시 발동", typeof(TriggerStatusSpec), () => new TriggerStatusSpec()),
new EffectSpecInfo("다음 턴 운명력", typeof(GrantNextTurnFateSpec), () => new GrantNextTurnFateSpec())
```

`BuiltInEffectDescriptionHandlers.cs`에 추가:

```csharp
public sealed class ConsumeStatusDescriptionHandler : IEffectDescriptionHandler
{
    public EffectKey Key => EffectKeys.ConsumeStatus;

    public string Describe(EffectData effect, int effectValue, DescriptionContext context)
    {
        if (!(effect.Payload is ConsumeStatusPayload payload))
            throw new ArgumentException(
                "Consume-status description requires a ConsumeStatusPayload.", nameof(effect));

        var text = context.TargetPrefix(effect)
            + context.Statuses.Resolve(payload.Key) + " 최대 " + payload.MaxAmount + " 소비";
        return payload.DamageBonusPerConsumed > 0
            ? text + " (소비 1당 피해 +" + payload.DamageBonusPerConsumed + ")"
            : text;
    }
}

public sealed class TriggerStatusDescriptionHandler : IEffectDescriptionHandler
{
    public EffectKey Key => EffectKeys.TriggerStatus;

    public string Describe(EffectData effect, int effectValue, DescriptionContext context)
    {
        if (!(effect.Payload is TriggerStatusPayload payload))
            throw new ArgumentException(
                "Trigger-status description requires a TriggerStatusPayload.", nameof(effect));

        return context.TargetPrefix(effect)
            + context.Statuses.Resolve(payload.Key) + " 즉시 발동 (이번 턴 종료에는 발동하지 않음)";
    }
}

public sealed class GrantNextTurnFateDescriptionHandler : IEffectDescriptionHandler
{
    public EffectKey Key => EffectKeys.GrantNextTurnFate;

    public string Describe(EffectData effect, int effectValue, DescriptionContext context)
        => "다음 사용 턴에 운명력 " + effectValue + " 획득";
}
```

`KoreanDescriptionCatalog.CreateDefault()`의 효과 등록에:

```csharp
effects.Register(new ConsumeStatusDescriptionHandler());
effects.Register(new TriggerStatusDescriptionHandler());
effects.Register(new GrantNextTurnFateDescriptionHandler());
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS. `ConditionLiteral` 출력 변경으로 codegen 리터럴을 문자열 비교하는 테스트가 있으면(검색: `SkipOnBasic` 전후의 `ConditionSpec {` 리터럴 기대값) 기대 문자열을 갱신.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Authoring/Specs/ConsumeStatusSpec.cs Assets/Core/Simulation/Authoring/Specs/TriggerStatusSpec.cs Assets/Core/Simulation/Authoring/Specs/GrantNextTurnFateSpec.cs Assets/Core/Simulation/Authoring/EffectSpec.cs Assets/Core/Simulation/Authoring/EffectSpecCatalog.cs Assets/Core/Simulation/Descriptions/BuiltInEffectDescriptionHandlers.cs Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs Assets/Core/Tests/EditMode/NewEffectSpecTests.cs
git commit -m "feat(core): authoring specs and descriptions for consume/trigger/fate effects"
```

---

### Task 12: 시작 카드 풀 저작 1 — 일반·조작 12장

**Files:**
- Create: `Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs`
- Test: `Assets/Core/Tests/EditMode/StarterPoolSpecsTests.cs` (신규)

**Interfaces:**
- Consumes: 지금까지의 모든 스펙·제약. `StarterDeckSpecs` 저작 패턴
- Produces: `StarterPoolSpecs.VanguardSlash()` 등 팩토리 12종 + `StarterPoolSpecs.Build()` (Task 13에서 22장으로 확장). 카드 id는 아래 표 고정 — Task 13·검증 테스트가 참조한다.

| id | 카드 | 요지 |
|---|---|---|
| `vanguard_slash` | 선봉 베기 | 순서3, 피해 5 |
| `parry_strike` | 쳐내기 | 순서5, 피해 1 + 자신 방어 3 |
| `hasten` | 재촉 | 개입 -1, 아군 대상 |
| `probing_strike` | 견제타 | 순서4, 피해 4 + 자신 방어 1 |
| `quick_cover` | 빠른 엄호 | 순서4, 아군 앞 하나 방어 4 |
| `delay` | 유예 | 개입 +1, 적 대상 |
| `delayed_strike` | 늦춘 일격 | 순서5, 피해 5 |
| `early_guard` | 앞선 대비 | 순서4, 자신 방어 4 |
| `crossover` | 엇갈림 | 개입 교환, 인접 필수 |
| `riposte` | 응수 | 순서5, 피해 3→7 (직전 적 피해 카드) |
| `foresight` | 예견 | 순서5, 방어 2→6 (다음 적 피해 카드) |
| `breather` | 숨 고르기 | 개입 +1, 아군 대상 |

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class StarterPoolSpecsTests
    {
        [Test]
        public void All_pool_specs_validate_against_default_registries()
        {
            var errors = AuthoringValidator.Validate(
                StarterPoolSpecs.Build(), AuthoringContext.Default());
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Riposte_after_enemy_damage_card_deals_boosted_damage()
        {
            var state = NewState();
            var enemyJab = StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", 4, 1);
            state.Zone.Add(new ExecutionCardInstance(enemyJab) { OwnerId = "goblin" });
            state.Zone.Add(new ExecutionCardInstance(
                CardSpecMapper.ToDefinition(StarterPoolSpecs.Riposte()))
                { OwnerId = CombatState.SoloPlayerId });

            var events = Resolve(state);

            Assert.AreEqual(7, events.OfType<CardResolved>()
                .Single(e => e.CardId == "riposte").DamageDealt);
        }

        [Test]
        public void Quick_cover_blocks_the_front_ally_not_the_owner()
        {
            var state = new CombatState();
            state.Party.Add(new PartyMember("front", "F", 20));
            state.Party.Add(new PartyMember("back", "B", 20));
            state.Enemies.Add(new Enemy("goblin", 30));
            var enemyJab = StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", 9, 4);
            state.Zone.Add(new ExecutionCardInstance(enemyJab) { OwnerId = "goblin" });
            state.Zone.Add(new ExecutionCardInstance(
                CardSpecMapper.ToDefinition(StarterPoolSpecs.QuickCover()))
                { OwnerId = "back" });

            Resolve(state);

            Assert.AreEqual(20, state.Party[0].Hp);   // 전열이 방어 4로 흡수
        }

        [Test]
        public void Crossover_swaps_only_adjacent_unlocked_cards()
        {
            var def = CardSpecMapper.ToDefinition(StarterPoolSpecs.Crossover());
            Assert.AreEqual(CardCategory.Intervention, def.Category);
            Assert.IsTrue(def.InterventionAction.RequireAdjacentTargets);
        }

        [Test]
        public void Hasten_targets_player_cards_and_delay_targets_enemy_cards()
        {
            Assert.AreEqual(Side.Player,
                CardSpecMapper.ToDefinition(StarterPoolSpecs.Hasten()).InterventionAction.TargetSide);
            Assert.AreEqual(-1,
                CardSpecMapper.ToDefinition(StarterPoolSpecs.Hasten()).InterventionAction.EffectValue);
            Assert.AreEqual(Side.Enemy,
                CardSpecMapper.ToDefinition(StarterPoolSpecs.Delay()).InterventionAction.TargetSide);
            Assert.AreEqual(Side.Player,
                CardSpecMapper.ToDefinition(StarterPoolSpecs.Breather()).InterventionAction.TargetSide);
            Assert.AreEqual(1,
                CardSpecMapper.ToDefinition(StarterPoolSpecs.Breather()).InterventionAction.EffectValue);
        }

        private static CombatState NewState()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 30));
            return state;
        }

        private static System.Collections.Generic.List<ResolutionEvent> Resolve(CombatState state)
            => new TurnResolver(CombatRegistriesAccessor.Effects(), CombatRegistriesAccessor.Statuses())
                .Resolve(state, 0);
    }

    /// <summary>CombatRegistries는 internal — 테스트가 쓰는 기본 레지스트리 접근자.
    /// (AuthoringContext.Default()와 같은 구성을 노출하는 헬퍼가 이미 있으면 그걸 사용한다.)</summary>
    internal static class CombatRegistriesAccessor
    {
        public static FateWeaver.Core.Effects.EffectRegistry Effects()
        {
            var effects = new FateWeaver.Core.Effects.EffectRegistry();
            effects.Register(new FateWeaver.Core.Effects.DamageHandler());
            effects.Register(new FateWeaver.Core.Effects.ApplyStatusHandler());
            effects.Register(new FateWeaver.Core.Effects.ConsumeStatusHandler());
            effects.Register(new FateWeaver.Core.Effects.TriggerStatusHandler());
            effects.Register(new FateWeaver.Core.Effects.GrantNextTurnFateHandler());
            effects.Register(new FateWeaver.Core.Effects.MoveFormationHandler());
            effects.Register(new FateWeaver.Core.Effects.NullifyNextPlayerConditionRewardHandler());
            effects.Register(new FateWeaver.Core.Effects.GrantNextPlayerDamageCardBonusHandler());
            return effects;
        }

        public static StatusRegistry Statuses()
        {
            var statuses = new StatusRegistry();
            statuses.Register(new StunBehavior());
            statuses.Register(new VulnerableBehavior());
            statuses.Register(new RewardSuppressionBehavior());
            statuses.Register(new BlockBehavior());
            statuses.Register(new SlowBehavior());
            statuses.Register(new HasteBehavior());
            statuses.Register(new PoisonBehavior(growthPerTurn: 1));
            statuses.Register(new PoisonDormantBehavior());
            statuses.Register(new PoisonStasisBehavior());
            statuses.Register(new ContagionBehavior());
            return statuses;
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~StarterPoolSpecsTests"`
Expected: 컴파일 실패 — `StarterPoolSpecs` 미정의.

- [ ] **Step 3: 구현**

`StarterPoolSpecs.cs` (Task 13에서 독 카드 팩토리가 이어 붙는다):

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>시작 카드 풀 22장 (Tools/card-idea-notebook/시작 카드 풀.md, 2026-07-29).
    /// StarterDeckSpecs와 같은 순수 CardSpec 저작 — SO 미러링은 병합 후 메인 체크아웃에서 진행.</summary>
    public static class StarterPoolSpecs
    {
        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            VanguardSlash(), ParryStrike(), Hasten(), ProbingStrike(), QuickCover(), Delay(),
            DelayedStrike(), EarlyGuard(), Crossover(), Riposte(), Foresight(), Breather()
            // Task 13: 독 카드 10장이 여기 추가된다.
        };

        public static CardSpec VanguardSlash() => new CardSpec
        {
            Id = "vanguard_slash", Name = "선봉 베기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 3,
            Effects = new EffectSpec[] { new DamageSpec { Value = 5 } }
        };

        public static CardSpec ParryStrike() => new CardSpec
        {
            Id = "parry_strike", Name = "쳐내기", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new DamageSpec { Value = 1 },
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 3,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec Hasten() => new CardSpec
        {
            Id = "hasten", Name = "재촉", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = -1,
            InterventionTargetSide = InterventionTargetSideRef.Player
        };

        public static CardSpec ProbingStrike() => new CardSpec
        {
            Id = "probing_strike", Name = "견제타", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
            {
                new DamageSpec { Value = 4 },
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 1,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec QuickCover() => new CardSpec
        {
            Id = "quick_cover", Name = "빠른 엄호", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
            {
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                    Lifetime = StatusLifetimeKind.ThisTurn,
                    Target = StatusApplyTarget.PartyBySelector, Selector = TargetSelectorRef.FrontMost
                }
            }
        };

        public static CardSpec Delay() => new CardSpec
        {
            Id = "delay", Name = "유예", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = 1,
            InterventionTargetSide = InterventionTargetSideRef.Enemy
        };

        public static CardSpec DelayedStrike() => new CardSpec
        {
            Id = "delayed_strike", Name = "늦춘 일격", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[] { new DamageSpec { Value = 5 } }
        };

        public static CardSpec EarlyGuard() => new CardSpec
        {
            Id = "early_guard", Name = "앞선 대비", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
            Effects = new EffectSpec[]
            {
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
                }
            }
        };

        public static CardSpec Crossover() => new CardSpec
        {
            Id = "crossover", Name = "엇갈림", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.SwapExecutionOrder),
            InterventionRequireAdjacent = true
        };

        public static CardSpec Riposte() => new CardSpec
        {
            Id = "riposte", Name = "응수", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new DamageSpec
                {
                    Value = 3,
                    Condition = new ConditionSpec
                    {
                        Kind = ConditionKind.PrevExecutedIsEnemyDamageCard, SuccessEffectValue = 7
                    }
                }
            }
        };

        public static CardSpec Foresight() => new CardSpec
        {
            Id = "foresight", Name = "예견", Side = Side.Player,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new EffectSpec[]
            {
                new ApplyStatusSpec
                {
                    Status = StatusKeyRef.Of(StatusKeys.Block), Value = 2,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                    Condition = new ConditionSpec
                    {
                        Kind = ConditionKind.NextIsEnemyDamageCard, SuccessEffectValue = 6
                    }
                }
            }
        };

        public static CardSpec Breather() => new CardSpec
        {
            Id = "breather", Name = "숨 고르기", Side = Side.Player,
            Category = CardCategory.Intervention, EnergyCost = 1,
            Intervention = InterventionKeyRef.Of(InterventionActionKeys.ChangeExecutionOrder),
            InterventionEffectValue = 1,
            InterventionTargetSide = InterventionTargetSideRef.Player
        };
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS (`StarterDeck.EnemyAttack` 시그니처가 다르면 테스트 쪽을 실제 시그니처에 맞춘다).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs Assets/Core/Tests/EditMode/StarterPoolSpecsTests.cs
git commit -m "feat(core): author starter pool part 1 (attack/defense/intervention cards)"
```

---

### Task 13: 시작 카드 풀 저작 2 — 독 10장 + 통합 검증

**Files:**
- Modify: `Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs`
- Test: `Assets/Core/Tests/EditMode/StarterPoolPoisonTests.cs` (신규)

**Interfaces:**
- Consumes: Task 12의 `StarterPoolSpecs`, Task 5~9의 독·효과, Task 12 테스트의 `CombatRegistriesAccessor`
- Produces: 팩토리 10종 + `Build()` 22장 완성. id: `venom_thrust`, `last_drop`, `spore_veil`, `spread_culture`, `toxic_reclaim`, `condensed_burst`, `distill`, `early_onset`, `stable_culture`, `posthumous_spread`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class StarterPoolPoisonTests
    {
        private static CombatState NewState(params Enemy[] enemies)
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            foreach (var enemy in enemies) state.Enemies.Add(enemy);
            return state;
        }

        private static ExecutionCardInstance Place(CombatState state, CardSpec spec)
        {
            var card = new ExecutionCardInstance(CardSpecMapper.ToDefinition(spec))
                { OwnerId = CombatState.SoloPlayerId };
            state.Zone.Add(card);
            return card;
        }

        private static System.Collections.Generic.List<ResolutionEvent> Resolve(CombatState state)
            => new TurnResolver(CombatRegistriesAccessor.Effects(), CombatRegistriesAccessor.Statuses())
                .Resolve(state, 0);

        [Test]
        public void Build_contains_all_22_cards_and_validates()
        {
            Assert.AreEqual(22, StarterPoolSpecs.Build().Count);
            CollectionAssert.IsEmpty(AuthoringValidator.Validate(
                StarterPoolSpecs.Build(), AuthoringContext.Default()));
        }

        [Test]
        public void Venom_thrust_deals_2_applies_poison_1_which_ticks_and_grows()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, StarterPoolSpecs.VenomThrust());

            var events = Resolve(state);

            // 피해 2 + 턴 종료 독 1 = 17. 독은 2로 성장.
            Assert.AreEqual(17, state.Enemies[0].Hp);
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
            Assert.AreEqual(1, events.OfType<StatusTicked>().Single().Damage);
        }

        [Test]
        public void Last_drop_applies_2_when_no_player_card_follows()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, StarterPoolSpecs.VanguardSlash()); // 순서 3, 먼저
            Place(state, StarterPoolSpecs.LastDrop());      // 순서 7, 마지막 → 독 2

            Resolve(state);

            // 독 2 부여 → 턴 종료 2 피해 + 성장 → 독 3. HP: 20 - 5(선봉) - 2 = 13.
            Assert.AreEqual(13, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Spread_culture_hits_and_poisons_every_living_enemy()
        {
            var state = NewState(new Enemy("front", 20), new Enemy("back", 20));
            Place(state, StarterPoolSpecs.SpreadCulture());

            Resolve(state);

            // 각각 피해 2 + 독 1 틱 = 17, 독 2로 성장.
            foreach (var enemy in state.Enemies)
            {
                Assert.AreEqual(17, enemy.Hp);
                Assert.AreEqual(2, enemy.Statuses.Get(StatusKeys.Poison).Magnitude);
            }
        }

        [Test]
        public void Condensed_burst_consumes_up_to_3_for_scaled_damage_then_reapplies_1()
        {
            var state = NewState(new Enemy("goblin", 30));
            state.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 4);
            Place(state, StarterPoolSpecs.CondensedBurst());

            var events = Resolve(state);

            // 소비 3 → 피해 2+6=8. 남은 독 1 + 재부여 1 = 2 → 틱 2 → 독 3.
            Assert.AreEqual(8, events.OfType<CardResolved>().Single().DamageDealt);
            Assert.AreEqual(30 - 8 - 2, state.Enemies[0].Hp);
            Assert.AreEqual(3, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Toxic_reclaim_blocks_only_after_a_real_consume()
        {
            // 독 없음: 방어 없음, 독 1만 부여됨.
            var without = NewState(new Enemy("goblin", 20));
            Place(without, StarterPoolSpecs.ToxicReclaim());
            Resolve(without);
            Assert.IsFalse(without.Party[0].Statuses.Has(StatusKeys.Block));
            Assert.AreEqual(2, without.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude); // 1 부여→틱 성장

            // 독 있음: 1 소비 후 재부여, 자신 방어 4.
            var with = NewState(new Enemy("goblin", 20));
            with.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            var card = Place(with, StarterPoolSpecs.ToxicReclaim());
            Resolve(with);
            Assert.AreEqual(1, card.ConsumedStatusAmount);
        }

        [Test]
        public void Distill_banks_fate_only_when_poison_was_consumed()
        {
            var with = NewState(new Enemy("goblin", 20));
            with.Enemies[0].Statuses.Stack(StatusKeys.Poison, StatusLifetime.Permanent, 1);
            Place(with, StarterPoolSpecs.Distill());
            Resolve(with);
            Assert.AreEqual(1, with.PendingNextTurnFateEnergy);

            var without = NewState(new Enemy("goblin", 20));
            Place(without, StarterPoolSpecs.Distill());
            Resolve(without);
            Assert.AreEqual(0, without.PendingNextTurnFateEnergy);
        }

        [Test]
        public void Early_onset_moves_the_tick_earlier_without_adding_one()
        {
            var state = NewState(new Enemy("goblin", 20));
            Place(state, StarterPoolSpecs.EarlyOnset());

            var events = Resolve(state);

            Assert.AreEqual(19, state.Enemies[0].Hp);   // 즉시 발동 1회만
            Assert.AreEqual(1, events.OfType<StatusTicked>().Count());
            Assert.AreEqual(2, state.Enemies[0].Statuses.Get(StatusKeys.Poison).Magnitude);
        }

        [Test]
        public void Stable_culture_poisons_the_back_enemy_without_growth_this_turn()
        {
            var state = NewState(new Enemy("front", 20), new Enemy("back", 20));
            Place(state, StarterPoolSpecs.StableCulture());

            Resolve(state);

            Assert.IsFalse(state.Enemies[0].Statuses.Has(StatusKeys.Poison));
            Assert.AreEqual(18, state.Enemies[1].Hp);   // 독 2 피해
            Assert.AreEqual(2, state.Enemies[1].Statuses.Get(StatusKeys.Poison).Magnitude); // 성장 없음
        }

        [Test]
        public void Posthumous_spread_marks_the_target_for_on_death_transfer()
        {
            var state = NewState(new Enemy("victim", 2), new Enemy("next", 20));
            Place(state, StarterPoolSpecs.PosthumousSpread());  // 피해 1 + 독 1 + 전염
            Place(state, StarterPoolSpecs.VanguardSlash());     // 피해 5 → 처치

            var events = Resolve(state);

            Assert.IsTrue(events.OfType<EnemyDied>().Any(e => e.EnemyId == "victim"));
            var transfer = events.OfType<StatusTransferred>().Single();
            Assert.AreEqual("next", transfer.ToHolderId);
            // 이전받은 독 1이 턴 종료 발동 → next는 19, 독 2.
            Assert.AreEqual(19, state.Enemies[1].Hp);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter "FullyQualifiedName~StarterPoolPoisonTests"`
Expected: 컴파일 실패 — `VenomThrust` 등 미정의.

- [ ] **Step 3: 구현**

`StarterPoolSpecs.cs`의 `Build()`를 22장으로 확장하고 팩토리 추가:

```csharp
public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
{
    VanguardSlash(), ParryStrike(), Hasten(), ProbingStrike(), QuickCover(), Delay(),
    DelayedStrike(), EarlyGuard(), Crossover(), Riposte(), Foresight(), Breather(),
    VenomThrust(), LastDrop(), SporeVeil(), SpreadCulture(), ToxicReclaim(),
    CondensedBurst(), Distill(), EarlyOnset(), StableCulture(), PosthumousSpread()
};

private static ApplyStatusSpec PoisonApply(int value) => new ApplyStatusSpec
{
    Status = StatusKeyRef.Of(StatusKeys.Poison), Value = value,
    Lifetime = StatusLifetimeKind.Permanent, Target = StatusApplyTarget.TargetEnemy
};

public static CardSpec VenomThrust() => new CardSpec
{
    Id = "venom_thrust", Name = "맹독 찌르기", Side = Side.Player,
    Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
    Effects = new EffectSpec[] { new DamageSpec { Value = 2 }, PoisonApply(1) }
};

public static CardSpec LastDrop()
{
    var poison = PoisonApply(1);
    poison.Condition = new ConditionSpec
    {
        Kind = ConditionKind.NoFollowingPlayerCard, SuccessEffectValue = 2
    };
    return new CardSpec
    {
        Id = "last_drop", Name = "마지막 한 방울", Side = Side.Player,
        Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 7,
        Effects = new EffectSpec[] { poison }
    };
}

public static CardSpec SporeVeil() => new CardSpec
{
    Id = "spore_veil", Name = "포자막", Side = Side.Player,
    Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
    Effects = new EffectSpec[]
    {
        PoisonApply(1),
        new ApplyStatusSpec
        {
            Status = StatusKeyRef.Of(StatusKeys.Block), Value = 2,
            Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self
        }
    }
};

public static CardSpec SpreadCulture()
{
    var poison = PoisonApply(1);
    poison.Selector = TargetSelectorRef.All;
    return new CardSpec
    {
        Id = "spread_culture", Name = "확산 배양", Side = Side.Player,
        Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 6,
        Effects = new EffectSpec[]
        {
            new DamageSpec { Value = 2, Selector = TargetSelectorRef.All },
            poison
        }
    };
}

public static CardSpec ToxicReclaim()
{
    var block = new ApplyStatusSpec
    {
        Status = StatusKeyRef.Of(StatusKeys.Block), Value = 4,
        Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
        Condition = new ConditionSpec
        {
            Kind = ConditionKind.ConsumedStatusAtLeast, N = 1,
            SuccessEffectValue = 4, SkipOnBasic = true
        }
    };
    return new CardSpec
    {
        Id = "toxic_reclaim", Name = "독성 환원", Side = Side.Player,
        Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
        Effects = new EffectSpec[]
        {
            new ConsumeStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Poison), MaxAmount = 1
            },
            PoisonApply(1),
            block
        }
    };
}

public static CardSpec CondensedBurst() => new CardSpec
{
    Id = "condensed_burst", Name = "응축 파열", Side = Side.Player,
    Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 6,
    Effects = new EffectSpec[]
    {
        new ConsumeStatusSpec
        {
            Status = StatusKeyRef.Of(StatusKeys.Poison), MaxAmount = 3, DamageBonusPerConsumed = 2
        },
        new DamageSpec { Value = 2 },
        PoisonApply(1)
    }
};

public static CardSpec Distill()
{
    var fate = new GrantNextTurnFateSpec
    {
        Value = 1,
        Condition = new ConditionSpec
        {
            Kind = ConditionKind.ConsumedStatusAtLeast, N = 1,
            SuccessEffectValue = 1, SkipOnBasic = true
        }
    };
    return new CardSpec
    {
        Id = "distill", Name = "증류", Side = Side.Player,
        Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
        Effects = new EffectSpec[]
        {
            new ConsumeStatusSpec
            {
                Status = StatusKeyRef.Of(StatusKeys.Poison), MaxAmount = 1
            },
            PoisonApply(1),
            fate
        }
    };
}

public static CardSpec EarlyOnset() => new CardSpec
{
    Id = "early_onset", Name = "조기 발병", Side = Side.Player,
    Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 3,
    Effects = new EffectSpec[]
    {
        PoisonApply(1),
        new TriggerStatusSpec
        {
            Status = StatusKeyRef.Of(StatusKeys.Poison),
            SuppressMarker = StatusKeyRef.Of(StatusKeys.PoisonDormant)
        }
    }
};

public static CardSpec StableCulture()
{
    var poison = PoisonApply(2);
    poison.Selector = TargetSelectorRef.BackMost;
    var stasis = new ApplyStatusSpec
    {
        Status = StatusKeyRef.Of(StatusKeys.PoisonStasis), Value = 0,
        Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.TargetEnemy,
        Selector = TargetSelectorRef.BackMost
    };
    return new CardSpec
    {
        Id = "stable_culture", Name = "안정 배양", Side = Side.Player,
        Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
        Effects = new EffectSpec[] { poison, stasis }
    };
}

public static CardSpec PosthumousSpread()
{
    var contagion = new ApplyStatusSpec
    {
        Status = StatusKeyRef.Of(StatusKeys.Contagion), Value = 0,
        Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2,
        Target = StatusApplyTarget.TargetEnemy
    };
    return new CardSpec
    {
        Id = "posthumous_spread", Name = "사후 전염", Side = Side.Player,
        Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 4,
        Effects = new EffectSpec[]
        {
            new DamageSpec { Value = 1 },
            PoisonApply(1),
            contagion
        }
    };
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core/Simulation/Authoring/StarterPoolSpecs.cs Assets/Core/Tests/EditMode/StarterPoolPoisonTests.cs
git commit -m "feat(core): author starter pool part 2 (poison archetype cards)"
```

---

### Task 14: 전체 검증 + 문서 색인 갱신

**Files:**
- Modify: `docs/superpowers/README.md` (활성 계획 표에 이 계획 추가 — 미반영 시)
- Modify: `Tools/card-idea-notebook/시작 카드 풀.md` 는 수정하지 않는다 (저작 원본 보존)

- [ ] **Step 1: 전체 헤드리스 스위트**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0`
Expected: 전체 PASS, 실패 0.

- [ ] **Step 2: 결정론 확인**

`CombatRngDeterminismTests`와 Compare 하니스 관련 테스트가 포함 통과했는지 출력에서 확인한다 (같은 시나리오+시드 = 같은 타임라인, 규칙 7).

- [ ] **Step 3: Unity 배치 EditMode 테스트 (가능한 경우)**

규칙 17에 따라 전용 워크트리에서 `-batchmode` EditMode 실행은 허용된다. Unity 경로가 확인되면:

```bash
"/Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath "$(pwd)" -runTests -testPlatform EditMode -testResults /private/tmp/starter-pool-editmode.xml -quit
```

실행 불가(라이선스/에디터 부재) 시 결과를 보고하고 병합 전 사용자 확인 항목으로 남긴다.

- [ ] **Step 4: 문서 색인 갱신 확인**

`docs/superpowers/README.md`의 "활성 계획과 로드맵" 표에 아래 행이 있는지 확인, 없으면 추가:

```markdown
| [상태 훅·독 시스템·시작 카드 풀](plans/2026-07-29-status-hooks-poison-starter-pool.md) | `active` | 상태 훅 3종, 독 아키타입, 위치 대상, 개입 제약, 시작 카드 22장 |
```

- [ ] **Step 5: 커밋 + 워킹 트리 정리**

```bash
git status
git add docs/superpowers/README.md
git commit -m "docs: index status-hooks/poison/starter-pool plan"
```

`git status`가 깨끗한지 확인한다 (규칙 18).

---

## 범위 밖 (의도적 보류)

- **Unity SO 저작 미러링** — 22장의 CardAsset SO 생성과 새 스펙 타입의 인스펙터 드로어 노출은 프리팹·에셋 저작이라 이 워크트리 범위 밖 (규칙 17). 병합 후 메인 체크아웃 작업으로 전달.
- **독 우선순위 전체 계층 (§3.3의 배율·가감·대체 층)** — 이번 22장은 1층(금지·고정)만 필요. 배율/가감 카드가 설계되면 `PoisonBehavior`의 성장 계산을 계층 파이프라인으로 확장.
- **UI 대상 후보 필터링** — 개입 진영 제한의 UI 미리보기는 `TargetingRequirement` 확장(P0-C §3.1의 보류 항목)과 함께. 현재는 코어 거부(자원 보존)로 충분.
- **`DamageHandler.SelectEnemy` ≡ `ApplyStatusHandler.SelectTargetEnemy` 중복** — Task 4의 `EnemyTargeting.ByIdOrFront`로 해소됨 (백로그 §12.3 완료 처리 가능).
- **위치 범위 '앞 둘'/'뒤 둘'** — 이번 카드에 소비자 없음. 필요 시 `TargetSelector` 값 추가 + 다중 선택 API 확장.

## Self-Review 결과

- 22장 전부에 대응 태스크 존재 (Task 12: 12장, Task 13: 10장). 카드별 요구 메커니즘 ↔ 태스크 매핑: 틱(T2)·사망 이전(T3+T6)·합산(T1+T4)·위치/광역(T4)·소비(T7)·즉시 발동(T8)·운명력(T9)·개입 제약(T10).
- 타입 일관성: `StatusTickContext`/`StatusDeathContext`(T1) ↔ T2·T3·T5·T6·T8 사용처 일치. `ConsumeStatusPayload(Key, MaxAmount, DamageBonusPerConsumed)` T7 정의 ↔ T11·T13 사용 일치. `InterventionTargetSideRef` T10 정의 ↔ T12 사용 일치.
- 알려진 조정 지점(placeholder 아님, 실행 중 확인): `StarterDeck.EnemyAttack` 시그니처(T9/T12), 방어 합산 전환으로 인한 기존 기대값(T4), `EnemyDied` 추가로 인한 타임라인 고정 테스트(T3), `ConditionLiteral` 문자열 기대값(T11).
