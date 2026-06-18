# Fate Weaver M3.3 SwapInitiative Plan

> Continue from M3.2. Add one representative order-manipulation action: swap the initiative values of two selected action cards.

## Goal

Prove that fate actions can reorder the future zone without numeric nudging. `SwapInitiative` exchanges two cards' initiatives before `TurnResolver`, allowing a conditional player card to move ahead of an enemy card and reach `Success`.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add UI selection, deck/hand/draw, lock/nullify, or adjacency validation.
- Keep the first version target-driven: caller provides both cards.

## Milestone Checklist

- [x] Add `FateActionKeys.SwapInitiative`.
- [x] Extend `FatePlay`/`FatePlayContext` with optional `SecondaryTarget`.
- [x] Add `SwapInitiativeHandler`.
- [x] Verify direct handler swaps two cards and spends fate energy.
- [x] Verify `FatePlayResolver` can execute `SwapInitiative`.
- [x] Verify swapping can turn a conditional card from `Basic` into `Success`.

## Test-First Steps

1. Add tests to `FateActionTests` for handler behavior and condition outcome.
2. Add one resolver test that executes `SwapInitiative` through `FatePlayResolver`.
3. RED: observe missing key/handler/secondary target support.
4. GREEN: implement the smallest support needed.
5. Run full headless tests.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Adjacent-only validation.
- Swapping zone list positions separately from initiative values.
- UI target selection.
- Timeline events for fate plays.
- Additional actions: lock, nullify, reorder.
