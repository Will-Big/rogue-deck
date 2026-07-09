# Warden Lock Enemy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Warden enemy as the first lock-tutorial enemy. The Warden draws from a deterministic shuffle bag, locks exactly one of its telegraphed cards each turn, and uses order-relative conditional cards so the player learns that locked cards cannot be moved or execution order-modified.

**Scope:** Pure Core/Simulation/headless work only. Unity CardAssets, Warden art, playtest encounter selection, and player-side lock mechanics are deferred.

**Tech Stack:** C# 9 (Unity 6 constraint: no file-scoped namespaces), NUnit, headless `.NET` test harness.

---

## Background / Reference

Spec: [`docs/superpowers/specs/2026-06-27-warden-lock-enemy-design.md`](../specs/2026-06-27-warden-lock-enemy-design.md).

Current seams to use:
- `IEnemyTurnPolicy` (`Assets/FateWeaver/Simulation/Enemies/IEnemyTurnPolicy.cs`) is the enemy card selection seam.
- `RandomMovesetPolicy` is deterministic per `(seed, turnIndex)` but is not a no-replacement deck cycle; Warden needs a new stateful `ShuffleBagPolicy`.
- `CardDefinition.StartsLocked` already exists; `DeckCombatSession.BeginTurn` bakes it into `ExecutionCardInstance.IsLocked`.
- Intervention lock/reorder rejection already exists for locked cards. This plan adds the missing execution order-fold immunity.
- `DescriptionComposer` + `KoreanDescriptionVocabulary` already generate descriptions from `EffectData`; Warden only needs the new condition vocabulary.

Decision for this slice:
- Add `ConditionKind.NoFollowingEnemyCard` for authoring, mapping to `new NoFollowingCardOfSide(Side.Enemy)`.
- Update `NoPrecedingCardOfSide` wording from "앞에 ..." to "이전 수행한 ...".
- Add `NoFollowingCardOfSide` wording as "이후 수행한 ...".
- Do **not** change `AdjacentCardIs` wording in this slice; "바로 앞/뒤" stays as-is.

Main verification command:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
```

---

## Milestone Checklist

- [ ] **M0: Conditions and wording** — `NoFollowingCardOfSide` evaluates correctly, authoring can map it, and Korean descriptions render the new/updated order-relative wording.
- [ ] **M1: Enemy policies** — `ShuffleBagPolicy` and `SelfLockPolicy` pass deterministic headless tests.
- [ ] **M2: Lock immunity** — locked enemy cards skip slow/haste execution order folding in `DeckCombatSession`.
- [ ] **M3: Warden deck contract** — Warden card ids, HP, effects, policy composition, and simple combat integration pass headless tests.
- [ ] **M4: Full headless regression** — all headless tests pass.

---

## Task 1: Add `NoFollowingCardOfSide`

**Files:**
- Modify: `Assets/FateWeaver/Core/Conditions/Condition.cs`
- Modify: `Assets/FateWeaver/Core/Conditions/ConditionEvaluator.cs`
- Modify: `Assets/FateWeaver/Simulation/Authoring/EffectSpec.cs`
- Modify: `Assets/FateWeaver/Simulation/Authoring/CardSpecMapper.cs`
- Modify: `Assets/FateWeaver/Simulation/Descriptions/KoreanDescriptionVocabulary.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/ConditionEvaluatorTests.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/CardSpecMapperTests.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/DescriptionComposerTests.cs`

- [ ] **Step 1: Write failing evaluator tests**

Add a test beside `NoPrecedingCardOfSide_checks_all_earlier_cards`:
- Build three ordered cards: enemy first, candidate enemy second, enemy/player after it.
- `new NoFollowingCardOfSide(Side.Player)` succeeds when no later player card exists and fails when one exists.
- `new NoFollowingCardOfSide(Side.Enemy)` fails when a later enemy card exists.

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~ConditionEvaluatorTests"
```

Expected: FAIL because `NoFollowingCardOfSide` does not exist.

- [ ] **Step 2: Implement the condition record and evaluator**

In `Condition.cs`, add a sealed record mirroring `NoPrecedingCardOfSide`:

```csharp
public sealed record NoFollowingCardOfSide(Side Side) : Condition;
```

In `ConditionEvaluator.Evaluate`, after the existing `NoPrecedingCardOfSide` block is fine:
- Loop from `index + 1` to `ctx.Order.Count - 1`.
- If any later card's `Def.Side` matches, return `ConditionTier.Basic`.
- Otherwise return `ConditionTier.Success`.

- [ ] **Step 3: Add authoring mapping tests**

In `CardSpecMapperTests`, add an `EffectSpec` with:
- `Condition = ConditionKind.NoFollowingEnemyCard`
- `SuccessEffectValue` set

Assert that `CardSpecMapper.ToEffectData` maps the condition to `NoFollowingCardOfSide` with `Side.Enemy`.

- [ ] **Step 4: Implement authoring mapping**

In `EffectSpec.cs`, append `NoFollowingEnemyCard` to `ConditionKind`.

In `CardSpecMapper.ToCondition`, map:

```csharp
case ConditionKind.NoFollowingEnemyCard:
    return new NoFollowingCardOfSide(Side.Enemy);
```

- [ ] **Step 5: Add description tests**

In `DescriptionComposerTests`, update the current Goblin expectation for `NoPrecedingCardOfSide(Player)` from:

```text
피해 3. 앞에 플레이어 카드가 없으면 피해 6.
```

to:

```text
피해 3. 이전 수행한 플레이어 카드 없으면 피해 6.
```

Add a direct Warden-style card using `EffectData.Conditional(EffectKeys.Damage, 2, new NoFollowingCardOfSide(Side.Enemy), 7)` and assert:

```text
피해 2. 이후 수행한 적 카드 없으면 피해 7.
```

- [ ] **Step 6: Implement Korean wording**

In `KoreanDescriptionVocabulary.Condition`:
- `NoPrecedingCardOfSide` returns `"이전 수행한 " + SideName(n.Side) + " 카드 없으면"`.
- `NoFollowingCardOfSide` returns `"이후 수행한 " + SideName(n.Side) + " 카드 없으면"`.

Do not touch `AdjacentStem`.

- [ ] **Step 7: Verify Task 1**

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~ConditionEvaluatorTests|FullyQualifiedName~CardSpecMapperTests|FullyQualifiedName~DescriptionComposerTests"
```

Expected: PASS.

---

## Task 2: Add `ShuffleBagPolicy`

**Files:**
- Create: `Assets/FateWeaver/Simulation/Enemies/ShuffleBagPolicy.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/ShuffleBagPolicyTests.cs`

- [ ] **Step 1: Write failing tests**

Cover these cases:
- For a six-card catalog with `drawPerTurn = 2`, the first three calls produce six total draws containing the catalog exactly once.
- A fourth call starts a newly shuffled full catalog and returns two cards.
- Same seed gives the same sequence across new policy instances.
- Different seed gives a different multi-turn signature.
- If remaining cards are fewer than `drawPerTurn`, the policy reshuffles the full deck before drawing; it does not return a partial leftover.
- Empty catalog returns an empty list.
- `drawPerTurn <= 0` returns an empty list.

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~ShuffleBagPolicyTests"
```

Expected: FAIL because `ShuffleBagPolicy` does not exist.

- [ ] **Step 2: Implement policy**

Implementation notes:
- Constructor signature: `ShuffleBagPolicy(IReadOnlyList<CardDefinition> deck, int drawPerTurn, int seed)`.
- Store `deck ?? Array.Empty<CardDefinition>()`, clamp draw count with `Math.Max(0, drawPerTurn)`, and create one `Random`.
- Keep a private `List<CardDefinition> _bag`.
- `CardsForTurn(int turnIndex)` is stateful; ignore `turnIndex` except for satisfying the interface.
- If `_bag.Count < _drawPerTurn`, replace `_bag` with a full shuffled copy of `_deck`.
- Draw exactly `Math.Min(_drawPerTurn, _bag.Count)` cards from the front, removing them from `_bag`.
- Shuffle with the same deterministic Fisher-Yates pattern used in local tests, not LINQ ordering by random.

- [ ] **Step 3: Verify Task 2**

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~ShuffleBagPolicyTests"
```

Expected: PASS.

---

## Task 3: Add `SelfLockPolicy`

**Files:**
- Create: `Assets/FateWeaver/Simulation/Enemies/SelfLockPolicy.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/SelfLockPolicyTests.cs`

- [ ] **Step 1: Write failing tests**

Use `EnemyIntent` or a small fake `IEnemyTurnPolicy` as the inner policy.

Cover:
- A non-empty turn locks exactly one returned card.
- Non-selected cards remain unlocked.
- Locked card is a copied `CardDefinition` with `StartsLocked = true`; the original catalog card remains unchanged.
- Same seed gives same locked index sequence across policy instances.
- Different seed gives a different multi-turn signature.
- Empty inner result returns an empty list.

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~SelfLockPolicyTests"
```

Expected: FAIL because `SelfLockPolicy` does not exist.

- [ ] **Step 2: Implement policy**

Implementation notes:
- Constructor signature: `SelfLockPolicy(IEnemyTurnPolicy inner, int seed)`.
- Store `inner`; if `inner` is null, treat as empty policy or throw `ArgumentNullException`. Prefer throwing so misuse is visible.
- Use one private `Random`.
- In `CardsForTurn`, copy `inner.CardsForTurn(turnIndex)` into an array/list.
- If count is zero, return it.
- Pick one index with `_rng.Next(cards.Count)`.
- Replace only that card with `cards[index] with { StartsLocked = true }`.

- [ ] **Step 3: Verify Task 3**

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~SelfLockPolicyTests"
```

Expected: PASS.

---

## Task 4: Make locked enemy cards immune to execution order folding

**Files:**
- Modify: `Assets/FateWeaver/Simulation/DeckCombatSession.cs`
- Test: extend `Assets/FateWeaver/Tests/EditMode/SlowHasteStatusTests.cs` or create `LockedEnemyInitiativeTests.cs`

- [ ] **Step 1: Write failing session test**

Create a locked enemy card:

```csharp
new CardDefinition("locked_jab", "잠긴 찌르기", Side.Enemy, CardType.Attack, 5, effects)
{ EnergyCost = 0, Category = CardCategory.Execution, StartsLocked = true };
```

Start a `DeckCombatSession` with that enemy card as the policy output, add enemy Slow before `BeginNextTurn`, then assert next turn:
- The card is `IsLocked == true`.
- The execution order stays at base `5`, not slowed to `8`.

Use an unlocked enemy card in the same test file to preserve the existing slow behavior expectation.

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~LockedEnemyInitiative|FullyQualifiedName~SlowHasteStatusTests"
```

Expected: FAIL because `DeckCombatSession.BeginTurn` currently folds execution order before setting `IsLocked`.

- [ ] **Step 2: Implement the small order change**

In `DeckCombatSession.BeginTurn`, change the enemy card loop from fold-then-lock to lock-then-conditional-fold:

```csharp
var inst = new ExecutionCardInstance(enemyCard);
inst.IsLocked = enemyCard.StartsLocked;
if (!inst.IsLocked)
{
    inst.ExecutionOrder = StatusExecutionOrder.ExecutionOrderFor(inst.ExecutionOrder, enemyBag, _statuses);
}
_state.Zone.Add(inst);
```

- [ ] **Step 3: Verify Task 4**

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~LockedEnemyInitiative|FullyQualifiedName~SlowHasteStatusTests"
```

Expected: PASS.

---

## Task 5: Add `WardenDeck`

**Files:**
- Create: `Assets/FateWeaver/Simulation/WardenDeck.cs`
- Test: `Assets/FateWeaver/Tests/EditMode/WardenDeckTests.cs`

- [ ] **Step 1: Write failing deck contract tests**

Assert:
- `WardenDeck.EnemyId == "warden"`.
- `WardenDeck.StartingHp == 20`.
- `WardenDeck.Deck()` returns 6 cards.
- Id counts are:
  - `warden_swing`: 2
  - `warden_smash`: 1
  - `warden_uppercut`: 1
  - `warden_block`: 1
  - `warden_brace`: 1
- Every card has `Side.Enemy`, `EnergyCost = 0`, `Category = CardCategory.Execution`.
- `warden_smash` is damage 2 with `NoFollowingCardOfSide(Side.Enemy)` success effect value 7.
- `warden_uppercut` is damage 2 with `NoPrecedingCardOfSide(Side.Enemy)` success effect value 7.
- `warden_brace` is block 3 with `NoPrecedingCardOfSide(Side.Enemy)` success effect value 6.
- `WardenDeck.Policy(seed)` returns 2 cards per turn and exactly one card is locked each turn.

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~WardenDeckTests"
```

Expected: FAIL because `WardenDeck` does not exist.

- [ ] **Step 2: Implement `WardenDeck`**

Use the same style as `GoblinDeck`.

Required constants and methods:
- `public const string EnemyId = "warden";`
- `public const int StartingHp = 20;`
- `public const int CardsPerTurn = 2;`
- card factory methods for `Swing`, `Smash`, `Uppercut`, `Block`, `Brace`.
- `public static IReadOnlyList<CardDefinition> Deck()` returning six cards, with `Swing()` included twice.
- `public static IEnemyTurnPolicy Policy(int seed) => new SelfLockPolicy(new ShuffleBagPolicy(Deck(), CardsPerTurn, seed), seed);`

Card data:
- `warden_swing`, "휘두르기", Attack, execution order 5, `Damage(3)`.
- `warden_smash`, "내려치기", Attack, execution order 5, `Damage(2, NoFollowingCardOfSide(Enemy) -> 7)`.
- `warden_uppercut`, "올려치기", Attack, execution order 4, `Damage(2, NoPrecedingCardOfSide(Enemy) -> 7)`.
- `warden_block`, "막기", Defense, execution order 4, self `Block` 3 this turn.
- `warden_brace`, "버티기", Defense, execution order 4, self `Block` 3 this turn with success effect value 6 on `NoPrecedingCardOfSide(Enemy)`.

- [ ] **Step 3: Add Korean enemy name**

In `Assets/FateWeaver/Unity/PlaytestKoreanText.cs`, map `WardenDeck.EnemyId` to `"간수"` in `EnemyName`.

This is a tiny Unity file touch, but still no Warden encounter wiring in this plan.

- [ ] **Step 4: Verify Task 5**

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~WardenDeckTests|FullyQualifiedName~DescriptionComposerTests"
```

Expected: PASS.

---

## Task 6: Add Warden combat integration proof

**Files:**
- Test: `Assets/FateWeaver/Tests/EditMode/WardenDeckTests.cs` or `Assets/FateWeaver/Tests/EditMode/WardenIntegrationTests.cs`

- [ ] **Step 1: Write integration tests**

Cover at least these cases:
- `warden_smash` resolves for 7 damage when no later enemy card exists in resolution order.
- `warden_smash` resolves for 2 damage when a later enemy card exists.
- A locked Warden card rejects intervention movement through the existing intervention action path. This can reuse existing lock/intervention assertions if they already cover locked cards; otherwise add a direct `DeckCombatSession.PlayInterventionCard` regression with a locked Warden policy output.

Use fixed `EnemyIntent` where exact card order matters. Do not rely on shuffle randomness for condition-resolution tests.

- [ ] **Step 2: Verify Task 6**

Run:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~Warden"
```

Expected: PASS.

---

## Final Verification

- [ ] Run targeted Warden/condition/policy/description tests:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj --filter "FullyQualifiedName~Warden|FullyQualifiedName~ShuffleBagPolicyTests|FullyQualifiedName~SelfLockPolicyTests|FullyQualifiedName~ConditionEvaluatorTests|FullyQualifiedName~DescriptionComposerTests"
```

- [ ] Run the full headless suite:

```powershell
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj
```

- [ ] Check for formatting/whitespace issues:

```powershell
git diff --check
```

Expected:
- Headless suite PASS.
- `git diff --check` prints no errors.

Unity caveat:
- This plan touches `PlaytestKoreanText.cs` for the Warden enemy name, but does not compile the Unity assembly through the headless harness. After implementation, open Unity once and confirm there are no Console errors if `.meta`/asmdef import issues appear.

---

## Deferred Work

- Player-side lock / restraint / telegraph mechanics.
- Warden CardAssets, art, and `CardSO/Enemies/Warden/` content.
- DeckPlaytest encounter selector or Warden encounter insertion.
- `AdjacentCardIs` wording migration from "바로 앞/뒤" to "바로 이전/이후 수행한".
- Any balance tuning after the first headless Warden slice.

---

## Self-Review

**Spec coverage:**
- Shuffle bag without replacement and full reshuffle on insufficient remainder -> Task 2.
- Exactly one self-locked Warden card per turn -> Task 3 and Task 5.
- Lock as execution order-fold immunity -> Task 4.
- `NoFollowingCardOfSide` core/authoring/description support -> Task 1.
- Warden HP/deck/cards/policy -> Task 5.
- Warden conditional resolution proof -> Task 6.

**Scope control:**
- No Unity art/assets or playtest selection wiring.
- No `AdjacentCardIs` wording churn.
- No new abstraction beyond the two policy classes required by the spec.

**Verification path:**
- Every behavioral change has a failing-test-first step.
- Final proof remains the existing headless `.NET` harness plus `git diff --check`.
