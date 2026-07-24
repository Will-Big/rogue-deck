# Fate Weaver M3.3 SwapExecutionOrder Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> Continue from M3.2. Add one representative order-manipulation action: swap the execution order values of two selected execution cards.

## Goal

Prove that intervention actions can reorder the future zone without numeric nudging. `SwapExecutionOrder` exchanges two cards' initiatives before `TurnResolver`, allowing a conditional player card to move ahead of an enemy card and reach `Success`.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add UI selection, deck/hand/draw, lock/nullify, or adjacency validation.
- Keep the first version target-driven: caller provides both cards.

## Milestone Checklist

- [x] Add `InterventionActionKeys.SwapExecutionOrder`.
- [x] Extend `InterventionPlay`/`InterventionPlayContext` with optional `SecondaryTarget`.
- [x] Add `SwapExecutionOrderHandler`.
- [x] Verify direct handler swaps two cards and spends fate energy.
- [x] Verify `InterventionPlayResolver` can execute `SwapExecutionOrder`.
- [x] Verify swapping can turn a conditional card from `Basic` into `Success`.

## Test-First Steps

1. Add tests to `InterventionActionTests` for handler behavior and condition outcome.
2. Add one resolver test that executes `SwapExecutionOrder` through `InterventionPlayResolver`.
3. RED: observe missing key/handler/secondary target support.
4. GREEN: implement the smallest support needed.
5. Run full headless tests.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Adjacent-only validation.
- Swapping zone list positions separately from execution order values.
- UI target selection.
- Timeline events for intervention plays.
- Additional actions: lock, nullify, reorder.
