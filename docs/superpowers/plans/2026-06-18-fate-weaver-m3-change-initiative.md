# Fate Weaver M3.1 ChangeInitiative Plan

> Continue from M2/M2.1. Keep the first fate-card slice narrow: one fate action changes one action card's initiative before turn resolution.

## Goal

Prove the core Fate Weaver loop in code: a fate action manipulates the future zone so a conditional action card changes from `Basic` to `Success`.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Preserve direct `TurnResolver.Resolve(state, turnIndex)` for fixed-zone tests.
- Do not add decks, hands, draw, UI, ScriptableObjects, status effects, swap, lock, nullify, or target-resolution overhaul.

## Milestone Checklist

- [x] Add typed `FateActionKey` and `FateActionKeys.ChangeInitiative`.
- [x] Add `FateActionData` with key, cost, and amount.
- [x] Add `FatePlayContext` with state, target card, amount, and cost-spent output.
- [x] Add `IFateActionHandler` and `FateActionRegistry`.
- [x] Add `ChangeInitiativeHandler`.
- [x] Verify fate energy is spent and initiative changes.
- [x] Verify a fate play can turn a conditional card from `Basic` into `Success` before resolution.

## Test-First Steps

1. Add `FateActionTests`.
2. RED: assert `ChangeInitiativeHandler` spends cost and changes target initiative.
3. RED: assert insufficient fate energy rejects the action and leaves initiative unchanged.
4. RED: assert applying `ChangeInitiative(-2)` before `TurnResolver` changes a `FirstToTrigger` card from default damage to success damage.
5. GREEN: implement only the fate action primitives needed by those tests.
6. Run full headless tests.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Fate card deck/hand/draw.
- A manipulation phase runner for multiple plays.
- Swap, lock, nullify, reorder, and force-condition-success actions.
- Real target resolution for player damage.
- Per-effect timeline events.
