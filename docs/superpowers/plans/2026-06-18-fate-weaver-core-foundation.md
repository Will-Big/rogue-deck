# Fate Weaver Core Foundation (M0–M1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the pure-C# combat domain core so that a fixed future zone resolves in 주도력(initiative) order, applies damage, and emits a deterministic event timeline — all verified by headless EditMode tests.

**Architecture:** A Unity asmdef `FateWeaver.Core` with `noEngineReferences:true` (no UnityEngine) holds all rules. Effects are dispatched through a typed-key handler registry (`EffectKey` → `IEffectHandler`). The `TurnResolver` freezes the zone into ascending-initiative order and produces a `List<ResolutionEvent>` as its sole output. `FateWeaver.Tests.EditMode` drives it with NUnit.

**Tech Stack:** Unity 6 (6000.2.13f1), C# (pure, no engine refs in Core), Unity Test Framework (NUnit) EditMode tests.

**Spec:** [docs/superpowers/specs/2026-06-18-fate-weaver-core-design.md](../specs/2026-06-18-fate-weaver-core-design.md) — this plan implements §3 (module boundaries), §4.2–4.4 (types, effect registry), §6 (turn resolution), and the M0/M1 rows of §10. Deferred to later plans: conditions (§4.3/M2), fate cards & registry generalization (§4.4/M3), status system & ApplyDamage/Block (§5/M4), simulation harness (§7/M5), param-resolution/growth (§4.6), `FateWeaver.Simulation` asmdef.

---

## Running Tests

EditMode tests run through Unity Test Framework. Two ways:

- **Inner loop (fast):** Unity Editor → `Window ▸ General ▸ Test Runner` → `EditMode` tab → `Run All`. Or in Rider/VS, run the `FateWeaver.Tests.EditMode` assembly's tests.
- **Canonical CLI (reproducible):** close the Editor first, then:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.2.13f1/Editor/Unity.exe" \
  -batchmode -projectPath "C:/UnityProjects/Rogue-deck" \
  -runTests -testPlatform EditMode \
  -testResults "C:/UnityProjects/Rogue-deck/test-results.xml" \
  -logFile - -quit
```

(Adjust the editor path if your install differs. Exit code 0 = all passed; results in `test-results.xml`.)

Throughout this plan, **"Run the EditMode tests"** means one of the above.

---

## File Structure

```
Assets/FateWeaver/
  Core/
    FateWeaver.Core.asmdef            # noEngineReferences:true, no references
    Cards/
      Side.cs                         # enum Side { Player, Enemy }
      CardType.cs                     # enum CardType { Attack, Skill, Defense }
      CardDefinition.cs               # immutable card template + EffectData
    Effects/
      EffectKey.cs                    # readonly record struct EffectKey + EffectKeys catalog
      IEffectHandler.cs               # handler interface + EffectContext
      EffectRegistry.cs               # EffectKey -> IEffectHandler
      DamageHandler.cs                # the one M1 handler
    Combat/
      ActionCardInstance.cs           # runtime card in the zone
      Enemy.cs                        # enemy entity
      CombatState.cs                  # player hp, enemies, zone, fate energy
      FutureZone.cs                   # ordered cards + ResolutionOrder()
      TurnResolver.cs                 # the resolution loop
    Events/
      ResolutionEvent.cs              # TurnStarted / CardResolved / TurnEnded / Outcome
  Tests/
    EditMode/
      FateWeaver.Tests.EditMode.asmdef
      SmokeTests.cs
      FutureZoneTests.cs
      DamageHandlerTests.cs
      TurnResolverTests.cs
```

All Core types live in **one assembly**, so sub-namespace cross-references (e.g., `Effects` ↔ `Combat`) are fine.

---

## Task M0.1: Core assembly + folders + base enums

**Files:**
- Create: `Assets/FateWeaver/Core/FateWeaver.Core.asmdef`
- Create: `Assets/FateWeaver/Core/Cards/Side.cs`
- Create: `Assets/FateWeaver/Core/Cards/CardType.cs`

- [ ] **Step 1: Create the Core asmdef**

`Assets/FateWeaver/Core/FateWeaver.Core.asmdef`:

```json
{
    "name": "FateWeaver.Core",
    "rootNamespace": "FateWeaver.Core",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

- [ ] **Step 2: Create the base enums**

`Assets/FateWeaver/Core/Cards/Side.cs`:

```csharp
namespace FateWeaver.Core.Cards
{
    public enum Side
    {
        Player,
        Enemy
    }
}
```

`Assets/FateWeaver/Core/Cards/CardType.cs`:

```csharp
namespace FateWeaver.Core.Cards
{
    public enum CardType
    {
        Attack,
        Skill,
        Defense
    }
}
```

- [ ] **Step 3: Let Unity compile**

In the Unity Editor, let the domain reload finish. Expected: no compile errors in the Console; a `FateWeaver.Core` assembly appears.

- [ ] **Step 4: Commit** (skip if the project is not yet a git repo — see "Note on git" at the end)

```bash
git add Assets/FateWeaver/Core
git commit -m "feat(core): add FateWeaver.Core assembly (noEngineReferences) and base enums"
```

---

## Task M0.2: EditMode test assembly + smoke test (green pipeline)

**Files:**
- Create: `Assets/FateWeaver/Tests/EditMode/FateWeaver.Tests.EditMode.asmdef`
- Test: `Assets/FateWeaver/Tests/EditMode/SmokeTests.cs`

- [ ] **Step 1: Create the test asmdef**

`Assets/FateWeaver/Tests/EditMode/FateWeaver.Tests.EditMode.asmdef`:

```json
{
    "name": "FateWeaver.Tests.EditMode",
    "rootNamespace": "FateWeaver.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "FateWeaver.Core"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Write the smoke test**

`Assets/FateWeaver/Tests/EditMode/SmokeTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Core.Cards;

namespace FateWeaver.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Enums_are_referenceable_from_tests()
        {
            Assert.AreEqual(Side.Player, Side.Player);
            Assert.AreNotEqual(CardType.Attack, CardType.Defense);
        }
    }
}
```

- [ ] **Step 3: Run the EditMode tests**

Expected: `SmokeTests.Enums_are_referenceable_from_tests` PASSES. This proves the test pipeline and the Core reference work.

- [ ] **Step 4: Commit**

```bash
git add Assets/FateWeaver/Tests
git commit -m "test: add EditMode test assembly and green smoke test"
```

---

## Task M0.3: Verify the noEngineReferences compile guard (manual)

This proves the architectural boundary is enforced by the build, not just convention.

- [ ] **Step 1: Temporarily add a UnityEngine reference inside Core**

Add this line at the top of `Assets/FateWeaver/Core/Cards/Side.cs`:

```csharp
using UnityEngine; // TEMP - must fail to compile
```

- [ ] **Step 2: Let Unity recompile and confirm it FAILS**

Expected: Console shows a compile error like `The type or namespace name 'UnityEngine' could not be found` for `FateWeaver.Core`. This is the desired result — Core cannot see UnityEngine.

- [ ] **Step 3: Remove the temporary line**

Delete the `using UnityEngine;` line. Confirm the Console is clean again and the smoke test still passes (run the EditMode tests).

- [ ] **Step 4: No commit needed** (no net file change). Record the result in the task tracker instead.

---

## Task M1.1: Foundational data types

Plain data/record types with no behavior. Verified by a construction smoke test.

**Files:**
- Create: `Assets/FateWeaver/Core/Effects/EffectKey.cs`
- Create: `Assets/FateWeaver/Core/Cards/CardDefinition.cs`
- Create: `Assets/FateWeaver/Core/Combat/ActionCardInstance.cs`
- Create: `Assets/FateWeaver/Core/Combat/Enemy.cs`
- Create: `Assets/FateWeaver/Core/Events/ResolutionEvent.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SmokeTests.cs` (add a test)

- [ ] **Step 1: EffectKey + catalog**

`Assets/FateWeaver/Core/Effects/EffectKey.cs`:

```csharp
namespace FateWeaver.Core.Effects
{
    /// <summary>Typed wrapper over a string id (open set, type-safe). See spec §4.5.</summary>
    public readonly record struct EffectKey(string Id)
    {
        public override string ToString() => Id;
    }

    public static class EffectKeys
    {
        public static readonly EffectKey Damage = new("damage");
    }
}
```

- [ ] **Step 2: CardDefinition + EffectData**

`Assets/FateWeaver/Core/Cards/CardDefinition.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Effects;

namespace FateWeaver.Core.Cards
{
    /// <summary>One effect entry on a card: which handler + its scalar amount (M1).</summary>
    public sealed record EffectData(EffectKey Key, int Amount);

    /// <summary>Immutable card template. See spec §4.1 (Definition layer).</summary>
    public sealed record CardDefinition(
        string Id,
        string Name,
        Side Side,
        CardType Type,
        int BaseInitiative,
        IReadOnlyList<EffectData> Effects);
}
```

- [ ] **Step 3: ActionCardInstance**

`Assets/FateWeaver/Core/Combat/ActionCardInstance.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>A card placed in the future zone for one combat. Initiative is mutable (fate cards change it later).</summary>
    public sealed class ActionCardInstance
    {
        public CardDefinition Def { get; }
        public int Initiative { get; set; }

        public ActionCardInstance(CardDefinition def)
        {
            Def = def;
            Initiative = def.BaseInitiative;
        }
    }
}
```

- [ ] **Step 4: Enemy**

`Assets/FateWeaver/Core/Combat/Enemy.cs`:

```csharp
namespace FateWeaver.Core.Combat
{
    public sealed class Enemy
    {
        public string Id { get; }
        public int Hp { get; set; }

        public Enemy(string id, int hp)
        {
            Id = id;
            Hp = hp;
        }
    }
}
```

> `CombatState` depends on `FutureZone` (Task M1.2), so it is created later, in Task M1.3.

- [ ] **Step 5: ResolutionEvent timeline types**

`Assets/FateWeaver/Core/Events/ResolutionEvent.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Events
{
    public enum Outcome { Ongoing, Win, Lose }

    /// <summary>The sole output of resolution. UI replays it; tests assert on it. See spec §2, §6.</summary>
    public abstract record ResolutionEvent;

    public sealed record TurnStarted(int TurnIndex) : ResolutionEvent;

    public sealed record CardResolved(
        string CardId,
        Side Side,
        int DamageDealt,
        string TargetId) : ResolutionEvent;

    public sealed record TurnEnded(int TurnIndex, Outcome Outcome) : ResolutionEvent;
}
```

- [ ] **Step 6: Add a construction smoke test**

Append to `Assets/FateWeaver/Tests/EditMode/SmokeTests.cs` (inside the `SmokeTests` class):

```csharp
        [Test]
        public void Can_build_a_card_and_enemy()
        {
            var def = new FateWeaver.Core.Cards.CardDefinition(
                "strike", "Strike", Side.Player, CardType.Attack, 2,
                new[] { new FateWeaver.Core.Cards.EffectData(FateWeaver.Core.Effects.EffectKeys.Damage, 5) });

            var card = new FateWeaver.Core.Combat.ActionCardInstance(def);
            var enemy = new FateWeaver.Core.Combat.Enemy("goblin", 12);

            Assert.AreEqual("strike", def.Id);
            Assert.AreEqual(2, card.Initiative);
            Assert.AreEqual(12, enemy.Hp);
        }
```

- [ ] **Step 7: Run the EditMode tests**

Expected: `Can_build_a_card_and_enemy` PASSES.

- [ ] **Step 8: Commit**

```bash
git add Assets/FateWeaver/Core Assets/FateWeaver/Tests/EditMode/SmokeTests.cs
git commit -m "feat(core): add card, enemy, effect-key, and event data types"
```

---

## Task M1.2: FutureZone with stable ascending ResolutionOrder

The zone resolves cards by ascending initiative (lower = earlier — spec §4.1), ties broken by insertion order (stable).

**Files:**
- Create: `Assets/FateWeaver/Core/Combat/FutureZone.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/FutureZoneTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/FateWeaver/Tests/EditMode/FutureZoneTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class FutureZoneTests
    {
        private static ActionCardInstance Card(string id, int initiative)
        {
            var def = new CardDefinition(id, id, Side.Player, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, 1) });
            return new ActionCardInstance(def);
        }

        [Test]
        public void ResolutionOrder_is_ascending_and_stable_on_ties()
        {
            var zone = new FutureZone();
            zone.Add(Card("A", 3));
            zone.Add(Card("B", 1));
            zone.Add(Card("C", 1)); // tie with B; inserted after B

            var order = zone.ResolutionOrder().Select(c => c.Def.Id).ToArray();

            // ascending initiative; B before C because of stable tie-break
            CollectionAssert.AreEqual(new[] { "B", "C", "A" }, order);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the EditMode tests. Expected: compile error / FAIL — `FutureZone` does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

`Assets/FateWeaver/Core/Combat/FutureZone.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace FateWeaver.Core.Combat
{
    /// <summary>Ordered set of action cards for one turn. See spec §4.1, §4.7.</summary>
    public sealed class FutureZone
    {
        private readonly List<ActionCardInstance> _cards = new();

        public IReadOnlyList<ActionCardInstance> Cards => _cards;

        public void Add(ActionCardInstance card) => _cards.Add(card);

        /// <summary>Ascending initiative, stable on ties (LINQ OrderBy is a stable sort).</summary>
        public IReadOnlyList<ActionCardInstance> ResolutionOrder()
            => _cards.OrderBy(c => c.Initiative).ToList();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run the EditMode tests. Expected: `ResolutionOrder_is_ascending_and_stable_on_ties` PASSES, and all prior tests still pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Core/Combat/FutureZone.cs Assets/FateWeaver/Tests/EditMode/FutureZoneTests.cs
git commit -m "feat(core): add FutureZone with stable ascending resolution order"
```

---

## Task M1.3: Effect handler registry + DamageHandler

Effects are dispatched by `EffectKey` to an `IEffectHandler` (spec §4.4). M1 ships the one handler we need: damage. The handler writes its outcome into the context; the resolver (M1.4) reads it.

**Files:**
- Create: `Assets/FateWeaver/Core/Combat/CombatState.cs` (depends on `FutureZone` from M1.2)
- Create: `Assets/FateWeaver/Core/Effects/IEffectHandler.cs`
- Create: `Assets/FateWeaver/Core/Effects/EffectRegistry.cs`
- Create: `Assets/FateWeaver/Core/Effects/DamageHandler.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/DamageHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/FateWeaver/Tests/EditMode/DamageHandlerTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class DamageHandlerTests
    {
        private static ActionCardInstance Card(Side side, int amount)
        {
            var def = new CardDefinition("c", "c", side, CardType.Attack, 1,
                new[] { new EffectData(EffectKeys.Damage, amount) });
            return new ActionCardInstance(def);
        }

        [Test]
        public void Player_damage_hits_first_enemy()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            var ctx = new EffectContext { Card = Card(Side.Player, 5), State = state, Amount = 5 };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual(7, state.Enemies[0].Hp);
            Assert.AreEqual(5, ctx.DamageDealt);
            Assert.AreEqual("goblin", ctx.TargetId);
        }

        [Test]
        public void Enemy_damage_hits_player()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            var ctx = new EffectContext { Card = Card(Side.Enemy, 4), State = state, Amount = 4 };

            new DamageHandler().Apply(ctx);

            Assert.AreEqual(26, state.PlayerHp);
            Assert.AreEqual(4, ctx.DamageDealt);
            Assert.AreEqual("player", ctx.TargetId);
        }

        [Test]
        public void Registry_resolves_handler_by_key_and_throws_on_unknown()
        {
            var registry = new EffectRegistry();
            var handler = new DamageHandler();
            registry.Register(handler);

            Assert.AreSame(handler, registry.Resolve(EffectKeys.Damage));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => registry.Resolve(new EffectKey("nope")));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the EditMode tests. Expected: compile error / FAIL — `CombatState`, `IEffectHandler`, `EffectContext`, `EffectRegistry`, `DamageHandler` don't exist.

- [ ] **Step 3: Write the implementations**

`Assets/FateWeaver/Core/Combat/CombatState.cs` (depends on `FutureZone` from M1.2):

```csharp
using System.Collections.Generic;

namespace FateWeaver.Core.Combat
{
    /// <summary>Mutable combat state. FateEnergyPerTurn is a variable (NOT fixed 3) — see spec §8.</summary>
    public sealed class CombatState
    {
        public int PlayerHp { get; set; }
        public List<Enemy> Enemies { get; } = new();
        public FutureZone Zone { get; } = new();
        public int FateEnergy { get; set; }
        public int FateEnergyPerTurn { get; set; }
        public int RngSeed { get; set; }
    }
}
```

`Assets/FateWeaver/Core/Effects/IEffectHandler.cs`:

```csharp
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>Per-effect inputs/outputs. Handler mutates State and writes its outcome here.</summary>
    public sealed class EffectContext
    {
        public ActionCardInstance Card;
        public CombatState State;
        public int Amount;

        // outputs (read by TurnResolver)
        public int DamageDealt;
        public string TargetId;
    }

    public interface IEffectHandler
    {
        EffectKey Key { get; }
        void Apply(EffectContext ctx);
    }
}
```

`Assets/FateWeaver/Core/Effects/EffectRegistry.cs`:

```csharp
using System.Collections.Generic;

namespace FateWeaver.Core.Effects
{
    public sealed class EffectRegistry
    {
        private readonly Dictionary<EffectKey, IEffectHandler> _handlers = new();

        public void Register(IEffectHandler handler) => _handlers[handler.Key] = handler;

        public IEffectHandler Resolve(EffectKey key)
            => _handlers.TryGetValue(key, out var h)
                ? h
                : throw new KeyNotFoundException($"No effect handler registered for '{key}'");
    }
}
```

`Assets/FateWeaver/Core/Effects/DamageHandler.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Effects
{
    /// <summary>M1 damage: player cards hit the first enemy, enemy cards hit the player.
    /// Block/vulnerable folding arrives with the ApplyDamage pipeline in M4 (spec §5).</summary>
    public sealed class DamageHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.Def.Side == Side.Player)
            {
                var target = ctx.State.Enemies[0];
                target.Hp -= ctx.Amount;
                ctx.DamageDealt = ctx.Amount;
                ctx.TargetId = target.Id;
            }
            else
            {
                ctx.State.PlayerHp -= ctx.Amount;
                ctx.DamageDealt = ctx.Amount;
                ctx.TargetId = "player";
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the EditMode tests. Expected: all three `DamageHandlerTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Core/Combat/CombatState.cs Assets/FateWeaver/Core/Effects Assets/FateWeaver/Tests/EditMode/DamageHandlerTests.cs
git commit -m "feat(core): add combat state, effect handler registry, and DamageHandler"
```

---

## Task M1.4: TurnResolver — resolve zone in order, emit timeline

The resolver freezes the zone order, runs each card's effects through the registry, emits one `CardResolved` per card (aggregating damage), and brackets the turn with `TurnStarted`/`TurnEnded` (with win/lose outcome). Spec §6.

**Files:**
- Create: `Assets/FateWeaver/Core/Combat/TurnResolver.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/TurnResolverTests.cs`

- [ ] **Step 1: Write the failing test**

`Assets/FateWeaver/Tests/EditMode/TurnResolverTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Tests
{
    public class TurnResolverTests
    {
        private static EffectRegistry Registry()
        {
            var r = new EffectRegistry();
            r.Register(new DamageHandler());
            return r;
        }

        private static ActionCardInstance Card(string id, Side side, int initiative, int damage)
        {
            var def = new CardDefinition(id, id, side, CardType.Attack, initiative,
                new[] { new EffectData(EffectKeys.Damage, damage) });
            return new ActionCardInstance(def);
        }

        [Test]
        public void Resolves_in_initiative_order_and_emits_timeline()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 12));
            // player card has higher initiative (2) than enemy card (1) => enemy resolves first
            state.Zone.Add(Card("strike", Side.Player, 2, 5));
            state.Zone.Add(Card("jab", Side.Enemy, 1, 3));

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            // hp effects applied
            Assert.AreEqual(27, state.PlayerHp);     // took 3
            Assert.AreEqual(7, state.Enemies[0].Hp); // took 5

            // timeline shape: TurnStarted, CardResolved(jab), CardResolved(strike), TurnEnded
            Assert.IsInstanceOf<TurnStarted>(events[0]);
            var first = (CardResolved)events[1];
            var second = (CardResolved)events[2];
            Assert.AreEqual("jab", first.CardId);    // enemy first (lower initiative)
            Assert.AreEqual("strike", second.CardId);
            Assert.AreEqual(5, second.DamageDealt);
            Assert.IsInstanceOf<TurnEnded>(events[^1]);
            Assert.AreEqual(Outcome.Ongoing, ((TurnEnded)events[^1]).Outcome);
        }

        [Test]
        public void Reports_win_when_all_enemies_dead()
        {
            var state = new CombatState { PlayerHp = 30 };
            state.Enemies.Add(new Enemy("goblin", 4));
            state.Zone.Add(Card("strike", Side.Player, 1, 5));

            var events = new TurnResolver(Registry()).Resolve(state, turnIndex: 0);

            Assert.AreEqual(Outcome.Win, ((TurnEnded)events[^1]).Outcome);
        }

        [Test]
        public void Resolution_is_deterministic()
        {
            CombatState Build()
            {
                var s = new CombatState { PlayerHp = 30 };
                s.Enemies.Add(new Enemy("goblin", 12));
                s.Zone.Add(Card("strike", Side.Player, 2, 5));
                s.Zone.Add(Card("jab", Side.Enemy, 1, 3));
                return s;
            }

            var a = new TurnResolver(Registry()).Resolve(Build(), 0);
            var b = new TurnResolver(Registry()).Resolve(Build(), 0);

            CollectionAssert.AreEqual(
                a.Select(e => e.ToString()).ToArray(),
                b.Select(e => e.ToString()).ToArray());
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the EditMode tests. Expected: compile error / FAIL — `TurnResolver` does not exist.

- [ ] **Step 3: Write the implementation**

`Assets/FateWeaver/Core/Combat/TurnResolver.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Events;

namespace FateWeaver.Core.Combat
{
    /// <summary>Freezes the zone order at resolution, runs each card's effects, emits the event timeline.
    /// See spec §6. Conditions, fate manipulation, and status folding arrive in later milestones.</summary>
    public sealed class TurnResolver
    {
        private readonly EffectRegistry _effects;

        public TurnResolver(EffectRegistry effects) => _effects = effects;

        public List<ResolutionEvent> Resolve(CombatState state, int turnIndex)
        {
            var events = new List<ResolutionEvent> { new TurnStarted(turnIndex) };

            foreach (var card in state.Zone.ResolutionOrder())
            {
                int totalDamage = 0;
                string targetId = null;

                foreach (var effect in card.Def.Effects)
                {
                    var ctx = new EffectContext
                    {
                        Card = card,
                        State = state,
                        Amount = effect.Amount
                    };
                    _effects.Resolve(effect.Key).Apply(ctx);
                    totalDamage += ctx.DamageDealt;
                    if (ctx.TargetId != null) targetId = ctx.TargetId;
                }

                events.Add(new CardResolved(card.Def.Id, card.Def.Side, totalDamage, targetId));
            }

            events.Add(new TurnEnded(turnIndex, ComputeOutcome(state)));
            return events;
        }

        private static Outcome ComputeOutcome(CombatState state)
        {
            if (state.PlayerHp <= 0) return Outcome.Lose;
            if (state.Enemies.All(e => e.Hp <= 0)) return Outcome.Win;
            return Outcome.Ongoing;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the EditMode tests. Expected: all `TurnResolverTests` PASS, and every prior test still passes.

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Core/Combat/TurnResolver.cs Assets/FateWeaver/Tests/EditMode/TurnResolverTests.cs
git commit -m "feat(core): add TurnResolver with ordered resolution and event timeline"
```

---

## Done criteria for M0–M1

- [ ] `FateWeaver.Core` compiles with `noEngineReferences:true` and the compile guard (M0.3) was confirmed.
- [ ] All EditMode tests pass: `SmokeTests`, `FutureZoneTests`, `DamageHandlerTests`, `TurnResolverTests`.
- [ ] A fixed future zone resolves in ascending-initiative order, applies damage, and produces a deterministic `TurnStarted → CardResolved* → TurnEnded` timeline.

## Note on git

The project is currently **not** a git repository. Either initialize one before starting (`git init` at `C:/UnityProjects/Rogue-deck`, add a Unity `.gitignore`), or skip the `git commit` steps and rely on the task checkboxes for progress. Recommended: initialize git first so the frequent-commit discipline holds.

## Next plans (written just-in-time)

- **M2 — Conditions:** `Condition` records + `ConditionEvaluator` + 실패/기본/성공 tiers (spec §4.3).
- **M3 — Fate cards:** `FateActionKey`/registry generalization, fate energy economy, manipulation phase (spec §4.4, §8).
- **M4 — Status system:** `IStatusHolder`/`IStatusBehavior`, ApplyDamage pipeline (block/vulnerable), disruption (spec §5).
- **M5 — Harness:** `FateWeaver.Simulation` asmdef, ScenarioRunner + Compare, report mode, doc ch.11 cards + ch.8 scenarios (spec §7, §9).
- **M6 — Extension-seam docs** (spec §11).
