# 상태 규칙 파라미터화와 3종 디버프 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

- 작성일: 2026-07-30
- 상태: `active` — Task 1~4 구현 완료(409 tests 통과), Unity 표시 확인과 count 단일화가 남았다
- 브랜치: `claude/brave-nash-3974c5`

**Goal:** 약화(주는 피해 −25%), 취약(받는 피해 +50%), 손상(방어도 획득 −25%)을 추가하고, 세 배율을
하드코딩이 아닌 런타임 조절 가능한 상태 규칙 데이터로 둔다.

**Architecture:** 피해 계산을 `배율 층 → 버림 → 흡수 층` 두 단계로 나눈다. 방어는 흡수 층으로
옮겨 걸린 순서와 무관하게 마지막에 적용된다. 각 상태의 배율은 `StatusRule`에 담고 `CombatState`가
보유하므로 전투 중 변경이 결정론과 세이브 경계를 지킨다. 새 상태는 핸들러 클래스 1개 + 키 등록으로
추가한다(레지스트리 규칙).

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1 제약), NUnit, `FateWeaver.Core`(UnityEngine 미참조)

## Global Constraints

- 헤드리스 테스트 명령: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`
- 착수 시점 기준선: 395 tests, 0 failed
- `FateWeaver.Core`에서 `UnityEngine`을 참조하지 않는다 (AGENTS.md 규칙 6)
- 모든 무작위는 `CombatState`의 시드 RNG를 경유한다. `System.Random` 즉석 생성·`DateTime`·`Guid.NewGuid()` 금지 (규칙 7)
- 튜닝 수치를 계산식에 박지 않는다. 명명된 프로퍼티나 데이터로 둔다 (규칙 8)
- 새 상태 = 핸들러 클래스 1개 + 키 등록. 중앙 switch를 키우지 않는다 (규칙 9)
- 카드·상태 설명을 하드코딩하지 않는다. 설명 레지스트리에 등록한다 (규칙 10)
- C# 9 한계: `record struct` 사용 금지, 기본 인터페이스 구현 사용 금지 (추상 기반 클래스로 대체)
- 소수점은 전부 버림. 정수 산술 `(value * percent) / 100`으로 표현한다
- 피해 최소 1을 보장하지 않는다. `floor(1 × 0.75) = 0`은 의도된 결과다

## 확정된 규칙

| 상태 | 키 | count의 의미 | 효과 |
|---|---|---|---|
| 취약 | `vulnerable` | 남은 턴 수 | 받는 피해 배율 150% |
| 약화 | `weak` | 남은 턴 수 | 주는 피해 배율 75% |
| 손상 | `damaged` | 남은 턴 수 | 방어도 획득 배율 75% |
| 방어 | `block` | 흡수량 그 자체 | count만큼 흡수 |

- count가 쌓이는 것은 **지속 턴이 늘어나는 것**이다. 취약 2를 두 번 걸면 취약 4 = 4턴이며
  효과가 +200%가 되는 것이 아니다.
- 피해 계산 순서: `기본 피해 → 약화(공격자) → 취약(대상) → 버림 → 방어 흡수 → HP 차감`
- 배율은 각 단계에서 정수로 버림한다(단계별 버림). 배율 누적 후 1회 버림으로 바꾸는 것은 같은
  보유자에게 곱셈 상태가 둘 이상 붙을 때 검토한다.

## 이 계획의 범위 밖 (후속 계획)

**count 단일화 (`StatusLifetime` → 상태별 감쇠 규칙)를 이 계획에서 제외한다.**

원래 제안 순서에서는 3종 추가보다 앞이었으나, 다음 이유로 뒤로 옮긴다.

- 3종은 모두 count = 남은 턴이므로 현재 `StatusLifetime.Turns(N)`으로 **정확히 표현된다.** 먼저
  단일화하지 않아도 rework가 발생하지 않는다.
- `StatusLifetime` 제거는 저작 콘텐츠(`Content/Cards/*.json`과 아직 C#인 적 덱
  `GoblinDeck`·`WardenDeck`), 저작 스펙(`ApplyStatusSpec`), 설명 문법(`LifetimeSuffix`)에 걸친다.
  (2026-08-05 갱신: 원문이 함께 적었던 `StarterPoolSpecs`·`StarterDeckSpecs`·
  `PartyPrototypeDeckSpecs`·`GeneratedCards.cs`는 계획 3b·3d가 제거했다.) 3종과 한 계획에
  묶으면 회귀 원인을 가릴 수 있다.
- 3종이 들어간 뒤 단일화하면 마이그레이션 검증 대상이 늘어 더 촘촘해진다.

**따라서 이 계획을 끝내도 "방어를 영구로, 독을 이번 턴만으로 바꾼다"는 요구는 아직 충족되지
않는다.** 그 요구는 후속 계획에서 다룬다. 이 계획이 충족하는 것은 "배율(강도)을 런타임에 바꾼다"
쪽이다.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Core/Status/StatusDamageLayer.cs` (신규) | 배율·흡수 층 구분 |
| `Assets/Core/Status/StatusDamageFold.cs` (신규) | 층 순서대로 피해를 접는 공용 로직 |
| `Assets/Core/Status/StatusRule.cs` (신규) | 상태 하나의 배율 파라미터 |
| `Assets/Core/Status/StatusRuleSet.cs` (신규) | 상태별 규칙 보관·조회·변경 |
| `Assets/Core/Status/StatusRuleCatalog.cs` (신규) | 기본값 카탈로그 |
| `Assets/Core/Status/WeakBehavior.cs` (신규) | 약화 |
| `Assets/Core/Status/DamagedBehavior.cs` (신규) | 손상 |
| `Assets/Core/Combat/CardActor.cs` (신규) | 카드 소유자(행위자)의 StatusBag 해결 |
| `Assets/Core/Status/IStatusBehavior.cs` | 훅 추가: 층 선언, 주는 피해, 획득 수치 |
| `Assets/Core/Status/BlockBehavior.cs` | 흡수 층으로 선언 |
| `Assets/Core/Status/VulnerableBehavior.cs` | 하드코딩 배율 → 규칙 조회 |
| `Assets/Core/Status/StatusKey.cs` | `Weak`, `Damaged` 키 |
| `Assets/Core/Status/StatusExecutionOrder.cs` | `StatusContext.Rules` 전달 |
| `Assets/Core/Combat/CombatState.cs` | `StatusRules` 보유 |
| `Assets/Core/Effects/IEffectHandler.cs` | `EffectContext.ActorStatuses` |
| `Assets/Core/Effects/DamageHandler.cs` | 주는 피해 접기 + 공용 fold 사용 |
| `Assets/Core/Effects/ApplyStatusHandler.cs` | 획득 수치 접기 |
| `Assets/Core/Combat/TurnResolver.cs` | `ActorStatuses`·`Rules` 배선 |
| `Assets/Core/Simulation/CombatRegistries.cs` | 새 상태 등록 |
| `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs` | 새 상태 이름 |
| `Assets/Core/Simulation/DeckCombatSession.cs` | `ExecutionOrderFor` 인자 추가 |

테스트는 기존 파일에 추가한다: `Assets/Core/Tests/EditMode/StatusTests.cs`,
`SlowHasteStatusTests.cs`, `AuthoringValidationTests.cs`. 3종 전용 테스트는
`Assets/Core/Tests/EditMode/DebuffStatusTests.cs`(신규)에 모은다.

---

### Task 1: 방어를 흡수 층으로 분리

현재 `DamageHandler.FoldIncoming`은 대상의 상태를 `bag.All` 삽입 순서대로 한 루프에서 접는다.
방어가 취약보다 먼저 걸려 있으면 방어가 먼저 흡수하고 남은 값에 취약이 곱해져, 확정된 규칙
(취약 먼저 → 방어 마지막)과 어긋난다. 이 태스크는 그 버그만 고친다. 새 상태는 추가하지 않는다.

**Files:**
- Create: `Assets/Core/Status/StatusDamageLayer.cs`
- Create: `Assets/Core/Status/StatusDamageFold.cs`
- Modify: `Assets/Core/Status/IStatusBehavior.cs`
- Modify: `Assets/Core/Status/BlockBehavior.cs`
- Modify: `Assets/Core/Effects/DamageHandler.cs`
- Test: `Assets/Core/Tests/EditMode/StatusTests.cs`

**Interfaces:**
- Produces: `enum StatusDamageLayer { Multiplier, Absorb }`;
  `IStatusBehavior.DamageLayer` (기본 `Multiplier`);
  `StatusDamageFold.Incoming(StatusBag bag, StatusRegistry registry, int damage, StatusContext prototype)`
  — 이 태스크에서는 `prototype` 없이 `Incoming(StatusBag, StatusRegistry, int)` 형태로 만들고
  Task 2에서 규칙 전달을 위해 확장한다.

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`Assets/Core/Tests/EditMode/StatusTests.cs`의 `Statuses()` 헬퍼에 방어를 등록한다.

```csharp
        private static StatusRegistry Statuses()
        {
            var r = new StatusRegistry();
            r.Register(new StunBehavior());
            r.Register(new VulnerableBehavior());
            r.Register(new RewardSuppressionBehavior());
            r.Register(new BlockBehavior());
            return r;
        }
```

같은 파일 마지막 테스트 뒤에 두 테스트를 추가한다.

```csharp
        [Test]
        public void Vulnerable_multiplies_before_block_absorbs_when_block_applied_first()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, 5);   // 방어가 먼저
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 10));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // 10 x 1.5 = 15, 그 다음 방어 5가 흡수 -> 10
            Assert.AreEqual(10, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(20, enemy.Hp);
            Assert.IsFalse(enemy.Statuses.Has(StatusKeys.Block)); // ThisTurn 방어는 턴 끝에 사라진다
        }

        [Test]
        public void Vulnerable_and_block_result_is_independent_of_apply_order()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2)); // 취약이 먼저
            enemy.Statuses.Add(StatusKeys.Block, StatusLifetime.ThisTurn, 5);
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 10));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(10, ((CardResolved)events[1]).DamageDealt);
            Assert.AreEqual(20, enemy.Hp);
        }
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~StatusTests"`

Expected: `Vulnerable_multiplies_before_block_absorbs_when_block_applied_first`가
`Expected: 10  But was: 7`로 FAIL. 두 번째 테스트는 PASS(현재 순서가 우연히 맞는 경우).

- [ ] **Step 3: 층 열거형을 만든다**

Create `Assets/Core/Status/StatusDamageLayer.cs`:

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>피해 계산에서 상태가 접히는 단계. 배율 층이 모두 접히고 버림된 뒤 흡수 층이
    /// 적용된다 (취약을 먼저 곱하고 방어가 추가 체력처럼 마지막에 흡수한다).
    /// 뺄셈이거나 자기 수치를 소모하는 상태는 배율 층에 넣지 않는다.</summary>
    public enum StatusDamageLayer
    {
        Multiplier,
        Absorb
    }
}
```

- [ ] **Step 4: 인터페이스에 층 선언을 추가한다**

`Assets/Core/Status/IStatusBehavior.cs`의 `IStatusBehavior`에 프로퍼티를 추가한다
(`StacksMagnitude` 선언 바로 아래).

```csharp
        /// <summary>피해 계산에서 이 상태가 접히는 단계 (방어만 흡수 층).</summary>
        StatusDamageLayer DamageLayer { get; }
```

같은 파일 `StatusBehavior` 추상 클래스에 기본값을 추가한다 (`StacksMagnitude` 기본값 아래).

```csharp
        public virtual StatusDamageLayer DamageLayer => StatusDamageLayer.Multiplier;
```

- [ ] **Step 5: 방어를 흡수 층으로 선언한다**

`Assets/Core/Status/BlockBehavior.cs`의 `StacksMagnitude` 아래에 추가한다.

```csharp
        public override StatusDamageLayer DamageLayer => StatusDamageLayer.Absorb;
```

- [ ] **Step 6: 공용 fold 모듈을 만든다**

Create `Assets/Core/Status/StatusDamageFold.cs`:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>보유자의 엔티티 스코프 상태를 층 순서대로 접어 받는 피해를 계산한다.
    /// 배율 층을 모두 접은 뒤(각 단계에서 정수로 버림) 흡수 층을 적용한다. 값을 실제로 바꾼
    /// UntilConsumed 상태는 그 자리에서 수명을 1 소비한다.</summary>
    public static class StatusDamageFold
    {
        public static int Incoming(StatusBag bag, StatusRegistry registry, int damage)
        {
            if (registry == null || bag == null)
            {
                return damage;
            }

            damage = FoldLayer(bag, registry, damage, StatusDamageLayer.Multiplier);
            damage = FoldLayer(bag, registry, damage, StatusDamageLayer.Absorb);
            return damage;
        }

        private static int FoldLayer(
            StatusBag bag,
            StatusRegistry registry,
            int damage,
            StatusDamageLayer layer)
        {
            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior)
                    || behavior.DamageLayer != layer)
                {
                    continue;
                }

                var after = behavior.ModifyIncomingDamage(damage, new StatusContext { Instance = status });
                if (after != damage)
                {
                    bag.Consume(status);
                }

                damage = after;
            }

            return damage;
        }
    }
}
```

- [ ] **Step 7: DamageHandler가 공용 fold를 쓰게 한다**

`Assets/Core/Effects/DamageHandler.cs`에서 `FoldIncoming` 메서드 전체(주석 포함, 파일 끝의
`private static int FoldIncoming(...)` 블록)를 다음으로 교체한다.

```csharp
        /// <summary>Folds the target's entity-scoped statuses into incoming damage
        /// (multiplier layer, then absorb layer). See StatusDamageFold.</summary>
        private static int FoldIncoming(EffectContext ctx, StatusBag bag, int damage)
            => StatusDamageFold.Incoming(bag, ctx.StatusRegistry, damage);
```

`using System.Collections.Generic;`는 `AllLivingParty`가 계속 쓰므로 남긴다.

- [ ] **Step 8: 테스트가 통과하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 397 tests (기준선 395 + 신규 2).

- [ ] **Step 9: 커밋**

```bash
git add Assets/Core/Status/StatusDamageLayer.cs Assets/Core/Status/StatusDamageFold.cs Assets/Core/Status/IStatusBehavior.cs Assets/Core/Status/BlockBehavior.cs Assets/Core/Effects/DamageHandler.cs Assets/Core/Tests/EditMode/StatusTests.cs
git commit -m "fix: apply block after damage multipliers regardless of apply order"
```

Unity `.meta` 파일이 생성되면 함께 스테이징한다.

---

### Task 2: 상태 배율을 런타임 조절 가능한 규칙으로 옮긴다

취약의 `(damage * 3) / 2`는 계산식에 박힌 배율이라 조절 지점이 없다(규칙 8 위반, backlog §12.2).
배율을 `StatusRule`로 옮기고 `CombatState`가 보유하게 해서 전투 중 변경이 시드·스냅샷 경계 안에
머물게 한다. 이 태스크는 새 상태를 추가하지 않으며 기존 수치(150%)를 그대로 재현한다.

**Files:**
- Create: `Assets/Core/Status/StatusRule.cs`
- Create: `Assets/Core/Status/StatusRuleSet.cs`
- Create: `Assets/Core/Status/StatusRuleCatalog.cs`
- Modify: `Assets/Core/Status/IStatusBehavior.cs`
- Modify: `Assets/Core/Status/StatusDamageFold.cs`
- Modify: `Assets/Core/Status/StatusExecutionOrder.cs`
- Modify: `Assets/Core/Status/VulnerableBehavior.cs`
- Modify: `Assets/Core/Combat/CombatState.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs`
- Modify: `Assets/Core/Effects/DamageHandler.cs`
- Modify: `Assets/Core/Simulation/DeckCombatSession.cs`
- Test: `Assets/Core/Tests/EditMode/StatusTests.cs`, `Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs`

**Interfaces:**
- Consumes: `StatusDamageFold.Incoming` (Task 1)
- Produces:
  - `StatusRule { int MultiplierPercent { get; set; } }`
  - `StatusRuleSet.For(StatusKey) -> StatusRule` (없는 키는 100% 규칙을 반환), `Set(StatusKey, StatusRule)`
  - `StatusRuleCatalog.Default() -> StatusRuleSet`
  - `StatusContext.Rules` (StatusRuleSet)
  - `CombatState.StatusRules` (StatusRuleSet, 기본값 `StatusRuleCatalog.Default()`)
  - `StatusDamageFold.Incoming(StatusBag, StatusRegistry, StatusRuleSet, int)`
  - `StatusExecutionOrder.ExecutionOrderFor(int, StatusBag, StatusRegistry, StatusRuleSet)`

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`Assets/Core/Tests/EditMode/StatusTests.cs` 마지막에 추가한다.

```csharp
        [Test]
        public void Vulnerable_multiplier_comes_from_the_combat_status_rules()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            state.StatusRules.Set(StatusKeys.Vulnerable, new StatusRule { MultiplierPercent = 200 });
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 4));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(8, ((CardResolved)events[1]).DamageDealt); // 4 x 2.00
        }

        [Test]
        public void Vulnerable_multiplier_defaults_to_one_hundred_fifty_percent()
        {
            var rules = StatusRuleCatalog.Default();
            Assert.AreEqual(150, rules.For(StatusKeys.Vulnerable).MultiplierPercent);
        }

        [Test]
        public void Unregistered_status_rule_is_a_neutral_multiplier()
        {
            var rules = StatusRuleCatalog.Default();
            Assert.AreEqual(100, rules.For(new StatusKey("no_such_status")).MultiplierPercent);
        }

        [Test]
        public void Vulnerable_multiplier_floors_odd_damage()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(Card("strike", Side.Player, 1, 5));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(7, ((CardResolved)events[1]).DamageDealt); // floor(5 x 1.5) = 7
        }
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~StatusTests"`

Expected: 컴파일 실패 — `StatusRule`, `StatusRuleCatalog`, `CombatState.StatusRules`가 없다는
CS0246/CS1061 에러.

- [ ] **Step 3: 규칙 타입을 만든다**

Create `Assets/Core/Status/StatusRule.cs`:

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>상태 하나의 배율 파라미터. count(지속·수치)와 독립이며, 런 중 유물 같은 효과로
    /// 바뀔 수 있으므로 계산식에 박지 않고 여기에 둔다. 정수 퍼센트로 표현해 결정론과 버림을
    /// 함께 지킨다 (적용은 (value * MultiplierPercent) / 100).</summary>
    public sealed class StatusRule
    {
        /// <summary>100 = 변화 없음. 취약 150(받는 피해 +50%), 약화·손상 75(-25%).</summary>
        public int MultiplierPercent { get; set; } = NeutralPercent;

        public const int NeutralPercent = 100;

        /// <summary>배율을 적용하고 버린다.</summary>
        public int Apply(int value) => (value * MultiplierPercent) / NeutralPercent;
    }
}
```

Create `Assets/Core/Status/StatusRuleSet.cs`:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Core.Status
{
    /// <summary>상태별 규칙 보관소. 전투 단위로 CombatState가 보유하므로 전투 중 변경이 시드·
    /// 스냅샷 경계를 넘지 않는다 (규칙 7). 등록되지 않은 키는 중립 배율을 반환한다.</summary>
    public sealed class StatusRuleSet
    {
        private readonly Dictionary<StatusKey, StatusRule> _rules = new();
        private static readonly StatusRule Neutral = new StatusRule();

        public void Set(StatusKey key, StatusRule rule) => _rules[key] = rule;

        public StatusRule For(StatusKey key)
            => _rules.TryGetValue(key, out var rule) ? rule : Neutral;
    }
}
```

Create `Assets/Core/Status/StatusRuleCatalog.cs`:

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>상태 배율의 기본값. PartyTuning.Prototype과 같은 역할이며, 튜닝 수치가 계산식이
    /// 아니라 명명된 한 곳에 모이게 한다 (규칙 8). 저작 데이터에서 값을 주입하게 되면 이 카탈로그가
    /// 그 기본값 출처가 된다.</summary>
    public static class StatusRuleCatalog
    {
        public const int VulnerableIncomingPercent = 150;

        public static StatusRuleSet Default()
        {
            var rules = new StatusRuleSet();
            rules.Set(StatusKeys.Vulnerable, new StatusRule { MultiplierPercent = VulnerableIncomingPercent });
            return rules;
        }
    }
}
```

- [ ] **Step 4: StatusContext에 규칙을 실어준다**

`Assets/Core/Status/IStatusBehavior.cs`의 `StatusContext`를 수정한다.

```csharp
    /// <summary>Inputs a status behavior may read when a hook fires.</summary>
    public sealed class StatusContext
    {
        public StatusInstance Instance;

        /// <summary>이 전투의 상태 규칙 (배율). 훅에서 튜닝 수치를 읽는 유일한 경로.</summary>
        public StatusRuleSet Rules;
    }
```

- [ ] **Step 5: CombatState가 규칙을 보유하게 한다**

`Assets/Core/Combat/CombatState.cs`의 `RngSeed` 프로퍼티 아래에 추가하고, 파일 상단
`using` 목록에 `using FateWeaver.Core.Status;`를 더한다.

```csharp
        /// <summary>이 전투의 상태 배율. 전투 단위로 존재하므로 전투 중 변경이 런으로 새지 않는다.
        /// 런 지속 변경(유물 등)은 전투 시작 시 이 값을 시딩해 반영한다.</summary>
        public Status.StatusRuleSet StatusRules { get; set; } = Status.StatusRuleCatalog.Default();
```

`using`을 추가하지 않고 `Status.` 접두사를 쓰는 이유는 `CombatState`가 이미
`Combat` 네임스페이스에서 `Status` 하위 네임스페이스를 그렇게 참조하고 있기 때문이다
(`StatusBag`은 `PartyMember`/`Enemy`가 보유한다). 접두사 형태를 유지한다.

- [ ] **Step 6: fold가 규칙을 전달하게 한다**

`Assets/Core/Status/StatusDamageFold.cs`를 수정한다 — 두 메서드 시그니처에 `StatusRuleSet rules`를
넣고 `StatusContext` 생성 시 함께 넘긴다.

```csharp
        public static int Incoming(StatusBag bag, StatusRegistry registry, StatusRuleSet rules, int damage)
        {
            if (registry == null || bag == null)
            {
                return damage;
            }

            damage = FoldLayer(bag, registry, rules, damage, StatusDamageLayer.Multiplier);
            damage = FoldLayer(bag, registry, rules, damage, StatusDamageLayer.Absorb);
            return damage;
        }

        private static int FoldLayer(
            StatusBag bag,
            StatusRegistry registry,
            StatusRuleSet rules,
            int damage,
            StatusDamageLayer layer)
        {
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior)
                    || behavior.DamageLayer != layer)
                {
                    continue;
                }

                var after = behavior.ModifyIncomingDamage(
                    damage,
                    new StatusContext { Instance = status, Rules = rules });
                if (after != damage)
                {
                    bag.Consume(status);
                }

                damage = after;
            }

            return damage;
        }
```

- [ ] **Step 7: 취약이 규칙을 읽게 한다**

`Assets/Core/Status/VulnerableBehavior.cs` 전체를 교체한다.

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>취약: the holder takes more damage, by the multiplier in this combat's StatusRules
    /// (default 150%). Applies regardless of damage source (entity incoming hook) — more robust than a
    /// per-card "+50%". count is remaining turns, not intensity: stacking extends duration.</summary>
    public sealed class VulnerableBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Vulnerable;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyIncomingDamage(int damage, StatusContext ctx)
            => ctx.Rules.For(Key).Apply(damage);
    }
}
```

- [ ] **Step 8: 호출 지점에 규칙을 배선한다**

`Assets/Core/Effects/DamageHandler.cs`의 `FoldIncoming`을 수정한다.

```csharp
        private static int FoldIncoming(EffectContext ctx, StatusBag bag, int damage)
            => StatusDamageFold.Incoming(bag, ctx.StatusRegistry, ctx.State.StatusRules, damage);
```

`Assets/Core/Status/StatusExecutionOrder.cs`의 `ExecutionOrderFor`에 규칙 인자를 추가한다.

```csharp
        public static int ExecutionOrderFor(
            int baseExecutionOrder,
            StatusBag bag,
            StatusRegistry registry,
            StatusRuleSet rules)
        {
            if (registry == null || bag == null)
            {
                return baseExecutionOrder;
            }

            var result = baseExecutionOrder;
            foreach (var status in bag.All)
            {
                if (registry.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.Entity)
                {
                    result = behavior.ModifyExecutionOrder(
                        result,
                        new StatusContext { Instance = status, Rules = rules });
                }
            }

            return result;
        }
```

`Assets/Core/Simulation/DeckCombatSession.cs`의 두 호출 지점(220~222행 부근의
`EffectiveExecutionOrderFor`, 382행 부근)에 `_state.StatusRules`를 마지막 인자로 추가한다.

`Assets/Core/Combat/TurnResolver.cs`의 `IsInterceptedByStatus`에서 `StatusContext` 생성 시
규칙을 함께 넘긴다. 이 메서드는 `state`를 받지 않으므로 시그니처에 `CombatState state`를 추가하고
`ResolveCard`의 호출 지점(49행 부근 `IsInterceptedByStatus(card)`)을
`IsInterceptedByStatus(state, card)`로 바꾼다.

```csharp
        private bool IsInterceptedByStatus(CombatState state, ExecutionCardInstance card)
        {
            if (_statuses == null)
            {
                return false;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(card.Statuses.All);
            foreach (var status in snapshot)
            {
                if (_statuses.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.CardInstance
                    && behavior.InterceptCardResolve(
                        new StatusContext { Instance = status, Rules = state.StatusRules }))
                {
                    card.Statuses.Consume(status);
                    return true;
                }
            }

            return false;
        }
```

- [ ] **Step 9: 기존 테스트의 호출 지점을 갱신한다**

`Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs`의 `ExecutionOrderFor` 호출 5곳에
`StatusRuleCatalog.Default()`를 마지막 인자로 추가한다. 예:

```csharp
            Assert.AreEqual(8, StatusExecutionOrder.ExecutionOrderFor(5, bag, Registry(), StatusRuleCatalog.Default()));
```

`null` registry / `null` bag을 넘기는 두 케이스도 같은 방식으로 인자를 더한다.

- [ ] **Step 10: 테스트가 통과하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 401 tests (397 + 신규 4).

- [ ] **Step 11: 커밋**

```bash
git add Assets/Core/Status Assets/Core/Combat/CombatState.cs Assets/Core/Combat/TurnResolver.cs Assets/Core/Effects/DamageHandler.cs Assets/Core/Simulation/DeckCombatSession.cs Assets/Core/Tests/EditMode/StatusTests.cs Assets/Core/Tests/EditMode/SlowHasteStatusTests.cs
git commit -m "refactor: move status multipliers into runtime-tunable StatusRules"
```

---

### Task 3: 약화 (주는 피해 −25%)

주는 피해를 접는 훅이 없다. `IStatusBehavior`에 추가하고, 공격자(카드 소유자)의 StatusBag을
해결하는 경로를 만든다. 대상의 bag만 오가던 `EffectContext`에 행위자 bag을 싣는다.

**Files:**
- Create: `Assets/Core/Combat/CardActor.cs`
- Create: `Assets/Core/Status/WeakBehavior.cs`
- Create: `Assets/Core/Tests/EditMode/DebuffStatusTests.cs`
- Modify: `Assets/Core/Status/IStatusBehavior.cs`
- Modify: `Assets/Core/Status/StatusKey.cs`
- Modify: `Assets/Core/Status/StatusDamageFold.cs`
- Modify: `Assets/Core/Status/StatusRuleCatalog.cs`
- Modify: `Assets/Core/Effects/IEffectHandler.cs`
- Modify: `Assets/Core/Effects/DamageHandler.cs`
- Modify: `Assets/Core/Combat/TurnResolver.cs`
- Modify: `Assets/Core/Simulation/CombatRegistries.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Test: `Assets/Core/Tests/EditMode/AuthoringValidationTests.cs`

**Interfaces:**
- Consumes: `StatusRule.Apply` (Task 2), `StatusContext.Rules` (Task 2)
- Produces:
  - `StatusKeys.Weak`
  - `IStatusBehavior.ModifyOutgoingDamage(int damage, StatusContext ctx)` (기본 항등)
  - `CardActor.StatusesFor(CombatState state, ExecutionCardInstance card) -> StatusBag` (해결 불가 시 null)
  - `EffectContext.ActorStatuses` (StatusBag)
  - `StatusDamageFold.Outgoing(StatusBag, StatusRegistry, StatusRuleSet, int)`
  - `StatusRuleCatalog.WeakOutgoingPercent = 75`

- [ ] **Step 1: 실패하는 테스트를 작성한다**

Create `Assets/Core/Tests/EditMode/DebuffStatusTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    /// <summary>약화·취약·손상. count는 남은 턴이고 배율은 StatusRules에서 온다. 배율은 단계마다
    /// 정수로 버리며 피해 최소 1을 보장하지 않는다.</summary>
    public class DebuffStatusTests
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
            return r;
        }

        private static ExecutionCardInstance PlayerStrike(string id, int damage)
        {
            var def = new CardDefinition(id, id, Side.Player, 1,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
        }

        [Test]
        public void Weak_reduces_outgoing_damage_by_the_rule_multiplier()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 8));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt); // floor(8 x 0.75) = 6
            Assert.AreEqual(24, enemy.Hp);
        }

        [Test]
        public void Weak_floors_and_does_not_guarantee_minimum_damage()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 1));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(0, ((CardResolved)events[1]).DamageDealt); // floor(1 x 0.75) = 0
            Assert.AreEqual(30, enemy.Hp);
        }

        [Test]
        public void Weak_stacking_extends_duration_not_intensity()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(4)); // 재부여 = 수명 갱신
            var enemy = new Enemy("goblin", 30);
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 8));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(6, ((CardResolved)events[1]).DamageDealt); // 여전히 x0.75
            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Weak).Count); // 4 -> 턴 끝에 3
        }

        [Test]
        public void Weak_then_vulnerable_floors_at_each_stage()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2));
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Vulnerable, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 10));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // floor(10 x 0.75) = 7, floor(7 x 1.5) = 10
            Assert.AreEqual(10, ((CardResolved)events[1]).DamageDealt);
        }

        [Test]
        public void Weak_on_the_target_does_not_reduce_damage_it_receives()
        {
            var state = new CombatState();
            state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Weak, StatusLifetime.Turns(2)); // 대상 쪽 약화는 무관
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerStrike("strike", 8));

            var events = new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(8, ((CardResolved)events[1]).DamageDealt);
        }
    }
}
```

`AuthoringValidationTests.cs`의 `Default_context_exposes_registered_status_keys_in_id_order`
기대 배열에 `StatusKeys.Weak`를 id 사전순 위치(`Vulnerable` 뒤, 즉 마지막)에 추가한다.

```csharp
            Assert.That(keys, Is.EqualTo(new[] {
                StatusKeys.Block,
                StatusKeys.Contagion,
                StatusKeys.Haste,
                StatusKeys.Poison,
                StatusKeys.PoisonDormant,
                StatusKeys.PoisonStasis,
                StatusKeys.RewardNullified,
                StatusKeys.Slow,
                StatusKeys.Stun,
                StatusKeys.Vulnerable,
                StatusKeys.Weak
            }));
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~DebuffStatusTests"`

Expected: 컴파일 실패 — `WeakBehavior`, `StatusKeys.Weak` 없음 (CS0246/CS0117).

- [ ] **Step 3: 키를 추가한다**

`Assets/Core/Status/StatusKey.cs`의 `StatusKeys`에 추가한다.

```csharp
        public static readonly StatusKey Weak = new StatusKey("weak");
```

- [ ] **Step 4: 주는 피해 훅을 추가한다**

`Assets/Core/Status/IStatusBehavior.cs`의 `IStatusBehavior`에서 `ModifyIncomingDamage` 선언 아래에
추가한다.

```csharp
        /// <summary>Entity-scoped: fold into damage the holder is about to DEAL (e.g. weak).</summary>
        int ModifyOutgoingDamage(int damage, StatusContext ctx);
```

같은 파일 `StatusBehavior`에 기본값을 추가한다.

```csharp
        public virtual int ModifyOutgoingDamage(int damage, StatusContext ctx) => damage;
```

- [ ] **Step 5: 약화 행동을 만든다**

Create `Assets/Core/Status/WeakBehavior.cs`:

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>약화: the holder deals less damage, by the multiplier in this combat's StatusRules
    /// (default 75%). Folded on the acting side before the target's incoming statuses, so a weak
    /// attacker hitting a vulnerable target floors twice. count is remaining turns, not intensity.</summary>
    public sealed class WeakBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Weak;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyOutgoingDamage(int damage, StatusContext ctx)
            => ctx.Rules.For(Key).Apply(damage);
    }
}
```

- [ ] **Step 6: 기본 배율을 카탈로그에 등록한다**

`Assets/Core/Status/StatusRuleCatalog.cs`를 수정한다.

```csharp
        public const int VulnerableIncomingPercent = 150;
        public const int WeakOutgoingPercent = 75;

        public static StatusRuleSet Default()
        {
            var rules = new StatusRuleSet();
            rules.Set(StatusKeys.Vulnerable, new StatusRule { MultiplierPercent = VulnerableIncomingPercent });
            rules.Set(StatusKeys.Weak, new StatusRule { MultiplierPercent = WeakOutgoingPercent });
            return rules;
        }
```

- [ ] **Step 7: 주는 피해 fold를 추가한다**

`Assets/Core/Status/StatusDamageFold.cs`에 메서드를 추가한다 (`Incoming` 아래).

```csharp
        /// <summary>행위자의 엔티티 스코프 상태를 접어 주는 피해를 계산한다. 흡수 층은 받는 쪽
        /// 개념이므로 여기서는 배율 층만 접는다.</summary>
        public static int Outgoing(StatusBag bag, StatusRegistry registry, StatusRuleSet rules, int damage)
        {
            if (registry == null || bag == null)
            {
                return damage;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior)
                    || behavior.DamageLayer != StatusDamageLayer.Multiplier)
                {
                    continue;
                }

                var after = behavior.ModifyOutgoingDamage(
                    damage,
                    new StatusContext { Instance = status, Rules = rules });
                if (after != damage)
                {
                    bag.Consume(status);
                }

                damage = after;
            }

            return damage;
        }
```

- [ ] **Step 8: 행위자 bag 해결 경로를 만든다**

Create `Assets/Core/Combat/CardActor.cs`:

```csharp
namespace FateWeaver.Core.Combat
{
    /// <summary>카드를 실제로 쓰는 쪽(행위자)의 StatusBag을 찾는다. OwnerId가 있으면 그것으로,
    /// 없으면 해당 진영에 후보가 정확히 하나일 때만 확정한다(단일 적·솔로 러너 호환). 후보가
    /// 둘 이상이고 OwnerId가 없으면 null을 돌려주어 행위자 상태를 적용하지 않는다 — 러너가
    /// OwnerId를 채우지 않는 다중 적 경로에서 임의의 대상을 고르지 않기 위한 것이다.</summary>
    public static class CardActor
    {
        public static Status.StatusBag StatusesFor(CombatState state, ExecutionCardInstance card)
        {
            if (state == null || card == null)
            {
                return null;
            }

            if (card.Def.Side == Cards.Side.Player)
            {
                var member = string.IsNullOrEmpty(card.OwnerId)
                    ? (state.Party.Count == 1 ? state.Party[0] : null)
                    : PartyTargeting.LivingById(state, card.OwnerId);
                return member?.Statuses;
            }

            var enemy = string.IsNullOrEmpty(card.OwnerId)
                ? (state.Enemies.Count == 1 ? state.Enemies[0] : null)
                : FindLivingEnemy(state, card.OwnerId);
            return enemy?.Statuses;
        }

        private static Enemy FindLivingEnemy(CombatState state, string enemyId)
        {
            foreach (var enemy in state.Enemies)
            {
                if (enemy.Id == enemyId && enemy.Hp > 0)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
```

- [ ] **Step 9: EffectContext에 행위자 bag을 싣는다**

`Assets/Core/Effects/IEffectHandler.cs`의 `EffectContext`에서 `StatusRegistry` 아래에 추가한다.

```csharp
        /// <summary>이 카드를 쓰는 쪽의 상태 (약화처럼 주는 피해를 접는 훅이 읽는다).
        /// 소유자를 확정할 수 없으면 null이며, 그 경우 행위자 상태는 적용되지 않는다.</summary>
        public Status.StatusBag ActorStatuses;
```

`Assets/Core/Combat/TurnResolver.cs`의 `EffectContext` 생성 블록(81~89행 부근)에 한 줄을 더한다.

```csharp
                var ctx = new EffectContext
                {
                    Card = card,
                    State = state,
                    ResolutionContext = resolutionContext,
                    StatusRegistry = _statuses,
                    ActorStatuses = CardActor.StatusesFor(state, card),
                    Effect = effect,
                    EffectValue = ResolveEffectValue(effect, tier)
                };
```

- [ ] **Step 10: DamageHandler가 주는 피해를 접게 한다**

`Assets/Core/Effects/DamageHandler.cs`의 `Apply` 시작부에서 `amount` 계산 줄을 교체한다.

```csharp
            var amount = FoldOutgoing(ctx, ctx.EffectValue + ctx.Card.ConsumePendingDamageBonus());
```

파일 끝 `FoldIncoming` 아래에 추가한다.

```csharp
        /// <summary>Folds the acting side's entity-scoped statuses into the damage it deals
        /// (e.g. Weak). Applied once per effect, before any target's incoming statuses.</summary>
        private static int FoldOutgoing(EffectContext ctx, int damage)
            => StatusDamageFold.Outgoing(
                ctx.ActorStatuses, ctx.StatusRegistry, ctx.State.StatusRules, damage);
```

- [ ] **Step 11: 레지스트리와 설명에 등록한다**

`Assets/Core/Simulation/CombatRegistries.cs`의 `Statuses()`에 추가한다.

```csharp
            statuses.Register(new WeakBehavior());
```

`Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`의 상태 이름 등록에 추가한다.

```csharp
            statuses.Register(StatusKeys.Weak, "약화");
```

- [ ] **Step 12: 테스트가 통과하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 406 tests (401 + 신규 5).

- [ ] **Step 13: 커밋**

```bash
git add Assets/Core/Status Assets/Core/Combat/CardActor.cs Assets/Core/Combat/TurnResolver.cs Assets/Core/Effects Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs Assets/Core/Tests/EditMode/DebuffStatusTests.cs Assets/Core/Tests/EditMode/AuthoringValidationTests.cs
git commit -m "feat: add weak status reducing outgoing damage"
```

---

### Task 4: 손상 (방어도 획득 −25%)

방어도 획득은 `ApplyStatusHandler.ApplyTo` 한 곳으로 모인다. 획득 수치를 접는 훅을 추가하고,
어느 키에 걸릴지는 행동이 스스로 판단하게 한다(중앙 switch 금지).

**Files:**
- Create: `Assets/Core/Status/DamagedBehavior.cs`
- Modify: `Assets/Core/Status/IStatusBehavior.cs`
- Modify: `Assets/Core/Status/StatusKey.cs`
- Modify: `Assets/Core/Status/StatusRuleCatalog.cs`
- Modify: `Assets/Core/Status/StatusDamageFold.cs`
- Modify: `Assets/Core/Effects/ApplyStatusHandler.cs`
- Modify: `Assets/Core/Simulation/CombatRegistries.cs`
- Modify: `Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`
- Test: `Assets/Core/Tests/EditMode/DebuffStatusTests.cs`, `Assets/Core/Tests/EditMode/AuthoringValidationTests.cs`

**Interfaces:**
- Consumes: `StatusRule.Apply`, `StatusContext.Rules` (Task 2)
- Produces:
  - `StatusKeys.Damaged`
  - `IStatusBehavior.ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx)` (기본 항등)
  - `StatusDamageFold.GainedMagnitude(StatusKey, StatusBag, StatusRegistry, StatusRuleSet, int)`
  - `StatusRuleCatalog.DamagedBlockGainPercent = 75`

- [ ] **Step 1: 실패하는 테스트를 작성한다**

`DebuffStatusTests.cs`의 `Statuses()` 헬퍼에 손상을 등록한다.

```csharp
            r.Register(new DamagedBehavior());
```

같은 파일에 헬퍼와 테스트를 추가한다.

```csharp
        // 방어를 Turns(2)로 거는 것은 저작 관례가 아니라 테스트 편의다. ThisTurn으로 걸면
        // 턴 종료 정리가 인스턴스를 지워 Magnitude를 조회할 수 없다.
        private static ExecutionCardInstance PlayerGuard(string id, int block)
        {
            var def = new CardDefinition(id, id, Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Block, StatusLifetime.Turns(2), StatusApplyTarget.Self, block)
                });
            return new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId };
        }

        [Test]
        public void Damaged_reduces_block_gained_by_the_rule_multiplier()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(new Enemy("goblin", 30));
            state.Zone.Add(PlayerGuard("guard", 5));

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            // floor(5 x 0.75) = 3
            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Block).Magnitude);
        }

        [Test]
        public void Damaged_does_not_reduce_other_gained_statuses()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            player.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(new Enemy("goblin", 30));
            var def = new CardDefinition("hex", "hex", Side.Player, 1,
                new[]
                {
                    EffectData.ApplyStatus(
                        StatusKeys.Vulnerable, StatusLifetime.Turns(4), StatusApplyTarget.Self)
                });
            state.Zone.Add(new ExecutionCardInstance(def) { OwnerId = CombatState.SoloPlayerId });

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(3, player.Statuses.Get(StatusKeys.Vulnerable).Count); // 4 -> 턴 끝에 3
        }

        [Test]
        public void Damaged_on_the_actor_does_not_reduce_block_gained_by_someone_else()
        {
            var state = new CombatState();
            var player = state.AddSoloPlayer(30);
            var enemy = new Enemy("goblin", 30);
            enemy.Statuses.Add(StatusKeys.Damaged, StatusLifetime.Turns(2));
            state.Enemies.Add(enemy);
            state.Zone.Add(PlayerGuard("guard", 5));

            new TurnResolver(Effects(), Statuses()).Resolve(state, 0);

            Assert.AreEqual(5, player.Statuses.Get(StatusKeys.Block).Magnitude); // 감소 없음
        }
```

`AuthoringValidationTests.cs`의 기대 배열에 `StatusKeys.Damaged`를 id 사전순 위치
(`Contagion` 뒤, `Haste` 앞)에 추가한다.

```csharp
            Assert.That(keys, Is.EqualTo(new[] {
                StatusKeys.Block,
                StatusKeys.Contagion,
                StatusKeys.Damaged,
                StatusKeys.Haste,
                StatusKeys.Poison,
                StatusKeys.PoisonDormant,
                StatusKeys.PoisonStasis,
                StatusKeys.RewardNullified,
                StatusKeys.Slow,
                StatusKeys.Stun,
                StatusKeys.Vulnerable,
                StatusKeys.Weak
            }));
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo --filter "FullyQualifiedName~DebuffStatusTests"`

Expected: 컴파일 실패 — `DamagedBehavior`, `StatusKeys.Damaged` 없음.

- [ ] **Step 3: 키와 기본 배율을 추가한다**

`Assets/Core/Status/StatusKey.cs`:

```csharp
        public static readonly StatusKey Damaged = new StatusKey("damaged");
```

`Assets/Core/Status/StatusRuleCatalog.cs`:

```csharp
        public const int DamagedBlockGainPercent = 75;
```

`Default()`에 추가한다.

```csharp
            rules.Set(StatusKeys.Damaged, new StatusRule { MultiplierPercent = DamagedBlockGainPercent });
```

- [ ] **Step 4: 획득 수치 훅을 추가한다**

`Assets/Core/Status/IStatusBehavior.cs`의 `IStatusBehavior`에 추가한다.

```csharp
        /// <summary>Entity-scoped: fold into the magnitude the holder is about to GAIN from an applied
        /// status (e.g. damaged reducing block gain). The behavior decides which gained keys it affects,
        /// so no central switch grows here.</summary>
        int ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx);
```

`StatusBehavior`에 기본값을 추가한다.

```csharp
        public virtual int ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx)
            => magnitude;
```

- [ ] **Step 5: 손상 행동을 만든다**

Create `Assets/Core/Status/DamagedBehavior.cs`:

```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>손상: the holder gains less block, by the multiplier in this combat's StatusRules
    /// (default 75%). Folded where the block is gained, on the holder receiving it — not on the card's
    /// actor. count is remaining turns, not intensity.</summary>
    public sealed class DamagedBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Damaged;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyGainedMagnitude(StatusKey gained, int magnitude, StatusContext ctx)
            => gained == StatusKeys.Block ? ctx.Rules.For(Key).Apply(magnitude) : magnitude;
    }
}
```

- [ ] **Step 6: 획득 수치 fold를 추가한다**

`Assets/Core/Status/StatusDamageFold.cs`에 추가한다.

```csharp
        /// <summary>보유자가 얻으려는 상태 수치를 그 보유자의 상태로 접는다 (예: 손상이 방어도
        /// 획득을 깎는다).</summary>
        public static int GainedMagnitude(
            StatusKey gained,
            StatusBag bag,
            StatusRegistry registry,
            StatusRuleSet rules,
            int magnitude)
        {
            if (registry == null || bag == null)
            {
                return magnitude;
            }

            // Snapshot: consuming may modify the bag mid-iteration.
            var snapshot = new List<StatusInstance>(bag.All);
            foreach (var status in snapshot)
            {
                if (!registry.TryResolve(status.Key, out var behavior))
                {
                    continue;
                }

                var after = behavior.ModifyGainedMagnitude(
                    gained,
                    magnitude,
                    new StatusContext { Instance = status, Rules = rules });
                if (after != magnitude)
                {
                    bag.Consume(status);
                }

                magnitude = after;
            }

            return magnitude;
        }
```

- [ ] **Step 7: ApplyStatusHandler가 획득 수치를 접게 한다**

`Assets/Core/Effects/ApplyStatusHandler.cs`의 `ApplyTo` 전체를 교체한다.

```csharp
        /// <summary>Stacking-aware status application: when the key's behavior declares
        /// StacksMagnitude (e.g. Block), an existing instance's Magnitude is added to rather than
        /// replaced; otherwise falls back to the legacy replace semantics. The magnitude is first
        /// folded through the RECEIVING holder's statuses (e.g. Damaged reducing block gain).</summary>
        private static void ApplyTo(EffectContext ctx, ApplyStatusPayload payload, StatusBag bag)
        {
            var magnitude = StatusDamageFold.GainedMagnitude(
                payload.Key, bag, ctx.StatusRegistry, ctx.State.StatusRules, ctx.EffectValue);

            if (ctx.StatusRegistry != null
                && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior)
                && behavior.StacksMagnitude)
            {
                bag.Stack(payload.Key, payload.Lifetime, magnitude);
                return;
            }

            bag.Add(payload.Key, payload.Lifetime, magnitude);
        }
```

- [ ] **Step 8: 레지스트리와 설명에 등록한다**

`Assets/Core/Simulation/CombatRegistries.cs`:

```csharp
            statuses.Register(new DamagedBehavior());
```

`Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs`:

```csharp
            statuses.Register(StatusKeys.Damaged, "손상");
```

- [ ] **Step 9: 테스트가 통과하는 것을 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 총 409 tests (406 + 신규 3).

- [ ] **Step 10: 커밋**

```bash
git add Assets/Core/Status Assets/Core/Effects/ApplyStatusHandler.cs Assets/Core/Simulation/CombatRegistries.cs Assets/Core/Simulation/Descriptions/KoreanDescriptionCatalog.cs Assets/Core/Tests/EditMode/DebuffStatusTests.cs Assets/Core/Tests/EditMode/AuthoringValidationTests.cs
git commit -m "feat: add damaged status reducing block gain"
```

---

### Task 5: 문서 갱신과 마무리

**Files:**
- Modify: `docs/superpowers/README.md`
- Modify: `docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md`
- Modify: `docs/superpowers/plans/2026-07-30-status-rule-and-debuffs.md` (이 문서)

- [ ] **Step 1: 백로그의 해소된 항목을 표시한다**

`docs/superpowers/plans/2026-07-16-architecture-refactor-backlog.md` §12.2의
`VulnerableBehavior 하드코딩` 항목에 해소 사실을 한 줄로 덧붙인다 — Task 2에서 배율이
`StatusRule`로 이동했고, `Magnitude`를 세기로 쓰지 않는 이유(count = 남은 턴)를 함께 적는다.

- [ ] **Step 2: 이 계획의 상태를 갱신한다**

이 문서 머리말의 `상태: active`를 유지하되, count 단일화가 후속 계획으로 남아 있음을 명시한
"범위 밖" 절이 그대로 유효한지 확인한다. 3종 구현이 끝났으면 완료 표시를 남긴다.

- [ ] **Step 3: 전체 테스트를 확인한다**

Run: `dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo`

Expected: `Failed: 0`, 409 tests

- [ ] **Step 4: 워킹 트리를 확인하고 커밋한다**

```bash
git status
```

의도하지 않은 생성물이 없는지 확인한 뒤 문서 변경만 스테이징한다.

```bash
git add docs/superpowers
git commit -m "docs: record status rule debuff plan outcome"
```

---

## Unity 레이어 확인 (사용자 검증 항목)

이 계획은 코어만 바꾸므로 헤드리스 테스트로 전부 검증된다. 다만 다음은 Unity에서 확인이 필요하다.

- 약화·손상 상태의 표시 이름과 아이콘이 전투 화면에 나오는지 (`CardStatusIcon`, `UnitView`)
- 카드 설명 자동 생성에 새 상태 이름이 반영되는지

전용 워크트리에서는 GUI Editor를 열지 않는다(규칙 17). 사용자가 병합 전 수동 검증을 요청하면
해당 워크트리를 `-projectPath`로 열어 확인한다.

## 후속 작업

| 항목 | 이유 |
|---|---|
| count 단일화 (`StatusLifetime` → 상태별 감쇠 규칙) | "방어를 영구로, 독을 이번 턴만으로" 요구를 충족한다. 이 계획의 "범위 밖" 절 참고 |
| `ApplyStatusHandler`의 Self 해결을 `CardActor`와 통합 | 소유자 해결 로직이 두 곳에 남아 있다 |
| 같은 보유자에게 곱셈 상태가 둘 이상 붙을 때의 순서 규칙 | 단계별 버림에서는 곱셈 순서가 결과를 바꾼다 |
| 카드 변형과 런타임 콘텐츠 로딩 | `specs/2026-07-30-card-mutation-and-runtime-content-design.md` |
