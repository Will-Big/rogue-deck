# 핵심 상태이상 시스템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 미래 영역의 주도력/순서를 비트는 핵심 상태이상(둔화·가속 신규, 고정 신규)을 엔진·저작·콘텐츠·표기까지 추가한다.

**Architecture:** 엔티티 상태(둔화/가속)는 `IStatusBehavior`에 새 `ModifyInitiative` 훅을 추가하고, 카드가 미래 영역에 *진입할 때*(적 카드 = `BeginTurn`, 플레이어 카드 = `PlayActionCard`) 소유 엔티티의 상태를 통과시켜 `ActionCardInstance.Initiative`에 합산(bake)한다. 정렬은 기존 `FutureZone.ResolutionOrder()`(주도력 오름차순)가 그대로 처리하므로 해석/UI 순서가 자동 일관된다. 고정(Lock)은 기존 `IsLocked` 경로를 재사용한다.

**동작 명시 (중요):** 둔화/가속은 *해석 단계*에 적용되므로 **이번 턴이 아니라 다음 턴부터** 작용하는 다턴 셋업 컨디션이다(이번 턴 즉시 템포 아님). 같은 턴 즉시 효과를 원하면 운명 카드 전달 + 정렬 시점 live-fold가 필요 — 후속.

**Tech Stack:** C# 9 (Unity 6 / netstandard2.1), 순수 코어 + Simulation, 헤드리스 `dotnet test`.

**검증 명령(공통):**
```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~CLASS"
```
전체: 같은 명령에서 `--filter` 제거. 통과 표시는 "통과!".

---

## File Structure

**생성**
- `Assets/FateWeaver/Core/Status/SlowBehavior.cs` — 둔화: 엔티티 스코프, 주도력 +Magnitude.
- `Assets/FateWeaver/Core/Status/HasteBehavior.cs` — 가속: 엔티티 스코프, 주도력 −Magnitude.
- `Assets/FateWeaver/Core/Status/StatusInitiative.cs` — 보유자 엔티티 상태를 접어 실효 주도력 계산하는 정적 헬퍼.
- `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs` — 훅/행동/헬퍼/세션 통합.
- `Assets/FateWeaver/Tests/EditMode/LockCardTests.cs` — 고정.

**수정**
- `Assets/FateWeaver/Core/Status/IStatusBehavior.cs` — `ModifyInitiative` 훅 + 기본 no-op.
- `Assets/FateWeaver/Core/Status/StatusKey.cs` — `Slow`, `Haste` 키.
- `Assets/FateWeaver/Core/Cards/CardDefinition.cs` — `StartsLocked` init-prop.
- `Assets/FateWeaver/Simulation/CombatRegistries.cs` — Slow/Haste 행동 등록.
- `Assets/FateWeaver/Simulation/DeckCombatSession.cs` — 레지스트리 보관 + 진입 시 주도력/잠금 bake.
- `Assets/FateWeaver/Simulation/Authoring/EffectSpec.cs` — `StatusKindRef`에 `Slow`, `Haste`.
- `Assets/FateWeaver/Simulation/Authoring/CardSpecMapper.cs` — `ToStatusKey` 케이스.
- `Assets/FateWeaver/Simulation/Authoring/StarterDeckSpecs.cs` — 둔화/가속 전달 카드 팩토리.
- `Assets/FateWeaver/Tests/EditMode/CardSpecMapperTests.cs` — Slow/Haste 매핑.
- `Assets/FateWeaver/Unity/PlaytestKoreanText.cs` — 둔화/가속 한글 상태명.

---

## Task 1: ModifyInitiative 훅

**Files:**
- Modify: `Assets/FateWeaver/Core/Status/IStatusBehavior.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs` (생성)

- [ ] **Step 1: 실패하는 테스트 작성**

`Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs`:
```csharp
using NUnit.Framework;
using FateWeaver.Core.Status;

namespace FateWeaver.Tests
{
    public class SlowHasteStatusTests
    {
        private static StatusContext Ctx(StatusKey key, int magnitude) =>
            new StatusContext { Instance = new StatusInstance(key, StatusLifetime.Turns(2), magnitude) };

        [Test]
        public void Base_behavior_does_not_change_initiative()
        {
            var block = new BlockBehavior();
            Assert.AreEqual(5, block.ModifyInitiative(5, Ctx(StatusKeys.Block, 3)));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: 컴파일 실패 — `ModifyInitiative` 정의 없음.

- [ ] **Step 3: 훅 추가**

`IStatusBehavior.cs`의 인터페이스에 메서드 추가, 추상 베이스에 기본 no-op 추가:
```csharp
    public interface IStatusBehavior
    {
        StatusKey Key { get; }
        StatusScope Scope { get; }

        int ModifyIncomingDamage(int damage, StatusContext ctx);

        bool InterceptCardResolve(StatusContext ctx);

        /// <summary>Entity-scoped: fold into the initiative of a card owned by the holder (e.g. slow/haste).</summary>
        int ModifyInitiative(int initiative, StatusContext ctx);
    }
```
그리고 `StatusBehavior` 추상 클래스에:
```csharp
        public virtual int ModifyInitiative(int initiative, StatusContext ctx) => initiative;
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: PASS (1).

- [ ] **Step 5: 커밋**

```bash
git add Assets/FateWeaver/Core/Status/IStatusBehavior.cs Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs
git commit -m "feat(status): add ModifyInitiative hook (no-op default)"
```

---

## Task 2: 둔화/가속 키 + 행동

**Files:**
- Modify: `Assets/FateWeaver/Core/Status/StatusKey.cs`
- Create: `Assets/FateWeaver/Core/Status/SlowBehavior.cs`, `Assets/FateWeaver/Core/Status/HasteBehavior.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs`

- [ ] **Step 1: 실패하는 테스트 추가**

`SlowHasteStatusTests`에 추가:
```csharp
        [Test]
        public void Slow_adds_magnitude_to_initiative()
        {
            var slow = new SlowBehavior();
            Assert.AreEqual(StatusScope.Entity, slow.Scope);
            Assert.AreEqual(StatusKeys.Slow, slow.Key);
            Assert.AreEqual(8, slow.ModifyInitiative(5, Ctx(StatusKeys.Slow, 3)));
        }

        [Test]
        public void Haste_subtracts_magnitude_from_initiative()
        {
            var haste = new HasteBehavior();
            Assert.AreEqual(StatusScope.Entity, haste.Scope);
            Assert.AreEqual(StatusKeys.Haste, haste.Key);
            Assert.AreEqual(2, haste.ModifyInitiative(5, Ctx(StatusKeys.Haste, 3)));
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: 컴파일 실패 — `StatusKeys.Slow`/`SlowBehavior` 없음.

- [ ] **Step 3: 키 + 행동 구현**

`StatusKey.cs`의 `StatusKeys`에 추가:
```csharp
        public static readonly StatusKey Slow = new StatusKey("slow");
        public static readonly StatusKey Haste = new StatusKey("haste");
```
`SlowBehavior.cs`:
```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>둔화: the holder's cards resolve later (initiative += Magnitude). Entity-scoped.</summary>
    public sealed class SlowBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Slow;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyInitiative(int initiative, StatusContext ctx)
            => initiative + ctx.Instance.Magnitude;
    }
}
```
`HasteBehavior.cs`:
```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>가속: the holder's cards resolve sooner (initiative -= Magnitude). Entity-scoped.</summary>
    public sealed class HasteBehavior : StatusBehavior
    {
        public override StatusKey Key => StatusKeys.Haste;
        public override StatusScope Scope => StatusScope.Entity;

        public override int ModifyInitiative(int initiative, StatusContext ctx)
            => initiative - ctx.Instance.Magnitude;
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: PASS (3).

- [ ] **Step 5: 커밋**

```bash
git add Assets/FateWeaver/Core/Status/StatusKey.cs Assets/FateWeaver/Core/Status/SlowBehavior.cs Assets/FateWeaver/Core/Status/HasteBehavior.cs Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs
git commit -m "feat(status): add Slow/Haste keys and behaviors"
```

---

## Task 3: 실효 주도력 fold 헬퍼

**Files:**
- Create: `Assets/FateWeaver/Core/Status/StatusInitiative.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs`

- [ ] **Step 1: 실패하는 테스트 추가**

`SlowHasteStatusTests`에 추가:
```csharp
        private static StatusRegistry Registry()
        {
            var r = new StatusRegistry();
            r.Register(new SlowBehavior());
            r.Register(new HasteBehavior());
            r.Register(new StunBehavior());
            return r;
        }

        [Test]
        public void Fold_applies_entity_statuses_only()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            Assert.AreEqual(8, StatusInitiative.InitiativeFor(5, bag, Registry()));

            var bag2 = new StatusBag();
            bag2.Add(StatusKeys.Haste, StatusLifetime.Turns(2), 2);
            Assert.AreEqual(3, StatusInitiative.InitiativeFor(5, bag2, Registry()));
        }

        [Test]
        public void Fold_ignores_card_scoped_and_null_inputs()
        {
            var bag = new StatusBag();
            bag.Add(StatusKeys.Stun, StatusLifetime.UntilConsumed(1)); // card-scoped -> ignored
            Assert.AreEqual(5, StatusInitiative.InitiativeFor(5, bag, Registry()));
            Assert.AreEqual(5, StatusInitiative.InitiativeFor(5, bag, null));
            Assert.AreEqual(5, StatusInitiative.InitiativeFor(5, null, Registry()));
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: 컴파일 실패 — `StatusInitiative` 없음.

- [ ] **Step 3: 헬퍼 구현**

`StatusInitiative.cs`:
```csharp
namespace FateWeaver.Core.Status
{
    /// <summary>Folds a holder's entity-scoped statuses into the initiative of a card it owns.
    /// Mirrors DamageHandler.FoldIncoming, but duration-based (no charge consume).</summary>
    public static class StatusInitiative
    {
        public static int InitiativeFor(int baseInitiative, StatusBag bag, StatusRegistry registry)
        {
            if (registry == null || bag == null)
            {
                return baseInitiative;
            }

            var result = baseInitiative;
            foreach (var status in bag.All)
            {
                if (registry.TryResolve(status.Key, out var behavior)
                    && behavior.Scope == StatusScope.Entity)
                {
                    result = behavior.ModifyInitiative(result, new StatusContext { Instance = status });
                }
            }

            return result;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: PASS (5).

- [ ] **Step 5: 커밋**

```bash
git add Assets/FateWeaver/Core/Status/StatusInitiative.cs Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs
git commit -m "feat(status): add StatusInitiative fold helper (entity-scoped only)"
```

---

## Task 4: 세션이 진입 시 주도력 bake (둔화/가속 통합)

**Files:**
- Modify: `Assets/FateWeaver/Simulation/CombatRegistries.cs`
- Modify: `Assets/FateWeaver/Simulation/DeckCombatSession.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs`

- [ ] **Step 1: 실패하는 통합 테스트 추가**

`SlowHasteStatusTests`에 추가 (using 추가: `using System.Collections.Generic;`, `using System.Linq;`, `using FateWeaver.Core.Cards;`, `using FateWeaver.Core.Combat;`, `using FateWeaver.Core.Effects;`, `using FateWeaver.Simulation;`):
```csharp
        private static CardDefinition PlayerStrike() => new CardDefinition(
            "p_strike", "찌르기", Side.Player, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) }) { Cost = 0, Category = CardCategory.Action };

        private static CardDefinition EnemyJab() => new CardDefinition(
            "e_jab", "적찌르기", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) }) { Cost = 0, Category = CardCategory.Action };

        private static EnemyIntent JabEachTurn() => new EnemyIntent(new IReadOnlyList<CardDefinition>[]
        {
            new[] { EnemyJab() }, new[] { EnemyJab() }
        });

        [Test]
        public void Enemy_slow_raises_next_turn_enemy_card_initiative()
        {
            var session = new DeckCombatSession(
                new[] { PlayerStrike() }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);
            session.State.Enemies[0].Statuses.Add(StatusKeys.Slow, StatusLifetime.Turns(2), 3);
            session.ResolveTurn();
            session.BeginNextTurn();
            var jab = session.CurrentOrder.First(c => c.Def.Id == "e_jab");
            Assert.AreEqual(8, jab.Initiative); // base 5 + slow 3
        }

        [Test]
        public void Player_haste_lowers_initiative_of_cards_placed_after_it()
        {
            var session = new DeckCombatSession(
                new[] { PlayerStrike() }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);
            session.State.PlayerStatuses.Add(StatusKeys.Haste, StatusLifetime.Turns(2), 3);
            session.PlayActionCard(0);
            var strike = session.CurrentOrder.First(c => c.Def.Id == "p_strike");
            Assert.AreEqual(2, strike.Initiative); // base 5 - haste 3
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: FAIL — 주도력이 bake 안 됨(둘 다 5로 나옴).

- [ ] **Step 3: 등록 + bake 구현**

`CombatRegistries.Statuses()`에 추가(기존 등록 뒤):
```csharp
            statuses.Register(new SlowBehavior());
            statuses.Register(new HasteBehavior());
```
`DeckCombatSession.cs` — 필드 추가:
```csharp
        private readonly StatusRegistry _statuses;
```
(상단 using에 `using FateWeaver.Core.Status;` 추가)
생성자에서 레지스트리를 한 번만 만들어 공유:
```csharp
            _statuses = CombatRegistries.Statuses();
            _resolver = new TurnResolver(CombatRegistries.Effects(), _statuses);
```
`BeginTurn`의 적 카드 추가 루프를 교체:
```csharp
            _state.Zone.Clear();
            var enemyBag = _state.Enemies.Count > 0 ? _state.Enemies[0].Statuses : null;
            foreach (var enemyCard in _enemyPolicy.CardsForTurn(index))
            {
                var inst = new ActionCardInstance(enemyCard);
                inst.Initiative = StatusInitiative.InitiativeFor(inst.Initiative, enemyBag, _statuses);
                _state.Zone.Add(inst);
            }
```
`PlayActionCard`의 배치 한 줄을 교체:
```csharp
            var placed = new ActionCardInstance(def);
            placed.Initiative = StatusInitiative.InitiativeFor(placed.Initiative, _state.PlayerStatuses, _statuses);
            _state.Zone.Add(placed);
```
(기존 `_state.Zone.Add(new ActionCardInstance(def));` 대체)

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: PASS (7).

- [ ] **Step 5: 회귀 확인 + 커밋**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo`
Expected: 전체 PASS.
```bash
git add Assets/FateWeaver/Simulation/CombatRegistries.cs Assets/FateWeaver/Simulation/DeckCombatSession.cs Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs
git commit -m "feat(sim): bake entity slow/haste into card initiative on zone entry"
```

---

## Task 5: 고정(Lock) — 카드 정의 + bake

**Files:**
- Modify: `Assets/FateWeaver/Core/Cards/CardDefinition.cs`
- Modify: `Assets/FateWeaver/Simulation/DeckCombatSession.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/LockCardTests.cs` (생성)

- [ ] **Step 1: 실패하는 테스트 작성**

`LockCardTests.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class LockCardTests
    {
        private static CardDefinition LockedJab() => new CardDefinition(
            "locked_jab", "고정된 일격", Side.Enemy, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 3) })
            { Cost = 0, Category = CardCategory.Action, StartsLocked = true };

        [Test]
        public void Locked_enemy_card_enters_zone_locked()
        {
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[] { new[] { LockedJab() } });
            var session = new DeckCombatSession(
                new[] { new CardDefinition("p", "p", Side.Player, CardType.Attack, 6,
                    new[] { new EffectData(EffectKeys.Damage, 1) }) { Cost = 0, Category = CardCategory.Action } },
                100, new[] { new Enemy("goblin", 100) }, intent, 3, 5, 1);

            var jab = session.CurrentOrder.First(c => c.Def.Id == "locked_jab");
            Assert.IsTrue(jab.IsLocked);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~LockCardTests"`
Expected: 컴파일 실패 — `StartsLocked` 없음.

- [ ] **Step 3: StartsLocked + bake 구현**

`CardDefinition.cs`의 `CardDefinition` record에 init-prop 추가(`FateAction` 옆):
```csharp
        /// <summary>When true, the card enters the future zone locked (fate reordering rejected).</summary>
        public bool StartsLocked { get; init; }
```
`DeckCombatSession.BeginTurn`의 적 카드 추가 루프에서 `inst.Initiative` 설정 다음 줄에 추가:
```csharp
                inst.IsLocked = enemyCard.StartsLocked;
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~LockCardTests"`
Expected: PASS (1).

- [ ] **Step 5: 운명 재배치 거부 테스트 추가 + 구현 확인**

`LockCardTests`에 추가(운명 카드로 잠긴 카드를 당기려 하면 거부):
```csharp
        [Test]
        public void Fate_cannot_reorder_a_locked_card()
        {
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[] { new[] { LockedJab() } });
            var pull = new CardDefinition("pull", "앞당김", Side.Player, CardType.Skill, 0,
                System.Array.Empty<EffectData>())
                { Cost = 1, Category = CardCategory.Fate,
                  FateAction = new FateWeaver.Core.Fate.FateActionData(
                      FateWeaver.Core.Fate.FateActionKeys.ChangeInitiative, 1, -2) };
            var session = new DeckCombatSession(
                new[] { pull }, 100, new[] { new Enemy("goblin", 100) }, intent, 3, 5, 1);

            int zoneIndex = 0;
            for (int i = 0; i < session.CurrentOrder.Count; i++)
                if (session.CurrentOrder[i].Def.Id == "locked_jab") zoneIndex = i;
            int handIndex = 0;
            for (int i = 0; i < session.Hand.Count; i++)
                if (session.Hand[i].Id == "pull") handIndex = i;

            Assert.IsFalse(session.PlayFateCard(handIndex, zoneIndex));
        }
```
Run: `dotnet test ... --filter "FullyQualifiedName~LockCardTests"`
Expected: PASS (2) — 기존 `IsLocked` 거부 로직이 이미 처리하므로 추가 구현 불필요.

- [ ] **Step 6: 커밋**

```bash
git add Assets/FateWeaver/Core/Cards/CardDefinition.cs Assets/FateWeaver/Simulation/DeckCombatSession.cs Assets/FateWeaver/Tests/EditMode/LockCardTests.cs
git commit -m "feat(cards): StartsLocked card property baked into zone (고정)"
```

---

## Task 6: 저작 매핑 (StatusKindRef → Slow/Haste)

**Files:**
- Modify: `Assets/FateWeaver/Simulation/Authoring/EffectSpec.cs`
- Modify: `Assets/FateWeaver/Simulation/Authoring/CardSpecMapper.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/CardSpecMapperTests.cs`

- [ ] **Step 1: 실패하는 테스트 추가**

`CardSpecMapperTests`에 추가(필요 using: `FateWeaver.Core.Status`, `FateWeaver.Simulation.Authoring`):
```csharp
        [Test]
        public void Maps_slow_and_haste_apply_status()
        {
            var slow = CardSpecMapper.ToEffectData(new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 3, Status = StatusKindRef.Slow,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.TargetEnemy });
            Assert.AreEqual(StatusKeys.Slow, slow.StatusKey.Value);

            var haste = CardSpecMapper.ToEffectData(new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 3, Status = StatusKindRef.Haste,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.Self });
            Assert.AreEqual(StatusKeys.Haste, haste.StatusKey.Value);
        }
```

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~CardSpecMapperTests"`
Expected: 컴파일 실패 — `StatusKindRef.Slow` 없음.

- [ ] **Step 3: enum + 매핑 추가**

`EffectSpec.cs`의 `StatusKindRef`에 **끝에** 추가(직렬화 호환):
```csharp
    public enum StatusKindRef { None, Stun, Vulnerable, Block, RewardNullified, Slow, Haste }
```
`CardSpecMapper.ToStatusKey`의 switch에 추가(`default` 앞):
```csharp
                case StatusKindRef.Slow: return StatusKeys.Slow;
                case StatusKindRef.Haste: return StatusKeys.Haste;
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~CardSpecMapperTests"`
Expected: PASS.

- [ ] **Step 5: 커밋**

```bash
git add Assets/FateWeaver/Simulation/Authoring/EffectSpec.cs Assets/FateWeaver/Simulation/Authoring/CardSpecMapper.cs Assets/FateWeaver/Tests/EditMode/CardSpecMapperTests.cs
git commit -m "feat(authoring): map Slow/Haste status refs"
```

---

## Task 7: 전달 카드(둔화/가속) + 엔드투엔드

**Files:**
- Modify: `Assets/FateWeaver/Simulation/Authoring/StarterDeckSpecs.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs`

- [ ] **Step 1: 실패하는 e2e 테스트 추가**

`SlowHasteStatusTests`에 추가(둔화 카드를 내고 해석하면 적이 둔화 상태를 얻고, 다음 턴 적 카드가 느려진다):
```csharp
        [Test]
        public void Playing_slow_card_slows_enemy_next_turn()
        {
            var slowCard = CardSpecMapper.ToDefinition(StarterDeckSpecs.SlowHex());
            var session = new DeckCombatSession(
                new[] { slowCard }, 100, new[] { new Enemy("goblin", 100) }, JabEachTurn(), 3, 5, 1);

            int hand = 0;
            for (int i = 0; i < session.Hand.Count; i++) if (session.Hand[i].Id == "slow_hex") hand = i;
            Assert.IsTrue(session.PlayActionCard(hand));
            session.ResolveTurn();
            Assert.IsTrue(session.State.Enemies[0].Statuses.Has(StatusKeys.Slow));
            session.BeginNextTurn();
            var jab = session.CurrentOrder.First(c => c.Def.Id == "e_jab");
            Assert.AreEqual(8, jab.Initiative); // base 5 + slow 3
        }
```
(상단 using에 `using FateWeaver.Simulation.Authoring;` 추가)

- [ ] **Step 2: 실패 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: 컴파일 실패 — `StarterDeckSpecs.SlowHex` 없음.

- [ ] **Step 3: 전달 카드 팩토리 추가**

`StarterDeckSpecs.cs`에 메서드 추가(`Build()`에는 넣지 않음 — 덱 구성 테스트 보존):
```csharp
        public static CardSpec SlowHex() => new CardSpec
        {
            Id = "slow_hex", Name = "둔화", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Action, Cost = 1, BaseInitiative = 3,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 3, Status = StatusKindRef.Slow,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.TargetEnemy } }
        };

        public static CardSpec QuickenSelf() => new CardSpec
        {
            Id = "quicken", Name = "가속", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Action, Cost = 1, BaseInitiative = 3,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, Amount = 3, Status = StatusKindRef.Haste,
                Lifetime = StatusLifetimeKind.Turns, LifetimeCount = 2, Target = StatusApplyTarget.Self } }
        };
```

- [ ] **Step 4: 통과 확인**

Run: `dotnet test ... --filter "FullyQualifiedName~SlowHasteStatusTests"`
Expected: PASS (8).

- [ ] **Step 5: 회귀 + 커밋**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo`
Expected: 전체 PASS.
```bash
git add Assets/FateWeaver/Simulation/Authoring/StarterDeckSpecs.cs Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs
git commit -m "feat(content): 둔화/가속 delivery card specs + e2e"
```

> 콘텐츠 배치(둔화/가속을 실제 DeckAsset/시작덱에, 고정을 특정 적 카드에)는 Unity SO 저작 단계 — 후속. 가속(Self) e2e는 same-turn이 아닌 *다음 턴 배치*부터 적용됨에 유의(Architecture 참고).

---

## Task 8: UI 한글 상태명 (Unity-레이어, 사용자 검증)

**Files:**
- Modify: `Assets/FateWeaver/Unity/PlaytestKoreanText.cs`

> 헤드리스 글롭 밖이라 dotnet 검증 불가 → Unity Play로 검증.

- [ ] **Step 1: 상태명 추가**

`PlaytestKoreanText.StatusName`에 추가(기존 분기 위):
```csharp
            if (key == StatusKeys.Slow) return "둔화";
            if (key == StatusKeys.Haste) return "가속";
```

- [ ] **Step 2: Unity 검증**

Unity Play: 적에게 둔화/가속이 걸리면 초상화 아래 상태 바에 `[둔화(3)]` / `[가속(3)]`이 표시되는지 확인.
Expected: 엔티티 상태가 초상화 아래에 표기. (카드 위 표기는 Task 9.)

- [ ] **Step 3: 커밋**

```bash
git add Assets/FateWeaver/Unity/PlaytestKoreanText.cs
git commit -m "feat(unity): Korean names for 둔화/가속 status"
```

---

## Task 9: 카드 상태 띠 + 중앙 "고정" 텍스트 제거 (Unity-레이어, 사용자 검증)

> 카드 이미지 리뷰 반영. 카드 스코프 상태는 **아트 영역 하단의 가로 아이콘 띠**(이름 바로 위)에 표기한다. 현재 카드 중앙의 "고정" 전용 텍스트 요소를 제거하고, 잠금을 그 띠의 아이콘으로 표시한다. (둔화/가속은 엔티티 상태라 카드가 아닌 초상화 아래 — Task 8.) 헤드리스 검증 불가 → Unity Play로 검증.

**Files:**
- Modify: `Assets/FateWeaver/Unity/Resources/CardView.prefab` (Unity Inspector)
- Modify: `Assets/FateWeaver/Unity/CardView.cs`

- [ ] **Step 1: 프리팹 — 중앙 "고정" 텍스트 제거 + 하단 띠 추가**

CardView 프리팹에서:
1. 아트 영역 중앙에 있던 "고정" TMP 텍스트 요소를 **삭제**.
2. 아트 영역 하단(이름 바로 위)에 `HorizontalLayoutGroup`을 가진 빈 컨테이너 `CardStatusRow`를 추가(가운데 정렬, 작은 간격).
3. 잠금 아이콘용 작은 `Image`(또는 TMP 글리프) 1개를 `CardStatusRow` 자식으로 추가하고 기본 비활성. (아이콘 비주얼은 1차엔 단순 lock 글리프/스프라이트.)

- [ ] **Step 2: CardView.cs — 잠금을 띠 아이콘으로 표시**

`CardView`의 직렬화 필드에서 기존 중앙 텍스트용 `_lockBadge`를 **하단 띠의 잠금 아이콘**으로 재지정(필드명 유지 가능, 프리팹에서 새 아이콘 오브젝트로 연결). `Bind`의 잠금 처리 로직은 그대로 둔다:
```csharp
            if (_lockBadge != null)
            {
                _lockBadge.SetActive(data.IsLocked);
            }
```
즉 코드 변경은 거의 없고, **프리팹에서 잠금 표시 오브젝트의 위치/비주얼만 중앙 텍스트 → 하단 띠 아이콘으로 교체**한다. `CardStatusRow`는 향후 카드 스코프 상태(기절 등)가 늘면 아이콘을 추가할 컨테이너로 남긴다.

- [ ] **Step 3: Unity 검증**

Unity Play: Task 5의 `StartsLocked` 적 카드가 미래 영역에 뜰 때 **중앙 "고정" 텍스트가 사라지고** 아트 하단 띠에 잠금 아이콘이 표시되는지 확인. 일반 카드엔 띠가 비어 있음.
Expected: 잠금 = 아트 하단 띠 아이콘, 중앙 텍스트 없음.

- [ ] **Step 4: 커밋**

```bash
git add Assets/FateWeaver/Unity/CardView.cs Assets/FateWeaver/Unity/Resources/CardView.prefab
git commit -m "feat(unity): card-status icon row at art bottom; remove central 고정 text"
```

---

## 후속 (이 계획 밖)

- **기절 전달 카드 + 아트 전체 딤** — 카드를 표적하는 효과(카드 스코프 상태 부여) 머신리가 필요. 기절이 카드에 실제로 부여되면 `CardStatusRow`에 기절 아이콘 + 아트 딤(발동 안 함)을 함께 구현 → 별도 작업.
- **같은-턴 즉시 둔화/가속** — 운명 카드 전달 + 정렬 시점 live-fold.
- **적이 플레이어에 디버프** — `StatusApplyTarget.TargetPlayer` 프리미티브.
- **콘텐츠 배치 + 신규 적 컨셉(간수/억제자)** — 본 상태이상 위에 별도 스펙.

---

## Self-Review

**1. 스펙 커버리지**
- 스코프 모델: Task 2/3(엔티티 fold, 카드-스코프 무시) ✓
- 둔화/가속(엔티티, +N/−N, N턴): Task 2/4/7 ✓ (지속은 기존 `StatusLifetime.Turns` 재사용 — Task 4 테스트가 Turns(2) 사용)
- 기절: 기존 유지(변경 없음), 전달은 후속으로 명시 ✓
- 고정(IsLocked 재사용, innate): Task 5 ✓
- 엔진 변경(ModifyInitiative 훅 + 진입 시 bake): Task 1/4 ✓
- 타깃(둔화=TargetEnemy, 가속=Self, 기존 재사용): Task 7 ✓
- 피드백(엔티티=초상화 아래, 고정 배지): Task 8 ✓ (배지 행/딤은 후속 — 기절 전달이 없어 v1 불필요, 스펙의 후속 항목과 일치)
- 배제(취약 휴면, RewardNullified 보류): 손대지 않음 ✓

**2. 플레이스홀더 스캔:** 모든 단계에 실제 코드/명령/기대값 포함. TBD 없음.

**3. 타입 일관성:** `ModifyInitiative(int, StatusContext)` 시그니처가 Task1(정의)·Task2(구현)·Task3(호출)에서 동일. `StatusInitiative.InitiativeFor(int, StatusBag, StatusRegistry)`가 Task3(정의)·Task4(호출) 동일. `StartsLocked` init-prop이 Task5 정의·사용 일관. `SlowHex()`/`QuickenSelf()` 팩토리 Task7 정의·사용 일관.

**갭/주의:** 가속(Self) 전달이 same-turn이 아님(다음 턴 배치부터) — 동작 명시에 기재. 콘텐츠 덱 배치는 Unity 단계로 분리.
