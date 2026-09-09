# Fate Weaver M3.4 Lock Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> Continue from M3.3. Add the smallest manipulation-blocking rule: a locked execution card cannot be changed by intervention manipulation.

## Goal

Introduce `Lock` as an intervention action that marks a card as fixed. Locked cards reject later execution order-changing intervention actions. This prepares the core for enemy disruption and preservation mechanics without adding status systems yet.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add status duration, turn cleanup, UI, deck/hand/draw, or enemy AI.
- Keep lock as a property on `ExecutionCardInstance` for now.

## Milestone Checklist

- [x] Add `ExecutionCardInstance.IsLocked`.
- [x] Add `InterventionActionKeys.Lock`.
- [x] Add `LockHandler`.
- [x] Verify `Lock` spends fate energy and locks a card.
- [x] Verify `ChangeExecutionOrder` rejects locked target cards.
- [x] Verify `SwapExecutionOrder` rejects when either target is locked.
- [x] Verify `InterventionPlayResolver` stops on a locked-card manipulation and preserves earlier successful plays.

## Test-First Steps

1. Add tests to `InterventionActionTests` for direct handler behavior.
2. Add a resolver test for lock followed by rejected manipulation.
3. RED: observe missing key/handler/locked state.
4. GREEN: implement only `IsLocked`, `LockHandler`, and locked checks in existing handlers.
5. Run full headless tests.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Status-based lock durations.
- Automatic lock cleanup.
- Timeline events for intervention plays.
- Enemy actions that apply lock.
- UI indicators for locked future-zone cards.
