# Fate Weaver M2.1 Conditional Effects Plan

> Continue from M2 Conditions. Keep the slice narrow: condition evaluation affects effect amount during turn resolution, and the event timeline records the tier.

## Goal

Wire the M2 `ConditionEvaluator` into `TurnResolver` so an execution card can use a low default amount and a higher condition-success effect value. This proves the core balance rule: automatic execution cards can be weak by default and strong only when their future-zone position is correct.

## Constraints

- Core stays pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not add intervention cards, manipulation phase, statuses, block, target selection, or scenario runner.
- Preserve existing unconditional `EffectData(key, amount)` card definitions.

## Milestone Checklist

- [x] Add a conditional `EffectData` construction path.
- [x] Resolve condition tier inside `TurnResolver` using a frozen `ResolutionContext`.
- [x] Use `SuccessEffectValue` only when the tier is `Success`; otherwise use the default amount.
- [x] Add `ConditionTier` to `CardResolved` for timeline/report inspection.
- [x] Verify success and basic paths with headless tests.

## Test-First Steps

1. Add `ConditionalEffectResolutionTests`.
2. RED: assert a `FirstToTrigger` card deals success damage when first.
3. RED: assert the same conditional card deals default damage when not first.
4. GREEN: extend `EffectData`, `CardResolved`, and `TurnResolver` minimally.
5. Run the full headless test command.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- Multiple condition aggregation per card/effect.
- Failure-specific amount separate from basic amount.
- Per-effect event records for cards with multiple targets.
- Intervention-card manipulation that changes the condition result before resolution.
