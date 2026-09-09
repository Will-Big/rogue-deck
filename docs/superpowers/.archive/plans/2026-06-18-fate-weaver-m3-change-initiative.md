# Fate Weaver M3.1 ChangeExecutionOrder Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> Continue from M2/M2.1. Keep the first intervention-card slice narrow: one intervention action changes one execution card's execution order before turn resolution.

## Goal

Prove the core Fate Weaver loop in code: an intervention action manipulates the future zone so a conditional execution card changes from `Basic` to `Success`.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Preserve direct `TurnResolver.Resolve(state, turnIndex)` for fixed-zone tests.
- Do not add decks, hands, draw, UI, ScriptableObjects, status effects, swap, lock, nullify, or target-resolution overhaul.

## Milestone Checklist

- [x] Add typed `InterventionActionKey` and `InterventionActionKeys.ChangeExecutionOrder`.
- [x] Add `InterventionActionData` with key, cost, and amount.
- [x] Add `InterventionPlayContext` with state, target card, amount, and cost-spent output.
- [x] Add `IInterventionActionHandler` and `InterventionActionRegistry`.
- [x] Add `ChangeExecutionOrderHandler`.
- [x] Verify fate energy is spent and execution order changes.
- [x] Verify an intervention play can turn a conditional card from `Basic` into `Success` before resolution.

## Test-First Steps

1. Add `InterventionActionTests`.
2. RED: assert `ChangeExecutionOrderHandler` spends cost and changes target execution order.
3. RED: assert insufficient fate energy rejects the action and leaves execution order unchanged.
4. RED: assert applying `ChangeExecutionOrder(-2)` before `TurnResolver` changes a `FirstToTrigger` card from default damage to success damage.
5. GREEN: implement only the intervention action primitives needed by those tests.
6. Run full headless tests.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Intervention card deck/hand/draw.
- A manipulation phase runner for multiple plays.
- Swap, lock, nullify, reorder, and force-condition-success actions.
- Real target resolution for player damage.
- Per-effect timeline events.
