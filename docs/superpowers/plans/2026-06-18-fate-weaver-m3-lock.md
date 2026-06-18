# Fate Weaver M3.4 Lock Plan

> Continue from M3.3. Add the smallest manipulation-blocking rule: a locked action card cannot be changed by fate manipulation.

## Goal

Introduce `Lock` as a fate action that marks a card as fixed. Locked cards reject later initiative-changing fate actions. This prepares the core for enemy disruption and preservation mechanics without adding status systems yet.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add status duration, turn cleanup, UI, deck/hand/draw, or enemy AI.
- Keep lock as a property on `ActionCardInstance` for now.

## Milestone Checklist

- [x] Add `ActionCardInstance.IsLocked`.
- [x] Add `FateActionKeys.Lock`.
- [x] Add `LockHandler`.
- [x] Verify `Lock` spends fate energy and locks a card.
- [x] Verify `ChangeInitiative` rejects locked target cards.
- [x] Verify `SwapInitiative` rejects when either target is locked.
- [x] Verify `FatePlayResolver` stops on a locked-card manipulation and preserves earlier successful plays.

## Test-First Steps

1. Add tests to `FateActionTests` for direct handler behavior.
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
- Timeline events for fate plays.
- Enemy actions that apply lock.
- UI indicators for locked future-zone cards.
