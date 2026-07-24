# Fate Weaver M4.1 Reward-Nullified Disruption Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> Continue from M3.4. Add the smallest enemy disruption behavior: the next player execution card cannot receive its condition-success reward.

## Goal

Represent the design rule from the balance notes: enemy cards should disrupt action-card condition rewards, including non-damage combo/setup cards later. For this first slice, an enemy effect marks the next player card as `ConditionRewardNullified`; if that card's condition would be `Success`, turn resolution treats it as `Basic`.

## Constraints

- Core remains pure C# with no UnityEngine references.
- C# 9 compatible.
- Do not build the full status system yet.
- Keep the marker directly on `ExecutionCardInstance` for now.
- Do not add duration cleanup beyond the current single resolution pass.

## Milestone Checklist

- [x] Add `ExecutionCardInstance.ConditionRewardNullified`.
- [x] Add `EffectKeys.NullifyNextPlayerConditionReward`.
- [x] Add `NullifyNextPlayerConditionRewardHandler`.
- [x] Pass frozen `ResolutionContext` into `EffectContext`.
- [x] Verify an enemy disruption marks the next player card.
- [x] Verify a marked card uses default amount even if its condition would succeed.

## Test-First Steps

1. Add tests to `ConditionalEffectResolutionTests`.
2. RED: assert disruption marks the next player card.
3. RED: assert a condition-success card resolves as `Basic` and uses default amount when marked.
4. GREEN: implement only the marker, context wiring, and handler.
5. Run full headless tests.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
```

## Deferred Work

- General `StatusKey` / `IStatusBehavior`.
- Status duration and cleanup.
- Per-effect event granularity.
- Enemy card catalog.
- UI indicators for disrupted cards.
