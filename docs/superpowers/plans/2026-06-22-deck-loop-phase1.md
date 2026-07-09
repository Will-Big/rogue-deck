# Deck Core Loop — Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the pure-C# deck combat loop — a single deck of execution+intervention cards drawn into a hand, fate-energy as a per-card cost, execution cards placed onto the per-turn future zone and intervention cards reordering it, resolved each turn — all headless-tested. Unity is untouched (Phase 2).

**Architecture:** Reuse the existing `FutureZone` / `TurnResolver` / conditions / effects / statuses / `InterventionPlayResolver` unchanged. Add a `Deck` (draw/discard/hand + seeded shuffle), extend `CardDefinition` with `EnergyCost`/`Category`/`InterventionAction`, define the 10-card starter deck and a deterministic enemy-intent script, and a `DeckCombatSession` driver that runs the turn loop. The conditional block on 엄호 already works through the existing `ResolveEffectValue`→`SuccessEffectValue` path, so **no Core effect code changes**.

**Tech Stack:** C# 9 (Unity 6 constraint), NUnit, headless `dotnet test`. New code lives in `Assets/FateWeaver/Core` (pure) and `Assets/FateWeaver/Simulation` (uses the `internal` `CombatRegistries`); tests in `Assets/FateWeaver/Tests/EditMode`. All three are compiled by the headless project.

**Run tests:** `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo`
Filter one class: append `--filter "FullyQualifiedName~ClassName"`. Output may be Korean ("통과!" = passed).

---

## File Structure

| File | Responsibility | Action |
|---|---|---|
| `Assets/FateWeaver/Core/Cards/CardCategory.cs` | Execution vs Intervention enum | Create |
| `Assets/FateWeaver/Core/Cards/CardDefinition.cs` | add `EnergyCost`/`Category`/`InterventionAction` | Modify |
| `Assets/FateWeaver/Core/Combat/Deck.cs` | draw/discard/hand piles + seeded shuffle/reshuffle | Create |
| `Assets/FateWeaver/Simulation/StarterDeck.cs` | 10-card starter deck + enemy-attack helper | Create |
| `Assets/FateWeaver/Simulation/EnemyIntent.cs` | per-turn enemy execution cards (deterministic) | Create |
| `Assets/FateWeaver/Simulation/DeckCombatSession.cs` | the turn-loop driver | Create |
| `Assets/FateWeaver/Tests/EditMode/CardDefinitionDataTests.cs` | card data carries energy cost/category/intervention | Create |
| `Assets/FateWeaver/Tests/EditMode/DeckTests.cs` | draw + reshuffle | Create |
| `Assets/FateWeaver/Tests/EditMode/StarterDeckTests.cs` | composition (10 cards, 7:3, costs) | Create |
| `Assets/FateWeaver/Tests/EditMode/EnemyIntentTests.cs` | ForTurn clamps | Create |
| `Assets/FateWeaver/Tests/EditMode/DeckCombatSessionTests.cs` | loop + balance invariants | Create |

---

## Task 1: `CardCategory` + extend `CardDefinition`

**Files:**
- Create: `Assets/FateWeaver/Core/Cards/CardCategory.cs`
- Modify: `Assets/FateWeaver/Core/Cards/CardDefinition.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/CardDefinitionDataTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/FateWeaver/Tests/EditMode/CardDefinitionDataTests.cs`:

```csharp
using System;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Tests
{
    public class CardDefinitionDataTests
    {
        [Test]
        public void Action_card_defaults_to_action_category()
        {
            var card = new CardDefinition(
                "slash", "베기", Side.Player, CardType.Attack, 5,
                new[] { new EffectData(EffectKeys.Damage, 3) }) { EnergyCost = 1 };

            Assert.AreEqual(CardCategory.Execution, card.Category);
            Assert.AreEqual(1, card.EnergyCost);
            Assert.IsNull(card.InterventionAction);
        }

        [Test]
        public void Intervention_card_carries_an_intervention_action()
        {
            var action = new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2);
            var card = new CardDefinition(
                "pull", "앞당김", Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
                { EnergyCost = 1, Category = CardCategory.Intervention, InterventionAction = action };

            Assert.AreEqual(CardCategory.Intervention, card.Category);
            Assert.AreSame(action, card.InterventionAction);
        }
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~CardDefinitionDataTests"`
Expected: FAIL to compile — `CardCategory` / `EnergyCost` / `Category` / `InterventionAction` do not exist.

- [ ] **Step 3: Create the enum**

Create `Assets/FateWeaver/Core/Cards/CardCategory.cs`:

```csharp
namespace FateWeaver.Core.Cards
{
    /// <summary>Whether a card produces effects on the future zone (Execution) or manipulates it (Intervention).</summary>
    public enum CardCategory
    {
        Execution,
        Intervention
    }
}
```

- [ ] **Step 4: Extend `CardDefinition`**

In `Assets/FateWeaver/Core/Cards/CardDefinition.cs`, replace the `CardDefinition` record declaration (the final record at the bottom of the file) with a bodied record. Add `using FateWeaver.Core.Intervention;` to the top of the file (next to the existing usings):

```csharp
    /// <summary>Immutable card template.</summary>
    public sealed record CardDefinition(
        string Id,
        string Name,
        Side Side,
        CardType Type,
        int BaseExecutionOrder,
        IReadOnlyList<EffectData> Effects)
    {
        /// <summary>Fate-energy cost to play this card.</summary>
        public int EnergyCost { get; init; }

        /// <summary>Execution (effects on the zone) or Intervention (zone control).</summary>
        public CardCategory Category { get; init; }

        /// <summary>For intervention cards: the action resolved when played (null for execution cards).</summary>
        public InterventionActionData InterventionAction { get; init; }
    }
```

- [ ] **Step 5: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~CardDefinitionDataTests"`
Expected: PASS (2 tests). (If the existing suite still compiles, positional construction elsewhere is unaffected — the new members are optional `init`.)

- [ ] **Step 6: Commit**

```bash
git add Assets/FateWeaver/Core/Cards/CardCategory.cs Assets/FateWeaver/Core/Cards/CardDefinition.cs Assets/FateWeaver/Tests/EditMode/CardDefinitionDataTests.cs
git commit -m "feat(core): card energy-cost/category/intervention-action on CardDefinition"
```

---

## Task 2: `Deck` (draw / discard / shuffle / reshuffle)

**Files:**
- Create: `Assets/FateWeaver/Core/Combat/Deck.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/DeckTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/FateWeaver/Tests/EditMode/DeckTests.cs`:

```csharp
using System;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Effects;

namespace FateWeaver.Tests
{
    public class DeckTests
    {
        private static CardDefinition Card(string id) => new CardDefinition(
            id, id, Side.Player, CardType.Attack, 5,
            new[] { new EffectData(EffectKeys.Damage, 1) }) { EnergyCost = 1 };

        [Test]
        public void Draw_moves_cards_from_draw_pile_to_hand()
        {
            var deck = new Deck(new[] { Card("a"), Card("b"), Card("c") }, seed: 1);
            Assert.AreEqual(3, deck.DrawCount);
            Assert.AreEqual(0, deck.HandCount);

            deck.Draw(2);

            Assert.AreEqual(2, deck.HandCount);
            Assert.AreEqual(1, deck.DrawCount);
        }

        [Test]
        public void Draw_reshuffles_discard_when_draw_pile_empty()
        {
            var deck = new Deck(new[] { Card("a"), Card("b"), Card("c") }, seed: 1);
            deck.Draw(3);          // hand 3, draw 0
            deck.DiscardHand();    // discard 3, hand 0
            Assert.AreEqual(0, deck.DrawCount);
            Assert.AreEqual(3, deck.DiscardCount);

            deck.Draw(2);          // must reshuffle the discard pile back in

            Assert.AreEqual(2, deck.HandCount);
            Assert.AreEqual(1, deck.DrawCount);
            Assert.AreEqual(0, deck.DiscardCount);
        }

        [Test]
        public void Draw_stops_when_no_cards_remain_anywhere()
        {
            var deck = new Deck(new[] { Card("a") }, seed: 1);
            deck.Draw(5); // only one card exists
            Assert.AreEqual(1, deck.HandCount);
        }
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~DeckTests"`
Expected: FAIL to compile — `Deck` does not exist.

- [ ] **Step 3: Create `Deck`**

Create `Assets/FateWeaver/Core/Combat/Deck.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Combat
{
    /// <summary>Draw pile / discard pile / hand for one combat, with a seeded shuffle.
    /// Pure C# (no UnityEngine) so the loop is headless-testable and deterministic.</summary>
    public sealed class Deck
    {
        private readonly List<CardDefinition> _draw = new List<CardDefinition>();
        private readonly List<CardDefinition> _discard = new List<CardDefinition>();
        private readonly List<CardDefinition> _hand = new List<CardDefinition>();
        private readonly Random _rng;

        public Deck(IEnumerable<CardDefinition> cards, int seed)
        {
            _rng = new Random(seed);
            foreach (var card in cards)
            {
                _draw.Add(card);
            }

            Shuffle(_draw);
        }

        public IReadOnlyList<CardDefinition> Hand => _hand;
        public int DrawCount => _draw.Count;
        public int DiscardCount => _discard.Count;
        public int HandCount => _hand.Count;

        public void Draw(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_draw.Count == 0)
                {
                    if (_discard.Count == 0)
                    {
                        return;
                    }

                    _draw.AddRange(_discard);
                    _discard.Clear();
                    Shuffle(_draw);
                }

                var top = _draw[_draw.Count - 1];
                _draw.RemoveAt(_draw.Count - 1);
                _hand.Add(top);
            }
        }

        public void DiscardFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count)
            {
                return;
            }

            _discard.Add(_hand[handIndex]);
            _hand.RemoveAt(handIndex);
        }

        public void DiscardHand()
        {
            _discard.AddRange(_hand);
            _hand.Clear();
        }

        private void Shuffle(List<CardDefinition> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
```

- [ ] **Step 4: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~DeckTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Core/Combat/Deck.cs Assets/FateWeaver/Tests/EditMode/DeckTests.cs
git commit -m "feat(core): seeded Deck with draw/discard/hand + reshuffle"
```

---

## Task 3: `StarterDeck` definition

**Files:**
- Create: `Assets/FateWeaver/Simulation/StarterDeck.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/StarterDeckTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/FateWeaver/Tests/EditMode/StarterDeckTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class StarterDeckTests
    {
        [Test]
        public void Has_ten_cards_seven_execution_three_intervention()
        {
            var cards = StarterDeck.Build();
            Assert.AreEqual(10, cards.Count);
            Assert.AreEqual(7, cards.Count(c => c.Category == CardCategory.Execution));
            Assert.AreEqual(3, cards.Count(c => c.Category == CardCategory.Intervention));
        }

        [Test]
        public void Contains_expected_counts_by_id()
        {
            var cards = StarterDeck.Build();
            Assert.AreEqual(2, cards.Count(c => c.Id == "slash"));
            Assert.AreEqual(2, cards.Count(c => c.Id == "guard"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "quick_cut"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "heavy_strike"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "cover"));
            Assert.AreEqual(2, cards.Count(c => c.Id == "pull_forward"));
            Assert.AreEqual(1, cards.Count(c => c.Id == "swap_positions"));
        }

        [Test]
        public void Intervention_card_cost_matches_its_intervention_action_cost()
        {
            var pull = StarterDeck.Build().First(c => c.Id == "pull_forward");
            Assert.AreEqual(CardCategory.Intervention, pull.Category);
            Assert.AreEqual(pull.EnergyCost, pull.InterventionAction.InterventionCost);
        }
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~StarterDeckTests"`
Expected: FAIL to compile — `StarterDeck` does not exist.

- [ ] **Step 3: Create `StarterDeck`**

Create `Assets/FateWeaver/Simulation/StarterDeck.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation
{
    /// <summary>The 10-card starter deck (7 execution : 3 intervention). Player execution cards share a base execution order
    /// so order among them is placement order; intervention cards + enemy execution order create the puzzle.</summary>
    public static class StarterDeck
    {
        public const int DefaultExecutionOrder = 5;

        public static IReadOnlyList<CardDefinition> Build()
        {
            var cards = new List<CardDefinition>();
            cards.Add(Slash());
            cards.Add(Slash());
            cards.Add(Guard());
            cards.Add(Guard());
            cards.Add(QuickCut());
            cards.Add(HeavyStrike());
            cards.Add(Cover());
            cards.Add(PullForward());
            cards.Add(PullForward());
            cards.Add(SwapPositions());
            return cards;
        }

        // --- execution cards ---------------------------------------------------

        public static CardDefinition Slash() => new CardDefinition(
            "slash", "베기", Side.Player, CardType.Attack, DefaultExecutionOrder,
            new[] { new EffectData(EffectKeys.Damage, 3) })
            { EnergyCost = 1, Category = CardCategory.Execution };

        public static CardDefinition Guard() => new CardDefinition(
            "guard", "막기", Side.Player, CardType.Defense, DefaultExecutionOrder,
            new[]
            {
                EffectData.ApplyStatus(StatusKeys.Block, StatusLifetime.ThisTurn, StatusApplyTarget.Self, 4)
            })
            { EnergyCost = 1, Category = CardCategory.Execution };

        public static CardDefinition QuickCut() => new CardDefinition(
            "quick_cut", "찰나의 베기", Side.Player, CardType.Attack, DefaultExecutionOrder,
            new[] { EffectData.Conditional(EffectKeys.Damage, 2, new FirstToTrigger(), 8) })
            { EnergyCost = 1, Category = CardCategory.Execution };

        public static CardDefinition HeavyStrike() => new CardDefinition(
            "heavy_strike", "강타", Side.Player, CardType.Attack, DefaultExecutionOrder,
            new[]
            {
                EffectData.Conditional(
                    EffectKeys.Damage, 5,
                    new AdjacentCardIs(AdjacentDirection.Previous, Side.Player, CardType.Attack), 10)
            })
            { EnergyCost = 2, Category = CardCategory.Execution };

        public static CardDefinition Cover() => new CardDefinition(
            "cover", "엄호", Side.Player, CardType.Defense, DefaultExecutionOrder,
            new[]
            {
                new EffectData(EffectKeys.ApplyStatus, 2)
                {
                    StatusKey = StatusKeys.Block,
                    StatusLifetime = StatusLifetime.ThisTurn,
                    StatusTarget = StatusApplyTarget.Self,
                    Condition = new AdjacentCardIs(AdjacentDirection.Next, Side.Enemy, CardType.Attack),
                    SuccessEffectValue = 7
                }
            })
            { EnergyCost = 1, Category = CardCategory.Execution };

        // --- intervention cards -----------------------------------------------------

        public static CardDefinition PullForward() => InterventionCard(
            "pull_forward", "앞당김", cost: 1,
            new InterventionActionData(InterventionActionKeys.ChangeExecutionOrder, interventionCost: 1, effectValue: -2));

        public static CardDefinition SwapPositions() => InterventionCard(
            "swap_positions", "자리 교환", cost: 1,
            new InterventionActionData(InterventionActionKeys.SwapExecutionOrder, interventionCost: 1, effectValue: 0));

        private static CardDefinition InterventionCard(string id, string name, int cost, InterventionActionData action) =>
            new CardDefinition(id, name, Side.Player, CardType.Skill, 0, Array.Empty<EffectData>())
            { EnergyCost = cost, Category = CardCategory.Intervention, InterventionAction = action };

        // --- helper for enemy intent ---------------------------------------

        public static CardDefinition EnemyAttack(string id, string name, int executionOrder, int damage) =>
            new CardDefinition(id, name, Side.Enemy, CardType.Attack, executionOrder,
                new[] { new EffectData(EffectKeys.Damage, damage) })
                { EnergyCost = 0, Category = CardCategory.Execution };
    }
}
```

- [ ] **Step 4: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~StarterDeckTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Simulation/StarterDeck.cs Assets/FateWeaver/Tests/EditMode/StarterDeckTests.cs
git commit -m "feat(sim): 10-card starter deck (베기/막기/찰나/강타/엄호/앞당김/교환)"
```

---

## Task 4: `EnemyIntent` (deterministic per-turn enemy cards)

**Files:**
- Create: `Assets/FateWeaver/Simulation/EnemyIntent.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/EnemyIntentTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Assets/FateWeaver/Tests/EditMode/EnemyIntentTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class EnemyIntentTests
    {
        [Test]
        public void For_turn_returns_that_turns_cards_and_clamps_past_the_end()
        {
            var t0 = new List<CardDefinition> { StarterDeck.EnemyAttack("jab_0", "고블린 찌르기", 4, 3) };
            var t1 = new List<CardDefinition> { StarterDeck.EnemyAttack("jab_1", "고블린 찌르기", 4, 5) };
            var intent = new EnemyIntent(new IReadOnlyList<CardDefinition>[] { t0, t1 });

            Assert.AreEqual("jab_0", intent.ForTurn(0)[0].Id);
            Assert.AreEqual("jab_1", intent.ForTurn(1)[0].Id);
            Assert.AreEqual("jab_1", intent.ForTurn(7)[0].Id); // clamps to the last defined turn
        }

        [Test]
        public void Empty_intent_returns_no_cards()
        {
            var intent = new EnemyIntent(new List<IReadOnlyList<CardDefinition>>());
            Assert.AreEqual(0, intent.ForTurn(0).Count);
        }
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~EnemyIntentTests"`
Expected: FAIL to compile — `EnemyIntent` does not exist.

- [ ] **Step 3: Create `EnemyIntent`**

Create `Assets/FateWeaver/Simulation/EnemyIntent.cs`:

```csharp
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation
{
    /// <summary>Deterministic enemy telegraph: the enemy execution cards placed on the future zone each turn.
    /// Turns past the end clamp to the last defined turn. (Real enemy AI is a later phase.)</summary>
    public sealed class EnemyIntent
    {
        private readonly IReadOnlyList<IReadOnlyList<CardDefinition>> _turns;

        public EnemyIntent(IReadOnlyList<IReadOnlyList<CardDefinition>> turns)
        {
            _turns = turns ?? Array.Empty<IReadOnlyList<CardDefinition>>();
        }

        public IReadOnlyList<CardDefinition> ForTurn(int turnIndex)
        {
            if (_turns.Count == 0)
            {
                return Array.Empty<CardDefinition>();
            }

            var index = turnIndex < 0 ? 0 : Math.Min(turnIndex, _turns.Count - 1);
            return _turns[index];
        }
    }
}
```

- [ ] **Step 4: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~EnemyIntentTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Simulation/EnemyIntent.cs Assets/FateWeaver/Tests/EditMode/EnemyIntentTests.cs
git commit -m "feat(sim): deterministic per-turn EnemyIntent"
```

---

## Task 5: `DeckCombatSession` (the turn loop)

**Files:**
- Create: `Assets/FateWeaver/Simulation/DeckCombatSession.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/DeckCombatSessionTests.cs`

> Energy: **intervention cards** deduct energy inside `InterventionPlayResolver` (the handler's `CanApply`/`Apply`).
> **Execution cards** deduct energy here. Both gate on `FateEnergy >= EnergyCost`.

- [ ] **Step 1: Write the failing test**

Create `Assets/FateWeaver/Tests/EditMode/DeckCombatSessionTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Simulation;

namespace FateWeaver.Tests
{
    public class DeckCombatSessionTests
    {
        private static EnemyIntent Goblin(int executionOrder, int damage) => new EnemyIntent(
            new IReadOnlyList<CardDefinition>[]
            {
                new[] { StarterDeck.EnemyAttack("goblin_jab", "고블린 찌르기", executionOrder, damage) }
            });

        private static int HandIndex(DeckCombatSession s, string id)
        {
            for (int i = 0; i < s.Hand.Count; i++)
            {
                if (s.Hand[i].Id == id) return i;
            }
            return -1;
        }

        private static int DamageOf(IReadOnlyList<ResolutionEvent> timeline, string cardId)
            => timeline.OfType<CardResolved>().First(e => e.CardId == cardId).DamageDealt;

        [Test]
        public void Playing_an_action_card_places_it_and_spends_energy()
        {
            var session = NewSession(new[] { StarterDeck.Slash() }, Goblin(4, 3));
            Assert.AreEqual(3, session.FateEnergy);

            Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "slash")));

            Assert.AreEqual(2, session.FateEnergy);                 // cost 1 spent
            Assert.IsTrue(session.CurrentOrder.Any(c => c.Def.Id == "slash"));
            Assert.AreEqual(0, session.Hand.Count(c => c.Id == "slash")); // moved to discard
        }

        [Test]
        public void Cannot_play_action_card_without_enough_energy()
        {
            // deck of two heavy strikes (cost 2 each); energy 3 -> only one is affordable.
            var session = NewSession(new[] { StarterDeck.HeavyStrike(), StarterDeck.HeavyStrike() }, Goblin(4, 3));
            Assert.IsTrue(session.PlayExecutionCard(HandIndex(session, "heavy_strike")));  // 3 -> 1
            Assert.IsFalse(session.PlayExecutionCard(HandIndex(session, "heavy_strike"))); // 1 < 2, rejected
            Assert.AreEqual(1, session.FateEnergy);
        }

        [Test]
        public void Quick_cut_pulled_to_the_front_lands_the_first_strike_bonus()
        {
            // Enemy at execution order 4 acts before the player's cards (base 5) by default.
            var session = NewSession(new[] { StarterDeck.QuickCut(), StarterDeck.PullForward() }, Goblin(4, 3));
            session.PlayExecutionCard(HandIndex(session, "quick_cut")); // placed at execution order 5

            // pull_forward (-2) on quick_cut -> execution order 3 -> now first.
            var quickIndex = ZoneIndex(session, "quick_cut");
            Assert.IsTrue(session.PlayInterventionCard(HandIndex(session, "pull_forward"), quickIndex));

            var timeline = session.ResolveTurn();
            Assert.AreEqual(8, DamageOf(timeline, "quick_cut")); // first-strike success
        }

        [Test]
        public void Heavy_strike_after_an_ally_attack_gets_the_combo_bonus()
        {
            var session = NewSession(new[] { StarterDeck.Slash(), StarterDeck.HeavyStrike() }, new EnemyIntent(
                new List<IReadOnlyList<CardDefinition>>())); // no enemy this turn
            session.PlayExecutionCard(HandIndex(session, "slash"));        // execution order 5 (placed first)
            session.PlayExecutionCard(HandIndex(session, "heavy_strike")); // execution order 5 (placed second -> after slash)

            var timeline = session.ResolveTurn();
            Assert.AreEqual(10, DamageOf(timeline, "heavy_strike")); // prev is a player attack -> +5
        }

        [Test]
        public void Cover_before_the_enemy_attack_absorbs_it()
        {
            // cover (base 5) resolves before goblin (6); its "next is enemy attack" bonus -> block 7.
            var session = NewSession(new[] { StarterDeck.Cover() }, Goblin(6, 3));
            session.PlayExecutionCard(HandIndex(session, "cover"));

            int hpBefore = session.State.PlayerHp;
            session.ResolveTurn();
            Assert.AreEqual(hpBefore, session.State.PlayerHp); // block 7 fully absorbs the 3 damage
        }

        [Test]
        public void Begin_next_turn_discards_hand_refills_energy_and_redraws()
        {
            var session = NewSession(StarterDeck.Build(), Goblin(4, 3));
            session.PlayExecutionCard(HandIndex(session, FirstActionId(session)));
            session.ResolveTurn();
            Assert.IsTrue(session.CurrentTurnResolved);

            Assert.IsTrue(session.BeginNextTurn());
            Assert.AreEqual(1, session.TurnIndex);
            Assert.AreEqual(3, session.FateEnergy);            // refilled
            Assert.IsFalse(session.CurrentTurnResolved);
            Assert.AreEqual(5, session.Hand.Count);            // fresh hand of 5
        }

        // --- helpers ---

        private static DeckCombatSession NewSession(
            IReadOnlyList<CardDefinition> deck, EnemyIntent intent)
            => new DeckCombatSession(
                deck, playerHp: 30,
                enemies: new[] { new Enemy("goblin", 100) },
                intent: intent, fateEnergyPerTurn: 3, handSize: 5, seed: 1);

        private static int ZoneIndex(DeckCombatSession s, string cardId)
        {
            var order = s.CurrentOrder;
            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].Def.Id == cardId) return i;
            }
            return -1;
        }

        private static string FirstActionId(DeckCombatSession s)
            => s.Hand.First(c => c.Category == CardCategory.Execution).Id;
    }
}
```

- [ ] **Step 2: Run it; verify it fails**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~DeckCombatSessionTests"`
Expected: FAIL to compile — `DeckCombatSession` does not exist.

- [ ] **Step 3: Create `DeckCombatSession`**

Create `Assets/FateWeaver/Simulation/DeckCombatSession.cs`:

```csharp
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation
{
    /// <summary>Drives the deck turn loop: draw a hand, spend fate energy to place execution cards onto the
    /// future zone and play intervention cards to reorder it, resolve, then begin the next turn. Pure C#.</summary>
    public sealed class DeckCombatSession
    {
        private readonly CombatState _state;
        private readonly Deck _deck;
        private readonly EnemyIntent _intent;
        private readonly TurnResolver _resolver;
        private readonly InterventionPlayResolver _interventionResolver;
        private readonly int _handSize;
        private IReadOnlyList<ResolutionEvent> _lastTimeline;

        public DeckCombatSession(
            IReadOnlyList<CardDefinition> deckCards,
            int playerHp,
            IReadOnlyList<Enemy> enemies,
            EnemyIntent intent,
            int fateEnergyPerTurn = 3,
            int handSize = 5,
            int seed = 0)
        {
            _state = new CombatState
            {
                PlayerHp = playerHp,
                FateEnergyPerTurn = fateEnergyPerTurn,
                RngSeed = seed
            };
            foreach (var enemy in enemies)
            {
                _state.Enemies.Add(enemy);
            }

            _deck = new Deck(deckCards, seed);
            _intent = intent;
            _handSize = handSize;
            _resolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses());
            _interventionResolver = new InterventionPlayResolver(CombatRegistries.InterventionActions());

            BeginTurn(0);
        }

        public int TurnIndex { get; private set; }
        public IReadOnlyList<CardDefinition> Hand => _deck.Hand;
        public int FateEnergy => _state.FateEnergy;
        public CombatState State => _state;
        public IReadOnlyList<ExecutionCardInstance> CurrentOrder => _state.Zone.ResolutionOrder();
        public IReadOnlyList<ResolutionEvent> LastTimeline => _lastTimeline;
        public Outcome Outcome { get; private set; } = Outcome.Ongoing;
        public bool CurrentTurnResolved { get; private set; }
        public bool IsComplete => Outcome != Outcome.Ongoing;
        public int DrawCount => _deck.DrawCount;
        public int DiscardCount => _deck.DiscardCount;

        /// <summary>Place an execution card from the hand onto the future zone (spends its fate-energy cost).</summary>
        public bool PlayExecutionCard(int handIndex)
        {
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var def = _deck.Hand[handIndex];
            if (def.Category != CardCategory.Execution || _state.FateEnergy < def.EnergyCost)
            {
                return false;
            }

            _state.FateEnergy -= def.EnergyCost;
            _state.Zone.Add(new ExecutionCardInstance(def));
            _deck.DiscardFromHand(handIndex);
            return true;
        }

        /// <summary>Play an intervention card from the hand, targeting card(s) by their index in CurrentOrder.
        /// The intervention handler deducts energy and rejects when locked / unaffordable.</summary>
        public bool PlayInterventionCard(int handIndex, int targetZoneIndex, int secondaryZoneIndex = -1)
        {
            if (CurrentTurnResolved || handIndex < 0 || handIndex >= _deck.Hand.Count)
            {
                return false;
            }

            var def = _deck.Hand[handIndex];
            if (def.Category != CardCategory.Intervention || def.InterventionAction == null)
            {
                return false;
            }

            var order = _state.Zone.ResolutionOrder();
            if (targetZoneIndex < 0 || targetZoneIndex >= order.Count)
            {
                return false;
            }

            var target = order[targetZoneIndex];
            ExecutionCardInstance secondary = null;
            if (secondaryZoneIndex >= 0)
            {
                if (secondaryZoneIndex >= order.Count)
                {
                    return false;
                }

                secondary = order[secondaryZoneIndex];
            }

            var result = _interventionResolver.Resolve(_state, new[] { new InterventionPlay(def.InterventionAction, target, secondary) });
            if (result.AppliedCount != 1)
            {
                return false;
            }

            _deck.DiscardFromHand(handIndex);
            return true;
        }

        public IReadOnlyList<ResolutionEvent> ResolveTurn()
        {
            if (CurrentTurnResolved)
            {
                return _lastTimeline;
            }

            _lastTimeline = _resolver.Resolve(_state, TurnIndex);
            CurrentTurnResolved = true;
            Outcome = OutcomeOf(_lastTimeline);
            return _lastTimeline;
        }

        /// <summary>Discard the leftover hand and start the next turn (enemy intent, energy refill, redraw).
        /// Returns false when the current turn is unresolved or combat is already decided.</summary>
        public bool BeginNextTurn()
        {
            if (!CurrentTurnResolved || IsComplete)
            {
                return false;
            }

            _deck.DiscardHand();
            BeginTurn(TurnIndex + 1);
            return true;
        }

        private void BeginTurn(int index)
        {
            TurnIndex = index;
            CurrentTurnResolved = false;
            _lastTimeline = null;

            _state.Zone.Clear();
            foreach (var enemyCard in _intent.ForTurn(index))
            {
                _state.Zone.Add(new ExecutionCardInstance(enemyCard));
            }

            _state.FateEnergy = _state.FateEnergyPerTurn;
            _deck.Draw(_handSize);
        }

        private static Outcome OutcomeOf(IReadOnlyList<ResolutionEvent> timeline)
        {
            for (int i = timeline.Count - 1; i >= 0; i--)
            {
                if (timeline[i] is TurnEnded ended)
                {
                    return ended.Outcome;
                }
            }

            return Outcome.Ongoing;
        }
    }
}
```

- [ ] **Step 4: Run it; verify it passes**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo --filter "FullyQualifiedName~DeckCombatSessionTests"`
Expected: PASS (6 tests). If `Heavy_strike...` fails because both cards share execution order 5 but sort order differs, confirm `FutureZone.ResolutionOrder` is a stable sort (it is — LINQ `OrderBy`), so placement order (slash then heavy_strike) holds.

- [ ] **Step 5: Commit**

```bash
git add Assets/FateWeaver/Simulation/DeckCombatSession.cs Assets/FateWeaver/Tests/EditMode/DeckCombatSessionTests.cs
git commit -m "feat(sim): DeckCombatSession turn loop (draw/play/resolve/next) + invariants"
```

---

## Task 6: Full-suite regression

**Files:** none (verification only)

- [ ] **Step 1: Run the entire headless suite**

Run: `dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo`
Expected: all tests PASS — the new deck-loop tests plus every pre-existing test (the `CardDefinition` change is backward compatible; no Core behavior changed).

- [ ] **Step 2: If anything failed, fix before proceeding**

If a pre-existing test broke, it is almost certainly a `CardDefinition` record-equality or construction issue — inspect and fix inline, re-run. Do not proceed until green.

- [ ] **Step 3: Commit (only if a fix was needed)**

```bash
git add -A
git commit -m "test: keep full headless suite green after deck-loop addition"
```

---

## Self-review notes (for the implementer)

- **Spec coverage:** single deck + per-card cost (Task 1/3), Deck draw/discard/reshuffle (Task 2), role split enforced by data (execution cards = effects, intervention cards = `InterventionAction`; Task 3), per-turn zone reset + loop (Task 5), starter deck (Task 3), enemy intent (Task 4), deterministic RNG/timeline + invariants (Task 5), conditional block on 엄호 (works via existing pipeline — verified by `Cover_before_the_enemy_attack_absorbs_it`). No Core effect code was needed.
- **Out of scope (later phases):** Unity deck/hand UI (Phase 2); reward card pool + new conditions `LastToTrigger`/`TargetHasStatus` (Phase 3); multi-enemy precise targeting (still `Enemies[0]` approximation).
- **Deferred existing modules:** scenario-scripted runners (`MultiTurnRunner`/`ScenarioRunner`) and their tests stay as a balance-regression tool; they are not modified here.
