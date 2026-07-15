# Task 6 Completion Report

## 구현

- `EffectKeys.MoveFormation`을 타입 안전 효과 키로 추가했다.
- `MoveFormationHandler`를 추가했다. 플레이어 카드는 살아 있는 `OwnerId` 파티원을 `CombatState.Party` 안에서, 적 카드는 살아 있는 `OwnerId` 적을 `CombatState.Enemies` 안에서 이동한다.
- 이동 값은 현재 index에 더하며 음수는 자기 진영 전열(index 0), 양수는 후방이다. 목적지는 자기 진영 리스트 경계에서 clamp하고 큰 정수 덧셈의 overflow를 피하도록 `long`으로 계산한다.
- 소유자가 누락되었거나 죽었으면 어떤 대형도 변경하기 전에 `NoValidTarget`으로 취소한다. 전열 fallback은 없다.
- `CombatRegistries.Effects()`에 새 핸들러를 등록했다.
- `TurnResolver` 통합 테스트로 order 2 이동 이후 order 5 적 `FrontMost` 공격이 변경된 대형의 B만 맞히며, 두 `CardResolved`가 실행 순서대로 발생함을 검증했다.

## 변경 파일

- Create: `Assets/Core/Effects/MoveFormationHandler.cs`
- Modify: `Assets/Core/Effects/EffectKey.cs`
- Modify: `Assets/Core/Simulation/CombatRegistries.cs`
- Create: `Assets/Core/Tests/EditMode/FormationTargetingIntegrationTests.cs`
- Create: `.superpowers/sdd/party-task-6-report.md`

## TDD RED

테스트 파일에 다음 5개 테스트를 제품 코드보다 먼저 작성했다.

- `Player_move_changes_only_party_order`
- `Enemy_move_changes_only_enemy_order`
- `Movement_clamps_to_own_formation_bounds`
- `Dead_or_missing_owner_cancels_instead_of_moving_front_member`
- `Later_frontmost_attack_uses_formation_after_earlier_move`

명령:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --filter FullyQualifiedName~FormationTargetingIntegrationTests
```

결과: exit 1. 아직 없는 `EffectKeys.MoveFormation`에서 `CS0117`, `MoveFormationHandler`에서 `CS0246`가 발생해 새 기능 부재로 인한 올바른 RED를 확인했다.

## Focused GREEN

명령:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --no-restore --filter FullyQualifiedName~FormationTargetingIntegrationTests --logger "console;verbosity=normal"
```

결과: exit 0, 5 passed / 0 failed / 0 skipped.

최종 자체 리뷰에서 적 카드의 null `OwnerId`가 null ID 적과 일치할 수 있는 경계 사례를 발견했다. `Dead_or_missing_owner_cancels_instead_of_moving_front_member` fixture에 null 소유자 재현을 먼저 추가했고, 수정 전 `Expected: NoValidTarget, But was: null`로 1 failed / 0 passed인 RED를 확인했다. 적 소유자 탐색 전에 null/empty `OwnerId`를 취소하도록 최소 수정한 뒤 focused 5 passed / 0 failed로 다시 GREEN을 확인했다.

## 전체 검증

커밋 직전 계약 명령:

```bash
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0
```

결과: exit 0, 218 passed / 0 failed / 0 skipped. `git diff --check`도 출력 없이 성공했다.

## 자체 리뷰

- `Party`와 `Enemies`를 섞지 않고 각 진영 index 0을 독립 전열로 취급한다.
- 음수/양수 방향과 양 끝 clamp를 테스트하며, 이동 거리가 `int.MinValue`/`int.MaxValue`여도 덧셈 overflow가 없다.
- 플레이어와 적 모두 죽은 소유자 및 누락/null 소유자를 검사하고, 취소 시 전열 멤버가 대신 이동하지 않음을 확인한다.
- 통합 fixture의 카드 이름은 `[검증] 대형 이동`, `[검증] 전열 공격`이고 ID는 `validation_*` 중립 ID만 사용한다.
- 위치 타겟 공격 로직은 변경하지 않았다. 기존 `DamageHandler`가 효과 실행 순간 `CombatState.Party`를 읽으므로 이동 이후 상태가 반영된다.
- 중앙 switch를 추가하지 않았고 효과 키/핸들러/레지스트리 패턴만 확장했다.
- UnityEngine, 새 RNG, 시간, GUID, 외부 패키지, C# 9 초과 문법을 추가하지 않았다.
- 변경은 Task 6 지정 제품/테스트 파일과 이 보고서에만 한정했다.

## 우려

차단 우려 없음. 이동 자체의 별도 이벤트 타입은 brief 범위가 아니며, 현재 타임라인은 요구대로 이동 카드의 `CardResolved`를 출력한다.
