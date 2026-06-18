# Fate Weaver M3.2 FatePlayResolver Plan

> Continue from M3.1 ChangeInitiative. This slice adds only a small ordered runner for already-selected fate plays.

## Goal

Apply multiple fate actions in script order before turn resolution, spending fate energy as each action succeeds. If a play cannot be applied, stop at that play and keep any earlier successful manipulation.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add deck, hand, draw, card discovery, UI, or ScriptableObject data.
- Do not add new fate action types beyond `ChangeInitiative`.
- Keep `ChangeInitiativeHandler` as the single source of initiative mutation.

## Milestone Checklist

- [x] Add `FatePlay` data: action + target card.
- [x] Add `FatePlayResult`: applied count, rejected index, total fate energy spent.
- [x] Add `FatePlayResolver` that executes plays in order through `FateActionRegistry`.
- [x] Verify multiple plays apply deterministically in order.
- [x] Verify insufficient energy stops the sequence and preserves earlier changes.

## Test-First Steps

1. Add `FatePlayResolverTests`.
2. RED: assert two `ChangeInitiative` plays apply in order and spend total cost.
3. RED: assert the second play is rejected when energy runs out, while the first remains applied.
4. GREEN: implement only `FatePlay`, `FatePlayResult`, and `FatePlayResolver`.
5. Run the full headless test command.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Fate card hand/deck/draw.
- Player input target selection.
- Continue-on-error policy variants.
- Timeline events for fate plays.
- Additional fate actions such as swap, lock, nullify, and reorder.
