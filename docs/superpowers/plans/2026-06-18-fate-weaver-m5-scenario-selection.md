# Fate Weaver M5.3 Scenario Selection Plan

> Continue from M5.2. Add one more sample and let the headless console choose a scenario by id.

## Goal

Make the headless runner useful for more than one case. Add a `Reward Nullified` scenario that demonstrates enemy disruption, and expose a small sample registry for CLI selection.

## Constraints

- Keep Simulation pure C# with no UnityEngine references.
- C# 9 compatible.
- No JSON loading yet.
- No file output yet.
- Keep CLI parsing minimal: first arg is scenario id.

## Milestone Checklist

- [x] Add `SampleScenarios.RewardNullified`.
- [x] Add `SampleScenarios.All` and `SampleScenarios.Find`.
- [x] Verify `quick-cut-swap` and `reward-nullified` are discoverable.
- [x] Verify reward-nullified comparison shows baseline success damage 10, manipulated/basic damage 2.
- [x] Update headless CLI to accept scenario id.
- [x] Verify console command for both scenarios.

## Test-First Steps

1. Add `SampleScenarioTests`.
2. RED: assert registry lookup by id.
3. RED: assert reward-nullified comparison demonstrates enemy disruption.
4. GREEN: implement registry/sample and CLI arg selection.
5. Run tests and `dotnet run` for both ids.

## Verification

```bash
dotnet test "C:/UnityProjects/Rogue-deck/Tests/Headless/FateWeaver.Tests.Headless.csproj" --nologo
dotnet run --project "C:/UnityProjects/Rogue-deck/Tools/FateWeaver.Headless/FateWeaver.Headless.csproj" --no-launch-profile -- quick-cut-swap
dotnet run --project "C:/UnityProjects/Rogue-deck/Tools/FateWeaver.Headless/FateWeaver.Headless.csproj" --no-launch-profile -- reward-nullified
```

## Deferred Work

- JSON scenario loading.
- Command help/list output.
- Multi-turn scenario data.
- Report file output.
