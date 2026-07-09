# Fate Weaver M2 Conditions Plan

> Uses superpowers:writing-plans + superpowers:test-driven-development. Keep this slice small: evaluate card-position conditions against a frozen future-zone order. Do not wire conditional effects into `TurnResolver` yet.

## Goal

Add the pure-C# condition model and evaluator needed by execution cards to ask "is this card in a good position?" The observable outcome is a deterministic `ConditionTier` for a card inside a frozen resolution context.

## Constraints

- Core remains pure C# under `FateWeaver.Core` with `noEngineReferences:true`.
- Stay within Unity 6 / C# 9 constraints.
- Use tests first. A new behavior must fail before production code is added.
- M2 does not add intervention cards, conditional effect branching, statuses, or target resolution.

## Milestone Checklist

- [x] Add `ConditionTier` and condition data records.
- [x] Add `ResolutionContext` that freezes `FutureZone.ResolutionOrder()`.
- [x] Add `ConditionEvaluator`.
- [x] Verify these condition types:
  - `FirstToTrigger`
  - `WithinNth`
  - `AdjacentCardIs`
  - `BeforeNextEnemyAttack`
  - `SameTarget`
- [x] Run full headless EditMode proxy tests.

## Test-First Steps

1. Create `Assets/FateWeaver/Tests/EditMode/ConditionEvaluatorTests.cs`.
2. Add tests that reference the intended API and fail because condition types do not exist.
3. Implement only enough code to pass those tests.
4. Keep evaluation rules explicit:
   - `FirstToTrigger`: success when the card is first in resolution order, otherwise basic.
   - `WithinNth(n)`: success when the card index is less than `n`, otherwise basic.
   - `AdjacentCardIs(dir, side, type)`: success when the adjacent card exists and matches both side and type, otherwise basic.
   - `BeforeNextEnemyAttack`: success when no enemy attack appears before this card in the frozen order, otherwise basic.
   - `SameTarget`: success when this card and its immediate previous player card share a target id, otherwise basic.

## Implementation Files

- Create `Assets/FateWeaver/Core/Conditions/ConditionTier.cs`
- Create `Assets/FateWeaver/Core/Conditions/Condition.cs`
- Create `Assets/FateWeaver/Core/Conditions/ConditionEvaluator.cs`
- Update `Assets/FateWeaver/Core/Combat/ExecutionCardInstance.cs` with an optional `TargetId`.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- TurnResolver conditional effect branching.
- Per-effect event granularity.
- Real target resolution instead of M1's first-enemy shortcut.
- Status/disruption handling that can force condition reward failure.
