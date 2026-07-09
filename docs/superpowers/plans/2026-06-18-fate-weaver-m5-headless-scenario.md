# Fate Weaver M5.1 Headless Scenario Plan

> Continue from M4.1. This is the first human-runnable slice: a fixed one-turn scenario can be executed from a console and reported as Markdown.

## Goal

Create a pure-C# simulation layer that builds a `CombatState` from scenario data, applies scripted intervention plays, resolves the turn, and emits a readable Markdown report.

## Constraints

- Keep simulation pure C# and free of UnityEngine references.
- C# 9 compatible.
- Support one-turn scenarios only.
- Use existing Core handlers: damage, reward-nullify, change execution order, swap execution order, lock.
- Do not add deck/hand/draw, JSON loading, Unity UI, or multiple-turn campaign flow yet.

## Milestone Checklist

- [x] Add `FateWeaver.Simulation` asmdef referencing `FateWeaver.Core`.
- [x] Add scenario data types: enemies, zone cards, intervention plays.
- [x] Add `ScenarioRunner`.
- [x] Add `ScenarioReport.ToMarkdown`.
- [x] Add a sample scenario where `SwapExecutionOrder` turns `quick_cut` from `Basic` into `Success`.
- [x] Add `Tools/FateWeaver.Headless` console runner for `dotnet run`.
- [x] Verify through tests and console output.

## Test-First Steps

1. Add `ScenarioRunnerTests`.
2. RED: assert sample scenario resolves `quick_cut` as `Success` and deals 10 damage.
3. RED: assert Markdown report contains initial order, intervention play summary, resolution, and final state.
4. GREEN: implement the minimal simulation model, runner, report, and sample scenario.
5. Add console project that prints the sample report.
6. Run full headless tests and `dotnet run`.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
dotnet run --project "C:/UnityProjects/Rogue-deck/Tools/FateWeaver.Headless/FateWeaver.Headless.csproj" --no-launch-profile
```

## Deferred Work

- JSON scenario loading.
- Multi-turn scenario scripts.
- Compare mode with no-manipulation baseline.
- Scenario report files.
- Unity UI.
