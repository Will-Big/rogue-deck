# Fate Weaver M3.2 InterventionPlayResolver Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> Continue from M3.1 ChangeExecutionOrder. This slice adds only a small ordered runner for already-selected intervention plays.

## Goal

Apply multiple intervention actions in script order before turn resolution, spending fate energy as each action succeeds. If a play cannot be applied, stop at that play and keep any earlier successful manipulation.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add deck, hand, draw, card discovery, UI, or ScriptableObject data.
- Do not add new intervention action types beyond `ChangeExecutionOrder`.
- Keep `ChangeExecutionOrderHandler` as the single source of execution order mutation.

## Milestone Checklist

- [x] Add `InterventionPlay` data: action + target card.
- [x] Add `InterventionPlayResult`: applied count, rejected index, total fate energy spent.
- [x] Add `InterventionPlayResolver` that executes plays in order through `InterventionActionRegistry`.
- [x] Verify multiple plays apply deterministically in order.
- [x] Verify insufficient energy stops the sequence and preserves earlier changes.

## Test-First Steps

1. Add `InterventionPlayResolverTests`.
2. RED: assert two `ChangeExecutionOrder` plays apply in order and spend total cost.
3. RED: assert the second play is rejected when energy runs out, while the first remains applied.
4. GREEN: implement only `InterventionPlay`, `InterventionPlayResult`, and `InterventionPlayResolver`.
5. Run the full headless test command.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Intervention card hand/deck/draw.
- Player input target selection.
- Continue-on-error policy variants.
- Timeline events for intervention plays.
- Additional intervention actions such as swap, lock, nullify, and reorder.
