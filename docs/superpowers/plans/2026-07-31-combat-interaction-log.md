# 전투 상호작용 로그 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-07-31
- 상태: `active` — 미착수
- 선행: [상태 규칙 파라미터화와 3종 디버프](2026-07-30-status-rule-and-debuffs.md) (Task 1~4 완료)

**Goal:** 전투에서 일어난 모든 상호작용을 결정론적 이벤트로 남기고, 턴 해석 직후 개발용 Console에
사람이 읽을 수 있는 형태로 덤프한다.

**Architecture:** 코어의 출력은 이벤트 타임라인뿐이다(AGENTS.md 규칙 11). 따라서 로그는 별도 채널이
아니라 **타임라인을 넓히고 그것을 읽는 포매터를 두는 것**이다. 포매터는 순수 C#(Simulation)에 두어
헤드리스로 검증하고, Unity는 호출만 한다.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit

## 이 계획이 생긴 이유

앞열 파티원이 HP 1에서 공격을 받고도 죽지 않는 것을 확인했는데, 원인을 화면에서 알 수 없었다.
실제 원인은 치명 버팀([PartyMember.TakeDamage](../../../Assets/Core/Combat/PartyMember.cs))이었고
`DeathsDoorSurvived` 이벤트도 정상 발행되고 있었다. 규칙은 맞게 동작했지만 **배틀 화면이 타임라인을
전혀 표시하지 않아** 규칙이 맞는지 틀리는지 판단할 방법이 없었다.

같은 사각지대가 이번에 추가한 3종 디버프에도 있다. 약화·취약·방어가 피해를 어떻게 바꿨는지는
`CardResolved.DamageDealt` 총합 하나로만 남아, 배율이 실제로 적용됐는지 화면에서 확인할 수 없다.

## Global Constraints

- 헤드리스 테스트: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- ~~착수 시점 기준선: 409 tests, 0 failed~~ — **2026-08-04 기준선은 헤드리스 533, Unity EditMode
  682(674 passed / 8 skipped)**. 착수 세션이 첫 실행에서 다시 실측할 것
- **설명 카탈로그는 주입받는다 (2026-08-04 정정).** 계획 3c가 `KoreanDescriptionCatalog.Default`
  전역과 무인자 `CreateDefault()`를 제거했다. 아래 코드 조각의 `Korean`·`_korean`은
  `KoreanDescriptionCatalog.CreateDefault(<상태 카탈로그>)`로 만들어 둔 값이다 — 테스트는
  `TestContent.Statuses()`(코어) 또는 `UnityTestContent.Statuses()`(Unity), 런타임은
  `GameContent.Statuses`를 넘긴다
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (규칙 6)
- 결정론을 깨지 않는다 — 로그 수집이 규칙 판정이나 RNG 소비 순서를 바꾸면 안 된다 (규칙 7)
- 코어의 출력은 이벤트 타임라인뿐이다. 진단용 별도 출력 채널을 만들지 않는다 (규칙 11)
- 상태 이름을 하드코딩하지 않는다. 포매터는 설명 레지스트리(`KoreanDescriptionCatalog`)에서 가져온다 (규칙 10)
- C# 9 한계: `record struct` 금지, 기본 인터페이스 구현 금지

## 확정된 설계 결정

**1. 피해 계산 내역은 새 이벤트가 아니라 `CardResolved`의 추가 페이로드로 싣는다.**
이벤트를 새로 끼워 넣으면 인덱스로 단언하는 테스트가 깨진다 — 측정해보니 `events[N]` 형태가
5개 파일 27곳이다(`ConditionalEffectResolutionTests`, `DebuffStatusTests`, `InterventionActionTests`,
`StatusTests`, `TurnResolverTests`). `record`에 init-only 프로퍼티를 **기본값과 함께** 추가하면 기존
생성자 호출이 그대로 컴파일되고 인덱스도 밀리지 않는다.

**2. 상태 부여·만료는 새 이벤트로 추가한다.** 지금 타임라인에는 상태가 걸리는 사건 자체가 없다
(`StatusTicked`·`StatusTransferred`만 있다). 페이로드로 표현할 기존 이벤트가 없으므로 새 이벤트가
불가피하고, 인덱스 의존 테스트 일부가 밀린다. 밀리는 테스트는 Task 2에서 함께 고친다.

**3. 표시는 개발용 Console만.** 인게임 전투 로그 패널은 이 계획 범위 밖이다. 포매터를 코어에 두므로
나중에 패널을 붙일 때 그대로 재사용한다.

**4. 로그는 항상 수집한다.** 상세/간략 모드를 나누면 타임라인이 모드에 따라 달라져 "같은 시나리오+
시드 = 같은 타임라인" 테스트가 복잡해진다. 수집은 항상 하고, 얼마나 보여줄지는 포매터가 정한다.

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Core/Events/DamageStep.cs` (신규) | 피해가 한 단계에서 어떻게 바뀌었는지 |
| `Assets/Core/Events/ResolutionEvent.cs` | `CardResolved`에 내역 첨부, `StatusApplied`·`StatusExpired` 추가 |
| `Assets/Core/Status/StatusDamageFold.cs` | 접는 단계를 수집기에 기록 |
| `Assets/Core/Status/StatusBag.cs` | `EndOfTurn`이 만료된 키를 돌려준다 |
| `Assets/Core/Effects/IEffectHandler.cs` | `EffectContext.DamageSteps` |
| `Assets/Core/Effects/DamageHandler.cs` | 접기 호출에 수집기 전달 |
| `Assets/Core/Effects/ApplyStatusHandler.cs` | `StatusApplied` 발행 |
| `Assets/Core/Combat/TurnResolver.cs` | 내역을 `CardResolved`에 첨부, 만료 이벤트 발행 |
| `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs` (신규) | 타임라인 → 한국어 여러 줄 |
| `Assets/Unity/BattleScreenController.cs` | 턴 해석 직후 포매터 호출 (임시 계측 대체) |
| `Assets/Unity/DeckPlaytestController.cs` | 자체 `RefreshTimeline`을 포매터로 교체 |

테스트: `Assets/Core/Tests/EditMode/CombatLogTests.cs` (신규), 기존 인덱스 의존 테스트 갱신.

---

### Task 1: 피해 계산 내역을 CardResolved에 싣는다

약화·취약·방어가 각각 피해를 어떻게 바꿨는지 단계로 남긴다. 새 이벤트를 만들지 않으므로 기존
인덱스 의존 테스트는 무수정이다.

**Files:**
- Create: `Assets/Core/Events/DamageStep.cs`
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Status/StatusDamageFold.cs`,
  `Assets/Core/Effects/IEffectHandler.cs`, `Assets/Core/Effects/DamageHandler.cs`,
  `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Produces:
  - `DamageStep(string HolderId, string StatusId, int Before, int After)`
  - `CardResolved.DamageSteps` (`IReadOnlyList<DamageStep>`, 기본 빈 목록)
  - `StatusDamageFold.Incoming(bag, registry, rules, damage, string holderId, List<DamageStep> trace)`
  - `StatusDamageFold.Outgoing(bag, registry, rules, damage, string holderId, List<DamageStep> trace)`
  - `EffectContext.DamageSteps` (`List<DamageStep>`)

- [ ] **Step 1: 실패하는 테스트를 작성한다**

Create `Assets/Core/Tests/EditMode/CombatLogTests.cs`:

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
    /// <summary>타임라인이 전투 상호작용을 빠짐없이 담는지. 규칙 11에 따라 로그의 원천은
    /// 타임라인 하나뿐이다.</summary>
    public class CombatLogTests
    {
        private static EffectRegistry Effects()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            r.Register(new ApplyStatusHandler());
            return r;
        }

        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new VulnerableBehavior());
            r.Register(new BlockBehavior());
            r.Register(new WeakBehavior());
            r.Register(new DamagedBehavior());
            return r;
        }

        [Test]
        public void Damage_steps_record_weak_then_vulnerable_then_block()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.Turns(2), 5);
            state.Enemies.Add(enemy);

            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 10) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var resolved = events.OfType<CardResolved>().Single();

            // 10 -> 약화 7 -> 취약 10 -> 방어 5 흡수 -> 5
            Assert.AreEqual(
                new[] { "weak", "vulnerable", "block" },
                resolved.DamageSteps.Select(s => s.StatusId).ToArray());
            Assert.AreEqual((10, 7), (resolved.DamageSteps[0].Before, resolved.DamageSteps[0].After));
            Assert.AreEqual((7, 10), (resolved.DamageSteps[1].Before, resolved.DamageSteps[1].After));
            Assert.AreEqual((10, 5), (resolved.DamageSteps[2].Before, resolved.DamageSteps[2].After));
            Assert.AreEqual(5, resolved.DamageDealt);
        }

        [Test]
        public void Damage_steps_name_the_holder_of_each_status()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);

            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 10) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var steps = events.OfType<CardResolved>().Single().DamageSteps;

            Assert.AreEqual(CombatState.SoloPlayerId, steps[0].HolderId); // 약화는 공격자에게
            Assert.AreEqual("goblin", steps[1].HolderId);                 // 취약은 대상에게
        }

        [Test]
        public void A_card_with_no_status_involved_records_no_steps()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.Enemies.Add(new Enemy("goblin", 30));
            var def = new CardDefinition("strike", "strike", Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, 4) });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsEmpty(events.OfType<CardResolved>().Single().DamageSteps);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~CombatLogTests"`

Expected: 컴파일 실패 — `DamageStep`, `CardResolved.DamageSteps` 없음 (CS0246/CS1061).

- [ ] **Step 3: DamageStep을 만든다**

Create `Assets/Core/Events/DamageStep.cs`:

```csharp
namespace FateWeaver.Core.Events
{
    /// <summary>피해가 한 단계에서 어떻게 바뀌었는지. HolderId는 그 단계를 만든 상태의 보유자
    /// (약화는 공격자, 취약·방어는 대상), StatusId는 그 상태의 키다. 값을 바꾸지 않은 상태는
    /// 단계를 남기지 않는다.</summary>
    public sealed record DamageStep(string HolderId, string StatusId, int Before, int After);
}
```

- [ ] **Step 4: CardResolved에 내역을 붙인다**

`Assets/Core/Events/ResolutionEvent.cs`의 `CardResolved` 본문(compat 생성자 위)에 추가한다.
init-only 기본값이므로 기존 생성자 호출은 전부 그대로 컴파일된다.

```csharp
        /// <summary>이 카드가 준 피해가 상태로 어떻게 바뀌었는지의 단계별 내역. 상태가 관여하지
        /// 않았으면 빈 목록이다. 이벤트를 새로 끼워 넣지 않으려고 페이로드로 싣는다.</summary>
        public System.Collections.Generic.IReadOnlyList<DamageStep> DamageSteps { get; init; }
            = System.Array.Empty<DamageStep>();
```

- [ ] **Step 5: fold가 단계를 기록하게 한다**

`Assets/Core/Status/StatusDamageFold.cs`의 세 공개 메서드에 `string holderId`와
`List<Events.DamageStep> trace`를 추가한다. `trace`가 null이면 기록하지 않는다(기존 호출 호환).
값이 바뀐 경우에만 기록한다 — 관여하지 않은 상태로 로그를 채우지 않기 위해서다.

`Incoming`/`Outgoing`/`GainedMagnitude` 안의 각 루프에서 `after != damage` 분기에 한 줄 더한다.

```csharp
                if (after != damage)
                {
                    trace?.Add(new Events.DamageStep(holderId, status.Key.Id, damage, after));
                    bag.Consume(status);
                }
```

`Incoming`은 두 층을 접으므로 `FoldLayer`에도 같은 두 인자를 넘긴다.

- [ ] **Step 6: EffectContext가 단계를 모으게 한다**

`Assets/Core/Effects/IEffectHandler.cs`의 `EffectContext`에 추가한다.

```csharp
        /// <summary>이 효과의 피해가 상태로 바뀐 단계들. TurnResolver가 카드 단위로 모아
        /// CardResolved에 싣는다.</summary>
        public List<Events.DamageStep> DamageSteps = new List<Events.DamageStep>();
```

`Assets/Core/Effects/DamageHandler.cs`의 두 접기 헬퍼가 이것을 넘기게 한다. 대상 보유자 id는
호출 지점마다 다르므로(적/파티원) `FoldIncoming` 시그니처에 `string holderId`를 더하고 각
호출 지점에서 `target.Id` 또는 `each.Id`를 넘긴다.

```csharp
        private static int FoldIncoming(EffectContext ctx, StatusBag bag, string holderId, int damage)
            => StatusDamageFold.Incoming(
                bag, ctx.StatusRegistry, ctx.State.StatusRules, damage, holderId, ctx.DamageSteps);

        private static int FoldOutgoing(EffectContext ctx, int damage)
            => StatusDamageFold.Outgoing(
                ctx.ActorStatuses, ctx.StatusRegistry, ctx.State.StatusRules, damage,
                ctx.Card.OwnerId, ctx.DamageSteps);
```

- [ ] **Step 7: TurnResolver가 내역을 CardResolved에 싣는다**

`ResolveCard`에서 카드 단위 누적 목록을 만들고 효과마다 `ctx.DamageSteps`를 붙인 뒤
`CardResolved` 생성에 `DamageSteps = ...`를 더한다. `totalDamage` 누적 옆에 한 줄을 더하면 된다.

```csharp
                totalDamage += ctx.DamageDealt;
                damageSteps.AddRange(ctx.DamageSteps);
```

```csharp
                events.Add(new CardResolved(
                    card.InstanceId, card.OwnerId, card.Def.Id, card.Def.Side, totalDamage, targetId, strongestTier)
                {
                    DamageSteps = damageSteps
                });
```

- [ ] **Step 8: 테스트가 통과하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 412 tests (409 + 신규 3). 인덱스 의존 테스트는 이벤트 개수가 그대로라
무수정으로 통과해야 한다 — 하나라도 깨지면 페이로드가 아니라 이벤트를 늘린 것이니 되돌린다.

- [ ] **Step 9: 커밋**

```bash
git add Assets/Core/Events Assets/Core/Status/StatusDamageFold.cs Assets/Core/Effects Assets/Core/Combat/TurnResolver.cs Assets/Core/Tests/EditMode/CombatLogTests.cs
git commit -m "feat: record per-status damage steps on CardResolved"
```

---

### Task 2: 상태 부여·만료 이벤트를 추가한다

타임라인에 상태가 걸리고 사라지는 사건이 없다. 페이로드로 실을 기존 이벤트가 없으므로 새 이벤트를
추가하고, 인덱스가 밀리는 테스트를 함께 고친다.

**Files:**
- Modify: `Assets/Core/Events/ResolutionEvent.cs`, `Assets/Core/Status/StatusBag.cs`,
  `Assets/Core/Effects/ApplyStatusHandler.cs`, `Assets/Core/Combat/TurnResolver.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`, 인덱스 의존 테스트 5개 파일

**Interfaces:**
- Produces:
  - `StatusApplied(string HolderId, string StatusId, int Count, int Magnitude, bool Stacked)`
  - `StatusExpired(string HolderId, string StatusId)`
  - `StatusBag.EndOfTurn()` → `IReadOnlyList<StatusKey>` (만료된 키)

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`CombatLogTests.cs`에 추가한다.

```csharp
        [Test]
        public void Applying_a_status_emits_status_applied_with_the_folded_magnitude()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(new Enemy("goblin", 30));
            var def = new CardDefinition("guard", "guard", Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Turns(2), StatusApplyTarget.Self, 5)
                });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);
            var applied = events.OfType<StatusApplied>().Single(e => e.StatusId == "block");

            Assert.AreEqual(CombatState.SoloPlayerId, applied.HolderId);
            Assert.AreEqual(3, applied.Magnitude); // 손상으로 floor(5 x 0.75)
        }

        [Test]
        public void A_status_that_runs_out_of_turns_emits_status_expired()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(1)); // 이번 턴 끝에 만료
            state.Enemies.Add(new Enemy("goblin", 30));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.IsTrue(events.OfType<StatusExpired>()
                .Any(e => e.HolderId == CombatState.SoloPlayerId && e.StatusId == "weak"));
        }
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~CombatLogTests"`

Expected: 컴파일 실패 — `StatusApplied`, `StatusExpired` 없음.

- [ ] **Step 3: 이벤트를 추가한다**

`Assets/Core/Events/ResolutionEvent.cs`의 `StatusTicked` 위에 추가한다.

```csharp
    /// <summary>상태가 보유자에게 부여되었다. Magnitude는 획득 훅(손상 등)을 거친 최종 값이고,
    /// Stacked는 기존 인스턴스에 합산되었는지다(방어·독).</summary>
    public sealed record StatusApplied(
        string HolderId, string StatusId, int Count, int Magnitude, bool Stacked) : ResolutionEvent;

    /// <summary>상태의 수명이 다해 보유자에게서 사라졌다 (ThisTurn 소멸 또는 Turns 소진).</summary>
    public sealed record StatusExpired(string HolderId, string StatusId) : ResolutionEvent;
```

- [ ] **Step 4: StatusBag이 만료된 키를 알려주게 한다**

`Assets/Core/Status/StatusBag.cs`의 `EndOfTurn`이 제거한 키를 모아 반환한다. 반환값을 무시하는
기존 호출은 그대로 컴파일된다.

```csharp
        /// <summary>턴 종료 정리. 사라진 상태의 키를 돌려주어 호출자가 만료 이벤트를 낼 수 있게 한다.</summary>
        public IReadOnlyList<StatusKey> EndOfTurn()
        {
            var expired = new List<StatusKey>();
            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                var status = _statuses[i];
                if (status.Kind == StatusLifetimeKind.ThisTurn)
                {
                    _statuses.RemoveAt(i);
                    expired.Add(status.Key);
                }
                else if (status.Kind == StatusLifetimeKind.Turns)
                {
                    status.Count--;
                    if (status.Count <= 0)
                    {
                        _statuses.RemoveAt(i);
                        expired.Add(status.Key);
                    }
                }
            }

            return expired;
        }
```

- [ ] **Step 5: 부여·만료 이벤트를 발행한다**

`Assets/Core/Effects/ApplyStatusHandler.cs`의 `ApplyTo`에서 적용 후 이벤트를 `ctx.ExtraEvents`에
넣는다. 보유자 id가 필요하므로 `ApplyTo`에 `string holderId`를 더하고 각 호출 지점에서 넘긴다
(`member.Id` / `enemy.Id`).

```csharp
            var stacked = /* Stack 경로를 탔는지 */;
            var instance = bag.Get(payload.Key);
            ctx.ExtraEvents.Add(new Events.StatusApplied(
                holderId, payload.Key.Id, instance.Count, instance.Magnitude, stacked));
```

`Assets/Core/Combat/TurnResolver.cs`의 `EndOfTurnMaintenance`에서 각 보유자의 `EndOfTurn()`
반환값을 받아 `StatusExpired`를 발행한다.

```csharp
            foreach (var member in state.Party)
            {
                foreach (var key in member.Statuses.EndOfTurn())
                {
                    events.Add(new StatusExpired(member.Id, key.Id));
                }
            }
```

적 쪽 `EndOfTurn` 호출 지점에도 같은 형태를 적용한다.

- [ ] **Step 6: 밀린 인덱스 의존 테스트를 고친다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

`events[N]`으로 단언하는 5개 파일에서 새 이벤트 때문에 밀린 곳을 고친다. **인덱스를 다시 세지 말고
`OfType<CardResolved>().Single()` 형태로 바꾼다** — 이벤트가 또 늘어도 안 깨지고, 무엇을 검증하는지가
분명해진다. 예:

```csharp
            // 전
            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt);
            // 후
            Assert.AreEqual(6, events.OfType<CardResolved>().Single().DamageDealt);
```

카드가 여럿인 테스트는 `.Single(e => e.CardId == "strike1")`로 특정한다.

- [ ] **Step 7: 전체 테스트를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 414 tests (412 + 신규 2).

- [ ] **Step 8: 커밋**

```bash
git add Assets/Core
git commit -m "feat: emit status applied and expired events"
```

---

### Task 3: 타임라인 한국어 포매터

타임라인을 사람이 읽는 여러 줄로 바꾼다. 코어(Simulation)에 두어 헤드리스로 검증하고, 상태·카드
이름은 설명 레지스트리에서 가져온다(규칙 10).

**Files:**
- Create: `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs`
- Test: `Assets/Core/Tests/EditMode/CombatLogTests.cs`

**Interfaces:**
- Consumes: `CardResolved.DamageSteps` (Task 1), `StatusApplied`·`StatusExpired` (Task 2)
- Produces: `TimelineTextFormatter.Format(IReadOnlyList<ResolutionEvent>, KoreanDescriptionCatalog) -> string`

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`CombatLogTests.cs`에 추가한다. 정확한 문장 전체를 단언하면 문구를 다듬을 때마다 깨지므로,
**빠지면 안 되는 정보가 들어있는지**를 단언한다.

```csharp
        [Test]
        public void Formatter_spells_out_each_damage_step_and_the_deaths_door_save()
        {
            var timeline = new ResolutionEvent[]
            {
                new TurnStarted(0),
                new CardResolved(1, "goblin", "jab", Side.Enemy, 4, "member_a")
                {
                    DamageSteps = new[]
                    {
                        new DamageStep("member_a", "vulnerable", 4, 6),
                        new DamageStep("member_a", "block", 6, 4)
                    }
                },
                new DeathsDoorSurvived("member_a"),
                new TurnEnded(0, Outcome.Ongoing)
            };

            var text = TimelineTextFormatter.Format(timeline, Korean);

            StringAssert.Contains("취약", text);
            StringAssert.Contains("4", text);
            StringAssert.Contains("6", text);
            StringAssert.Contains("방어", text);
            StringAssert.Contains("치명", text);   // 왜 살아남았는지가 반드시 보여야 한다
            StringAssert.Contains("member_a", text);
        }

        [Test]
        public void Formatter_handles_an_empty_timeline_without_throwing()
        {
            Assert.AreEqual(
                string.Empty,
                TimelineTextFormatter.Format(
                    System.Array.Empty<ResolutionEvent>(), Korean));
        }
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~Formatter"`

Expected: 컴파일 실패 — `TimelineTextFormatter` 없음.

- [ ] **Step 3: 포매터를 만든다**

Create `Assets/Core/Simulation/Descriptions/TimelineTextFormatter.cs`. 모든 이벤트 종류를 다루고,
알 수 없는 이벤트는 조용히 건너뛰지 말고 타입 이름이라도 남긴다 — 침묵 실패를 만들지 않기 위해서다.

```csharp
using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Events;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>타임라인을 사람이 읽는 여러 줄로 바꾼다. 코어의 출력은 타임라인뿐이므로(규칙 11)
    /// 로그의 원천도 이것 하나다. 상태·카드 이름은 설명 레지스트리에서 가져온다.</summary>
    public static class TimelineTextFormatter
    {
        public static string Format(
            IReadOnlyList<ResolutionEvent> timeline,
            KoreanDescriptionCatalog catalog)
        {
            if (timeline == null || timeline.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var evt in timeline)
            {
                AppendEvent(sb, evt, catalog);
            }

            return sb.ToString();
        }

        private static void AppendEvent(
            StringBuilder sb, ResolutionEvent evt, KoreanDescriptionCatalog catalog)
        {
            switch (evt)
            {
                case TurnStarted e:
                    sb.Append("== ").Append(e.TurnIndex + 1).AppendLine("턴 시작 ==");
                    break;
                case CardResolved e:
                    sb.Append("  ").Append(e.CardId).Append(" 해결");
                    if (e.TargetId != null) sb.Append(" → ").Append(e.TargetId);
                    sb.Append(" (피해 ").Append(e.DamageDealt).AppendLine(")");
                    foreach (var step in e.DamageSteps)
                    {
                        sb.Append("      ").Append(step.HolderId).Append('의')
                          .Append(StatusName(catalog, step.StatusId))
                          .Append(": ").Append(step.Before).Append(" → ").AppendLine(step.After.ToString());
                    }

                    break;
                case CardCancelled e:
                    sb.Append("  ").Append(e.CardId).Append(" 취소 (").Append(e.Reason).AppendLine(")");
                    break;
                case StatusApplied e:
                    sb.Append("  ").Append(e.HolderId).Append("에게 ")
                      .Append(StatusName(catalog, e.StatusId))
                      .Append(" 부여 (count=").Append(e.Count)
                      .Append(", 수치=").Append(e.Magnitude)
                      .AppendLine(e.Stacked ? ", 합산)" : ")");
                    break;
                case StatusExpired e:
                    sb.Append("  ").Append(e.HolderId).Append('의')
                      .Append(StatusName(catalog, e.StatusId)).AppendLine(" 만료");
                    break;
                case StatusTicked e:
                    sb.Append("  ").Append(e.HolderId).Append('의')
                      .Append(StatusName(catalog, e.StatusId))
                      .Append(" 발동 (피해 ").Append(e.Damage)
                      .Append(", 수치 ").Append(e.Magnitude).AppendLine(")");
                    break;
                case StatusTransferred e:
                    sb.Append("  ").Append(StatusName(catalog, e.StatusId))
                      .Append(' ').Append(e.Magnitude).Append(" 이전: ")
                      .Append(e.FromHolderId).Append(" → ").AppendLine(e.ToHolderId);
                    break;
                case DeathsDoorSurvived e:
                    sb.Append("  ").Append(e.MemberId).AppendLine(" 치명 버팀 발동 (HP 1로 유지)");
                    break;
                case PartyMemberDied e:
                    sb.Append("  ").Append(e.MemberId).AppendLine(" 사망");
                    break;
                case EnemyDied e:
                    sb.Append("  ").Append(e.EnemyId).AppendLine(" 처치");
                    break;
                case TurnEnded e:
                    sb.Append("== ").Append(e.TurnIndex + 1).Append("턴 종료 (")
                      .Append(e.Outcome).AppendLine(") ==");
                    break;
                default:
                    sb.Append("  [미처리 이벤트] ").AppendLine(evt.GetType().Name);
                    break;
            }
        }

        private static string StatusName(KoreanDescriptionCatalog catalog, string statusId)
            => catalog.Statuses.Resolve(new Core.Status.StatusKey(statusId));
    }
}
```

- [ ] **Step 4: 테스트가 통과하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 416 tests (414 + 신규 2).

- [ ] **Step 5: 커밋**

```bash
git add Assets/Core
git commit -m "feat: add korean timeline text formatter"
```

---

### Task 4: Unity 화면에 배선하고 임시 계측을 걷어낸다

**Files:**
- Modify: `Assets/Unity/BattleScreenController.cs`, `Assets/Unity/DeckPlaytestController.cs`

- [ ] **Step 1: 배틀 화면이 포매터를 쓰게 한다**

`BattleScreenController`의 임시 `DebugDumpTimeline`(있다면)을 지우고, `_session.ResolveTurn()`
직후에 포매터를 호출한다. 우클릭 디버그 훅(`DebugApply*`)도 함께 지운다 — 상태를 거는 수단은
콘텐츠 카드로 저작하는 것이 정석이고, 임시 훅을 남기면 저작 경로와 이중 원본이 된다.

```csharp
                _session.ResolveTurn();
                Debug.Log(TimelineTextFormatter.Format(
                    _session.LastTimeline, _korean));
```

- [ ] **Step 2: 플레이테스트 화면의 자체 렌더링을 교체한다**

`DeckPlaytestController.RefreshTimeline`은 `CardResolved`와 `TurnEnded`만 다루고 나머지를 조용히
버린다. 본문을 포매터 호출로 교체해 두 화면이 같은 문장을 쓰게 한다.

```csharp
        private void RefreshTimeline()
            => _timelineText.text = TimelineTextFormatter.Format(
                _session.LastTimeline, _korean);
```

- [ ] **Step 3: Unity에서 컴파일을 확인한다**

워크트리를 `-projectPath`로 열어 Console에 컴파일 에러가 없는지 본다(규칙 17의 예외 범위).
씬·Prefab·ScriptableObject는 저작하지 않는다.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Unity
git commit -m "feat: dump the combat timeline to the console"
```

---

### Task 5: 문서 갱신

- [ ] **Step 1: 색인과 백로그를 갱신한다**

`docs/superpowers/README.md`의 활성 계획에서 이 문서의 상태를 갱신하고, 백로그 §13에 "배틀 화면이
타임라인을 표시하지 않는다" 항목이 있으면 해소로 표시한다.

- [ ] **Step 2: 전체 테스트와 워킹 트리를 확인하고 커밋한다**

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
git status
```

## 범위 밖

| 항목 | 이유 |
|---|---|
| 인게임 전투 로그 패널 | 이번 결정은 개발용 Console만. 포매터가 코어에 있으므로 나중에 그대로 붙인다 |
| 파일 출력 | 필요해지면 포매터 결과를 쓰는 얇은 층으로 추가한다 |
| 운명력 변화 이벤트 | 현재 타임라인에 없다. 필요해지면 별도 항목으로 다룬다 |
| 개입(조작) 카드 적용 이벤트 | 실행 순서 변경·잠금이 타임라인에 남지 않는다. 별도 항목 |
