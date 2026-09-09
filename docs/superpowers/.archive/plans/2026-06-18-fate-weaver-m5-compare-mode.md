# Fate Weaver M5.2 Compare Mode Plan

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

> Continue from M5.1. Add no-manipulation baseline comparison so a human can see why intervention plays matter.

## Goal

Run the same scenario twice: once with no intervention plays, once with the scripted intervention plays. Report the behavioral delta in Markdown.

## Constraints

- Keep Simulation pure C# with no UnityEngine references.
- C# 9 compatible.
- One-turn scenarios only.
- Do not add JSON loading, file output, UI, or deck/hand/draw.
- Keep `ScenarioRunner.Run` unchanged for direct scripted execution.

## Milestone Checklist

- [x] Add `ScenarioRunner.Compare`.
- [x] Add `ScenarioComparisonResult`.
- [x] Add `ScenarioComparisonReport.ToMarkdown`.
- [x] Verify baseline `quick_cut` is `Basic` damage 2.
- [x] Verify manipulated `quick_cut` is `Success` damage 10.
- [x] Verify comparison report shows baseline, manipulated, and HP deltas.
- [x] Update `Tools/FateWeaver.Headless` to print comparison report.

## Test-First Steps

1. Add `ScenarioComparisonTests`.
2. RED: assert `Compare` exposes baseline and manipulated results.
3. RED: assert Markdown report contains both paths and enemy/player HP deltas.
4. GREEN: implement minimal comparison model/report.
5. Run full headless tests and console command.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
dotnet run --project "C:/UnityProjects/Rogue-deck/Tools/FateWeaver.Headless/FateWeaver.Headless.csproj" --no-launch-profile
```

## Deferred Work

- Multi-turn comparison.
- Structured metrics beyond HP deltas.
- Report file output.
- Scenario selection from command-line args.
