# Hybrid Card Authoring (SO → generated C#) Implementation Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Author cards as ScriptableObjects in the Inspector while keeping the rules pure C#: a flat `CardSpec` (+ `CardSpecMapper`) bridges authored data to the core `CardDefinition`, and an editor menu generates pure-C# `CardSpec` literals so the headless sims read the same authored cards.

**Architecture:** Pure `EffectSpec`/`CardSpec` (enum-flattened) + `CardSpecMapper.ToDefinition` live in `Simulation` (headless-tested). Unity `CardAsset`/`DeckAsset` ScriptableObjects hold the same data + Sprite/description and expose `ToSpec()`. An editor `CardCodeGenerator` emits `Simulation/Generated/GeneratedCards.cs`. The hand-coded `StarterDeck` stays as the equivalence oracle.

**Tech Stack:** C# 9 (Unity 6), NUnit, headless `dotnet test`. Pure code + tests in `Assets/Core/Simulation` / `Tests/EditMode` (headless-compiled); SO + generator in `Assets/Unity` (+ `/Editor`, user-verified).

**Run tests:** `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo` (filter: `--filter "FullyQualifiedName~ClassName"`). Output may be Korean ("통과!" = passed).

**Verification split:** Tasks 1–2 are pure → headless-verified here. Tasks 3–5 are Unity/editor → the **user** compiles/runs in the editor (I cannot). The pure equivalence test (Task 2) is the safety net that the authored data maps to correct cards.

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `Assets/Core/Simulation/Authoring/EffectSpec.cs` | authoring enums + flat EffectSpec | Create |
| `Assets/Core/Simulation/Authoring/CardSpec.cs` | flat card data | Create |
| `Assets/Core/Simulation/Authoring/CardSpecMapper.cs` | CardSpec → core CardDefinition | Create |
| `Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs` | the 10 starter cards as specs | Create |
| `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs` | mapping unit tests | Create |
| `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs` | spec deck behaves like hand-coded | Create |
| `Assets/Unity/CardAsset.cs` | ScriptableObject card + ToSpec | Create |
| `Assets/Unity/DeckAsset.cs` | ScriptableObject deck + ToSpecs | Create |
| `Assets/Unity/Editor/CardCodeGenerator.cs` | generate C# + seed starter SO | Create |
| `Assets/Unity/Editor/FateWeaver.Unity.Editor.asmdef` | add Simulation ref | Modify |

---

## Task 1: Authoring data types + `CardSpecMapper`

**Files:**
- Create: `Assets/Core/Simulation/Authoring/EffectSpec.cs`
- Create: `Assets/Core/Simulation/Authoring/CardSpec.cs`
- Create: `Assets/Core/Simulation/Authoring/CardSpecMapper.cs`
- Test: `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/Core/Tests/EditMode/CardSpecMapperTests.cs`:

```csharp
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class CardSpecMapperTests
    {
        [Test]
        public void Maps_flat_damage_action()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new[] { new EffectSpec { Kind = EffectKind.Damage, EffectValue = 3 } }
            });

            Assert.AreEqual(CardCategory.Execution, def.Category);
            Assert.AreEqual(1, def.EnergyCost);
            Assert.AreEqual(1, def.Effects.Count);
            Assert.AreEqual(EffectKeys.Damage, def.Effects[0].Key);
            Assert.AreEqual(3, def.Effects[0].EffectValue);
            Assert.IsNull(def.Effects[0].Condition);
        }

        [Test]
        public void Maps_conditional_damage()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new[] { new EffectSpec {
                    Kind = EffectKind.Damage, EffectValue = 2,
                    Condition = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(2, e.EffectValue);
            Assert.AreEqual(8, e.SuccessEffectValue);
            Assert.IsInstanceOf<FirstToTrigger>(e.Condition);
        }

        [Test]
        public void Maps_conditional_apply_status()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
                Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
                Effects = new[] { new EffectSpec {
                    Kind = EffectKind.ApplyStatus, EffectValue = 2, Status = StatusKindRef.Block,
                    Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                    Condition = ConditionKind.NextIsEnemyAttack, SuccessEffectValue = 7 } }
            });

            var e = def.Effects[0];
            Assert.AreEqual(EffectKeys.ApplyStatus, e.Key);
            Assert.AreEqual(2, e.EffectValue);
            Assert.AreEqual(7, e.SuccessEffectValue);
            Assert.IsTrue(e.StatusKey.HasValue);
            Assert.AreEqual(StatusKeys.Block, e.StatusKey.Value);
            var adjacent = (AdjacentCardIs)e.Condition;
            Assert.AreEqual(AdjacentDirection.Next, adjacent.Direction);
            Assert.AreEqual(Side.Enemy, adjacent.Side);
        }

        [Test]
        public void Maps_intervention_card()
        {
            var def = CardSpecMapper.ToDefinition(new CardSpec
            {
                Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
                Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.ChangeExecutionOrder, InterventionEffectValue = -2
            });

            Assert.AreEqual(CardCategory.Intervention, def.Category);
            Assert.AreEqual(0, def.Effects.Count);
            Assert.AreEqual(InterventionActionKeys.ChangeExecutionOrder, def.InterventionAction.Key);
            Assert.AreEqual(1, def.InterventionAction.InterventionCost);
            Assert.AreEqual(-2, def.InterventionAction.EffectValue);
        }
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~CardSpecMapperTests"`
Expected: FAIL to compile — `EffectSpec` / `CardSpec` / `CardSpecMapper` do not exist.

- [ ] **Step 3: Create the authoring data types**

Create `Assets/Core/Simulation/Authoring/EffectSpec.cs`:

```csharp
using System;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    public enum EffectKind { Damage, ApplyStatus, GrantNextAttackBonus, NullifyNextReward }

    public enum ConditionKind { None, FirstToTrigger, WithinNth, BeforeNextEnemyAttack, PrevIsPlayerAttack, NextIsEnemyAttack }

    public enum StatusKindRef { None, Stun, Vulnerable, Block, RewardNullified }

    public enum InterventionKind { None, ChangeExecutionOrder, SwapExecutionOrder, Lock }

    /// <summary>Flat, Inspector- and codegen-friendly description of one effect. Mapped to core EffectData.</summary>
    [Serializable]
    public struct EffectSpec
    {
        public EffectKind Kind;
        public int EffectValue;
        public ConditionKind Condition;
        public int ConditionN;
        public int SuccessEffectValue;
        public StatusKindRef Status;
        public StatusLifetimeKind Lifetime;
        public int LifetimeCount;
        public StatusApplyTarget Target;
    }
}
```

Create `Assets/Core/Simulation/Authoring/CardSpec.cs`:

```csharp
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Flat, pure card data (the single source the headless sims read). Built from a CardAsset SO
    /// at edit time (code generation) and converted to a core CardDefinition by CardSpecMapper.</summary>
    public sealed class CardSpec
    {
        public string Id;
        public string Name;
        public Side Side;
        public CardType Type;
        public CardCategory Category;
        public int EnergyCost;
        public int BaseExecutionOrder;
        public EffectSpec[] Effects;
        public InterventionKind Intervention;
        public int InterventionEffectValue;
    }
}
```

- [ ] **Step 4: Create `CardSpecMapper`**

Create `Assets/Core/Simulation/Authoring/CardSpecMapper.cs`:

```csharp
using System;
using System.Linq;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>Pure mapping from authored CardSpec to the core CardDefinition. Single place that knows
    /// how the flat authoring enums correspond to core keys / condition records / status / intervention actions.</summary>
    public static class CardSpecMapper
    {
        public static CardDefinition ToDefinition(CardSpec spec)
        {
            if (spec.Category == CardCategory.Intervention)
            {
                return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.Type, 0, Array.Empty<EffectData>())
                {
                    EnergyCost = spec.EnergyCost,
                    Category = CardCategory.Intervention,
                    InterventionAction = new InterventionActionData(ToInterventionKey(spec.Intervention), spec.EnergyCost, spec.InterventionEffectValue)
                };
            }

            var effects = (spec.Effects ?? Array.Empty<EffectSpec>()).Select(ToEffectData).ToArray();
            return new CardDefinition(spec.Id, spec.Name, spec.Side, spec.Type, spec.BaseExecutionOrder, effects)
            {
                EnergyCost = spec.EnergyCost,
                Category = CardCategory.Execution
            };
        }

        public static EffectData ToEffectData(EffectSpec e)
        {
            var key = ToEffectKey(e.Kind);
            var hasCondition = e.Condition != ConditionKind.None;

            if (e.Kind == EffectKind.ApplyStatus)
            {
                return new EffectData(key, e.EffectValue)
                {
                    StatusKey = ToStatusKey(e.Status),
                    StatusLifetime = ToLifetime(e.Lifetime, e.LifetimeCount),
                    StatusTarget = e.Target,
                    Condition = hasCondition ? ToCondition(e) : null,
                    SuccessEffectValue = hasCondition ? e.SuccessEffectValue : (int?)null
                };
            }

            return hasCondition
                ? EffectData.Conditional(key, e.EffectValue, ToCondition(e), e.SuccessEffectValue)
                : new EffectData(key, e.EffectValue);
        }

        private static EffectKey ToEffectKey(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.ApplyStatus: return EffectKeys.ApplyStatus;
                case EffectKind.GrantNextAttackBonus: return EffectKeys.GrantNextPlayerAttackDamageBonus;
                case EffectKind.NullifyNextReward: return EffectKeys.NullifyNextPlayerConditionReward;
                default: return EffectKeys.Damage;
            }
        }

        private static Condition ToCondition(EffectSpec e)
        {
            switch (e.Condition)
            {
                case ConditionKind.FirstToTrigger: return new FirstToTrigger();
                case ConditionKind.WithinNth: return new WithinNth(e.ConditionN);
                case ConditionKind.BeforeNextEnemyAttack: return new BeforeNextEnemyAttack();
                case ConditionKind.PrevIsPlayerAttack:
                    return new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, CardType.Attack);
                case ConditionKind.NextIsEnemyAttack:
                    return new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack);
                default: return null;
            }
        }

        private static StatusKey ToStatusKey(StatusKindRef s)
        {
            switch (s)
            {
                case StatusKindRef.Stun: return StatusKeys.Stun;
                case StatusKindRef.Vulnerable: return StatusKeys.Vulnerable;
                case StatusKindRef.RewardNullified: return StatusKeys.RewardNullified;
                default: return StatusKeys.Block;
            }
        }

        private static StatusLifetime ToLifetime(StatusLifetimeKind kind, int count)
        {
            switch (kind)
            {
                case StatusLifetimeKind.Permanent: return StatusLifetime.Permanent;
                case StatusLifetimeKind.Turns: return StatusLifetime.Turns(count);
                case StatusLifetimeKind.UntilConsumed: return StatusLifetime.UntilConsumed(count);
                default: return StatusLifetime.ThisTurn;
            }
        }

        private static InterventionActionKey ToInterventionKey(InterventionKind f)
        {
            switch (f)
            {
                case InterventionKind.SwapExecutionOrder: return InterventionActionKeys.SwapExecutionOrder;
                case InterventionKind.Lock: return InterventionActionKeys.Lock;
                default: return InterventionActionKeys.ChangeExecutionOrder;
            }
        }
    }
}
```

- [ ] **Step 5: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~CardSpecMapperTests"`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add Assets/Core/Simulation/Authoring/EffectSpec.cs Assets/Core/Simulation/Authoring/CardSpec.cs Assets/Core/Simulation/Authoring/CardSpecMapper.cs Assets/Core/Tests/EditMode/CardSpecMapperTests.cs
git commit -m "feat(sim): flat CardSpec/EffectSpec + CardSpecMapper to core CardDefinition"
```

---

## Task 2: `StarterDeckSpecs` + equivalence safety net

**Files:**
- Create: `Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs`
- Test: `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;
using FateWeaver.Simulation.Authoring;

namespace FateWeaver.Tests
{
    public class StarterDeckSpecEquivalenceTests
    {
        private static CardDefinition Def(string id) =>
            CardSpecMapper.ToDefinition(StarterDeckSpecs.Build().First(s => s.Id == id));

        private static EnemyIntent Goblin(int executionOrder, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", executionOrder, damage) }
            });

        private static int HandIndex(DeckCombatSession s, string id)
        {
            for (int i = 0; i < s.Hand.Count; i++) if (s.Hand[i].Id == id) return i;
            return -1;
        }

        private static int ZoneIndex(DeckCombatSession s, string id)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++) if (order[i].Def.Id == id) return i;
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> t, string id)
            => t.OfType<CardResolved>().First(e => e.CardId == id).DamageDealt;

        [Test]
        public void Spec_deck_has_same_composition()
        {
            var specs = StarterDeckSpecs.Build();
            Assert.AreEqual(10, specs.Count);
            Assert.AreEqual(7, specs.Count(s => s.Category == CardCategory.Execution));
            Assert.AreEqual(3, specs.Count(s => s.Category == CardCategory.Intervention));
        }

        [Test]
        public void Spec_quick_cut_pulled_first_deals_eight()
        {
            var session = new DeckCombatSession(
                new[] { Def("quick_cut"), Def("pull_forward") }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(4, 3), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "quick_cut"));
            session.PlayInterventionCard(HandIndex(session, "pull_forward"), ZoneIndex(session, "quick_cut"));
            Assert.AreEqual(8, DamageOf(session.ResolveTurn(), "quick_cut"));
        }

        [Test]
        public void Spec_heavy_strike_after_ally_attack_deals_ten()
        {
            var session = new DeckCombatSession(
                new[] { Def("slash"), Def("heavy_strike") }, 30,
                new[] { new Enemy("goblin", 100) },
                new EnemyIntent(new List<IReadOnlyList<CardDefinition>>()), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "slash"));
            session.PlayExecutionCard(HandIndex(session, "heavy_strike"));
            Assert.AreEqual(10, DamageOf(session.ResolveTurn(), "heavy_strike"));
        }

        [Test]
        public void Spec_cover_before_enemy_attack_absorbs()
        {
            var session = new DeckCombatSession(
                new[] { Def("cover") }, 30,
                new[] { new Enemy("goblin", 100) }, Goblin(6, 3), 3, 5, 1);
            session.PlayExecutionCard(HandIndex(session, "cover"));
            int hp = session.State.PlayerHp;
            session.ResolveTurn();
            Assert.AreEqual(hp, session.State.PlayerHp);
        }
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~StarterDeckSpecEquivalenceTests"`
Expected: FAIL to compile — `StarterDeckSpecs` does not exist.

- [ ] **Step 3: Create `StarterDeckSpecs`**

Create `Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Authoring
{
    /// <summary>The 10-card starter deck expressed as flat CardSpecs (mirrors the hand-coded StarterDeck,
    /// which stays as the equivalence oracle). The SO/codegen path produces specs of this shape.</summary>
    public static class StarterDeckSpecs
    {
        public static IReadOnlyList<CardSpec> Build() => new List<CardSpec>
        {
            Slash(), Slash(), Guard(), Guard(), QuickCut(), HeavyStrike(), Cover(),
            PullForward(), PullForward(), SwapPositions()
        };

        public static CardSpec Slash() => new CardSpec
        {
            Id = "slash", Name = "베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec { Kind = EffectKind.Damage, EffectValue = 3 } }
        };

        public static CardSpec Guard() => new CardSpec
        {
            Id = "guard", Name = "막기", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 4, Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self } }
        };

        public static CardSpec QuickCut() => new CardSpec
        {
            Id = "quick_cut", Name = "찰나의 베기", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.Damage, EffectValue = 2, Condition = ConditionKind.FirstToTrigger, SuccessEffectValue = 8 } }
        };

        public static CardSpec HeavyStrike() => new CardSpec
        {
            Id = "heavy_strike", Name = "강타", Side = Side.Player, Type = CardType.Attack,
            Category = CardCategory.Execution, EnergyCost = 2, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.Damage, EffectValue = 5, Condition = ConditionKind.PrevIsPlayerAttack, SuccessEffectValue = 10 } }
        };

        public static CardSpec Cover() => new CardSpec
        {
            Id = "cover", Name = "엄호", Side = Side.Player, Type = CardType.Defense,
            Category = CardCategory.Execution, EnergyCost = 1, BaseExecutionOrder = 5,
            Effects = new[] { new EffectSpec {
                Kind = EffectKind.ApplyStatus, EffectValue = 2, Status = StatusKindRef.Block,
                Lifetime = StatusLifetimeKind.ThisTurn, Target = StatusApplyTarget.Self,
                Condition = ConditionKind.NextIsEnemyAttack, SuccessEffectValue = 7 } }
        };

        public static CardSpec PullForward() => new CardSpec
        {
            Id = "pull_forward", Name = "앞당김", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.ChangeExecutionOrder, InterventionEffectValue = -2
        };

        public static CardSpec SwapPositions() => new CardSpec
        {
            Id = "swap_positions", Name = "자리 교환", Side = Side.Player, Type = CardType.Skill,
            Category = CardCategory.Intervention, EnergyCost = 1, Intervention = InterventionKind.SwapExecutionOrder, InterventionEffectValue = 0
        };
    }
}
```

- [ ] **Step 4: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~StarterDeckSpecEquivalenceTests"`
Expected: PASS (4 tests) — the spec-built cards behave identically to the hand-coded `StarterDeck`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Core/Simulation/Authoring/StarterDeckSpecs.cs Assets/Core/Tests/EditMode/StarterDeckSpecEquivalenceTests.cs
git commit -m "feat(sim): StarterDeckSpecs + behavioral equivalence to hand-coded deck"
```

---

## Task 3: `CardAsset` + `DeckAsset` ScriptableObjects (Unity)

**Files:**
- Create: `Assets/Unity/CardAsset.cs`
- Create: `Assets/Unity/DeckAsset.cs`

> Unity-only; the **user** confirms it compiles. No headless test (ScriptableObject + Sprite are UnityEngine).

- [ ] **Step 1: Create `CardAsset`**

Create `Assets/Unity/CardAsset.cs`:

```csharp
using System;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Authoring;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Inspector-authored card. The single source of truth for card data; converts to a pure
    /// CardSpec for the rules layer. Art/Description are presentation-only (not part of CardSpec).</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Card", fileName = "Card")]
    public sealed class CardAsset : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Side Side;
        public CardType Type;
        public CardCategory Category;
        public int EnergyCost = 1;
        public int BaseExecutionOrder = 5;
        public Sprite Art;
        [TextArea] public string Description;
        public EffectSpec[] Effects = Array.Empty<EffectSpec>();
        public InterventionKind Intervention;
        public int InterventionEffectValue;

        public CardSpec ToSpec() => new CardSpec
        {
            Id = Id,
            Name = DisplayName,
            Side = Side,
            Type = Type,
            Category = Category,
            EnergyCost = EnergyCost,
            BaseExecutionOrder = BaseExecutionOrder,
            Effects = Effects,
            Intervention = Intervention,
            InterventionEffectValue = InterventionEffectValue
        };
    }
}
```

- [ ] **Step 2: Create `DeckAsset`**

Create `Assets/Unity/DeckAsset.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Simulation.Authoring;
using UnityEngine;

namespace FateWeaver.Unity
{
    /// <summary>Inspector-authored deck: a list of cards with counts. Expands to flat CardSpecs.</summary>
    [CreateAssetMenu(menuName = "Fate Weaver/Deck", fileName = "Deck")]
    public sealed class DeckAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public CardAsset Card;
            public int Count;
        }

        public string Id;
        public Entry[] Entries = Array.Empty<Entry>();

        public IReadOnlyList<CardSpec> ToSpecs()
        {
            var specs = new List<CardSpec>();
            foreach (var entry in Entries)
            {
                if (entry.Card == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    specs.Add(entry.Card.ToSpec());
                }
            }

            return specs;
        }
    }
}
```

- [ ] **Step 3: User verifies compile**

User: let Unity reload. Expected: compiles; `Fate Weaver/Card` and `Fate Weaver/Deck` appear under the Create asset menu. Report errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Unity/CardAsset.cs Assets/Unity/DeckAsset.cs
git commit -m "feat(unity): CardAsset/DeckAsset ScriptableObjects (author -> CardSpec)"
```

---

## Task 4: `CardCodeGenerator` editor menu (+ seed starter SO)

**Files:**
- Create: `Assets/Unity/Editor/CardCodeGenerator.cs`
- Modify: `Assets/Unity/Editor/FateWeaver.Unity.Editor.asmdef`

> Editor-only; the **user** runs the menus. The generated file is pure and compiles in both Unity and headless.

- [ ] **Step 1: Add the Simulation reference to the editor asmdef**

Replace the `"references"` array in `Assets/Unity/Editor/FateWeaver.Unity.Editor.asmdef` with:

```json
    "references": [
        "FateWeaver.Unity",
        "FateWeaver.Simulation",
        "Unity.TextMeshPro",
        "UnityEngine.UI",
        "Unity.InputSystem"
    ],
```

- [ ] **Step 2: Create the generator + seeder**

Create `Assets/Unity/Editor/CardCodeGenerator.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;
using FateWeaver.Simulation.Authoring;
using UnityEditor;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    /// <summary>Edit-time bridge: (1) seeds the starter cards as CardAsset/DeckAsset from the pure
    /// StarterDeckSpecs, and (2) generates pure-C# CardSpec literals so the headless sims read the same
    /// authored cards. The generated file lives under Simulation (compiled by Unity AND headless).</summary>
    public static class CardCodeGenerator
    {
        private const string CardFolder = "Assets/Unity/Cards";
        private const string DeckAssetPath = CardFolder + "/StarterDeck.asset";
        private const string GeneratedPath = "Assets/Core/Simulation/Generated/GeneratedCards.cs";

        [MenuItem("Fate Weaver/Seed Starter Card Assets")]
        public static void SeedStarter()
        {
            Directory.CreateDirectory(CardFolder);
            var deck = ScriptableObject.CreateInstance<DeckAsset>();
            deck.Id = "starter";
            var entries = new List<DeckAsset.Entry>();

            foreach (var spec in DistinctById(StarterDeckSpecs.Build(), out var counts))
            {
                var card = ScriptableObject.CreateInstance<CardAsset>();
                Apply(card, spec);
                var path = CardFolder + "/" + spec.Id + ".asset";
                AssetDatabase.CreateAsset(card, path);
                entries.Add(new DeckAsset.Entry { Card = card, Count = counts[spec.Id] });
            }

            deck.Entries = entries.ToArray();
            AssetDatabase.CreateAsset(deck, DeckAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Seeded starter CardAssets + DeckAsset under " + CardFolder);
        }

        [MenuItem("Fate Weaver/Generate Cards from SO")]
        public static void Generate()
        {
            var deck = AssetDatabase.LoadAssetAtPath<DeckAsset>(DeckAssetPath);
            if (deck == null)
            {
                Debug.LogError("No DeckAsset at " + DeckAssetPath + " — run 'Fate Weaver/Seed Starter Card Assets' first.");
                return;
            }

            Directory.CreateDirectory("Assets/Core/Simulation/Generated");
            File.WriteAllText(GeneratedPath, Emit(deck.ToSpecs()), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("Generated " + GeneratedPath);
        }

        private static void Apply(CardAsset card, CardSpec spec)
        {
            card.Id = spec.Id;
            card.DisplayName = spec.Name;
            card.Side = spec.Side;
            card.Type = spec.Type;
            card.Category = spec.Category;
            card.EnergyCost = spec.EnergyCost;
            card.BaseExecutionOrder = spec.BaseExecutionOrder;
            card.Effects = spec.Effects ?? System.Array.Empty<EffectSpec>();
            card.Intervention = spec.Intervention;
            card.InterventionEffectValue = spec.InterventionEffectValue;
        }

        private static IEnumerable<CardSpec> DistinctById(IReadOnlyList<CardSpec> specs, out Dictionary<string, int> counts)
        {
            counts = new Dictionary<string, int>();
            var order = new List<CardSpec>();
            foreach (var spec in specs)
            {
                if (!counts.ContainsKey(spec.Id))
                {
                    counts[spec.Id] = 0;
                    order.Add(spec);
                }

                counts[spec.Id]++;
            }

            return order;
        }

        private static string Emit(IReadOnlyList<CardSpec> specs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED by Fate Weaver/Generate Cards from SO. Do not edit by hand.");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using FateWeaver.Core.Cards;");
            sb.AppendLine("using FateWeaver.Core.Effects;");
            sb.AppendLine("using FateWeaver.Core.Status;");
            sb.AppendLine("using FateWeaver.Simulation.Authoring;");
            sb.AppendLine();
            sb.AppendLine("namespace FateWeaver.Simulation.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class GeneratedCards");
            sb.AppendLine("    {");
            sb.AppendLine("        public static IReadOnlyList<CardSpec> StarterDeck() => new List<CardSpec>");
            sb.AppendLine("        {");
            foreach (var spec in specs)
            {
                sb.Append("            ").AppendLine(EmitSpec(spec) + ",");
            }
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EmitSpec(CardSpec s)
        {
            var sb = new StringBuilder();
            sb.Append("new CardSpec { ");
            sb.Append("Id = ").Append(Quote(s.Id)).Append(", ");
            sb.Append("Name = ").Append(Quote(s.Name)).Append(", ");
            sb.Append("Side = Side.").Append(s.Side).Append(", ");
            sb.Append("Type = CardType.").Append(s.Type).Append(", ");
            sb.Append("Category = CardCategory.").Append(s.Category).Append(", ");
            sb.Append("EnergyCost = ").Append(s.EnergyCost).Append(", ");
            sb.Append("BaseExecutionOrder = ").Append(s.BaseExecutionOrder).Append(", ");
            sb.Append("Intervention = InterventionKind.").Append(s.Intervention).Append(", ");
            sb.Append("InterventionEffectValue = ").Append(s.InterventionEffectValue).Append(", ");
            sb.Append("Effects = new EffectSpec[] { ");
            var effects = s.Effects ?? System.Array.Empty<EffectSpec>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(EmitEffect(effects[i]));
            }
            sb.Append(" } }");
            return sb.ToString();
        }

        private static string EmitEffect(EffectSpec e)
        {
            var sb = new StringBuilder();
            sb.Append("new EffectSpec { ");
            sb.Append("Kind = EffectKind.").Append(e.Kind).Append(", ");
            sb.Append("EffectValue = ").Append(e.EffectValue).Append(", ");
            sb.Append("Condition = ConditionKind.").Append(e.Condition).Append(", ");
            sb.Append("ConditionN = ").Append(e.ConditionN).Append(", ");
            sb.Append("SuccessEffectValue = ").Append(e.SuccessEffectValue).Append(", ");
            sb.Append("Status = StatusKindRef.").Append(e.Status).Append(", ");
            sb.Append("Lifetime = StatusLifetimeKind.").Append(e.Lifetime).Append(", ");
            sb.Append("LifetimeCount = ").Append(e.LifetimeCount).Append(", ");
            sb.Append("Target = StatusApplyTarget.").Append(e.Target).Append(" }");
            return sb.ToString();
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
```

- [ ] **Step 3: User verifies compile**

User: let Unity reload. Expected: editor compiles; menus `Fate Weaver/Seed Starter Card Assets` and `Fate Weaver/Generate Cards from SO` exist. Report errors (e.g., if `FateWeaver.Simulation` assembly name differs in the editor asmdef).

- [ ] **Step 4: Commit**

```bash
git add Assets/Unity/Editor/CardCodeGenerator.cs Assets/Unity/Editor/FateWeaver.Unity.Editor.asmdef
git commit -m "feat(unity): editor seeder + CardSpec code generator"
```

---

## Task 5: Author + generate + verify (user, in editor)

**Files:** generated `Assets/Core/Simulation/Generated/GeneratedCards.cs` (+ seeded SO assets under `Assets/Unity/Cards/`)

- [ ] **Step 1: Seed the starter SO assets**

User: run `Fate Weaver ▸ Seed Starter Card Assets`. Expected: 7 `CardAsset` `.asset` files (slash, guard, quick_cut, heavy_strike, cover, pull_forward, swap_positions) + `StarterDeck.asset` appear under `Assets/Unity/Cards/`. Inspect a card (e.g., `cover`) — its `Effects[0]` shows Kind=ApplyStatus, EffectValue=2, Condition=NextIsEnemyAttack, SuccessEffectValue=7.

- [ ] **Step 2: Generate the pure cards**

User: run `Fate Weaver ▸ Generate Cards from SO`. Expected: `Assets/Core/Simulation/Generated/GeneratedCards.cs` is created and the project recompiles with no errors.

- [ ] **Step 3: Verify headless suite stays green**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo`
Expected: all PASS — the generated file compiles headlessly and nothing regressed. (The generated `GeneratedCards.StarterDeck()` mirrors `StarterDeckSpecs`, already proven equivalent in Task 2.)

- [ ] **Step 4: Commit**

```bash
git add Assets/Unity/Cards Assets/Core/Simulation/Generated
git commit -m "chore(content): seed starter card SO assets + generated CardSpecs"
```

---

## Self-review notes (for the implementer)

- **Spec coverage:** SO-source-of-truth + generated-C# export (Tasks 3–5), enum-flattened EffectSpec (Task 1), CardSpecMapper with the documented mapping rules (Task 1), StarterDeckSpecs + equivalence net (Task 2), CardAsset/DeckAsset (Task 3), generator + seeder (Task 4). Conditional ApplyStatus (엄호) verified by `Maps_conditional_apply_status` and the equivalence cover test.
- **Out of scope (Sub-project 2):** Phase 2 deck/hand UI + prefab-ization; consuming `GeneratedCards`/`DeckAsset` from the runtime controller (the controller rework happens with the deck UI).
- **Oracle retained:** hand-coded `StarterDeck` (Simulation) stays as the equivalence oracle; do not delete it in this sub-project.
- **Risk:** Task 4 generator is blind editor code — expect a fix iteration after the user runs it (string emission / enum names). The pure mapper (Tasks 1–2) is the verified safety net regardless.
```
