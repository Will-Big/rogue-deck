# P0-B2 — `CardType` 제거와 효과 기반 카드 성질 합성 (구현 기록)

> **보관 문서:** 완료되었거나 현재 기준에서 대체된 역사 기록입니다. 현행 규칙의 권위 문서가 아니며, 현재 문서는 [`docs/superpowers/README.md`](../../README.md)에서 확인합니다.

- 작성일: 2026-07-20
- 상태: **구현 완료, 머지 후 사용자 Play 검증 대기**
- 원 백로그: [`2026-07-16-architecture-refactor-backlog.md`](../../plans/2026-07-16-architecture-refactor-backlog.md) §4.1
- 설계 문서: [`../specs/2026-07-19-card-type-removal-design.md`](../specs/2026-07-19-card-type-removal-design.md)
- 구현 계획: [`2026-07-19-card-type-removal.md`](2026-07-19-card-type-removal.md)
- 검증 브랜치: `refactor/p0-b2-card-type-removal`
- 검증 기준 커밋: `e448b9e refactor(unity): remove CardType authoring field`

## 구현 결과

카드의 단일 `Attack`/`Skill`/`Defense` 분류를 제거하고, 정의에 `EffectKeys.Damage`가 있는지를
`CardDefinition.HasEffect(EffectKey)`로 질의하도록 바꿨다. 인접·이전·선행 카드 조건과 다음 플레이어 피해 카드
강화는 이 타입 안전 질의를 사용한다. `Damage + ApplyStatus(Block)` 복합 카드는 피해 카드로 판정되고,
Block 전용 카드는 피해 카드로 판정되지 않는다. 실행/조작 플레이 경로 구분은 계속 `CardCategory`만 담당한다.

코어·저작·시나리오·생성 코드에서 `CardType`과 `Type` 스키마를 제거했으며, Unity `CardAsset`, 코드 생성기,
18개 CardSO YAML에서도 직렬화된 `Type` 필드를 제거했다. 이전 공격 용어의 효과 키·핸들러·저작 spec·조건 이름과
한국어 설명은 `DamageCard`/`피해 카드` 의미로 이관했다.

## 커밋 목록

1. `166c5b7 refactor(core): derive card effects through typed query`
2. `200de79 refactor(core): derive damage card conditions from effects`
3. `8fc85a1 refactor(core): target next damage card by effect`
4. `7c7c8d5 refactor(core): remove CardType schema`
5. `e448b9e refactor(unity): remove CardType authoring field`

## Tasks 1–5 RED/GREEN 증거

### Task 1 — 타입 안전 효과 질의

- RED: `CardDefinitionDataTests`에 효과 구성과 빈 키 테스트를 먼저 추가한 뒤, `CardDefinition.HasEffect`가 없어
  `CS1061` 컴파일 실패를 확인했다.
- GREEN: 집중 테스트 6/6 통과, 당시 전체 헤드리스 311/311 통과(실패 0, 스킵 0).
- 결과: 최상위 효과 키의 정확한 일치만 검사하고 빈 키는 `ArgumentException`으로 거부한다.

### Task 2 — 효과 기반 피해 카드 조건

- RED: 인접·이전 실행·다음 적 피해 카드 테스트를 먼저 추가한 뒤 새 조건 타입이 없어 `CS0246` 컴파일 실패를
  확인했다.
- GREEN: `ConditionEvaluatorTests` 11/11 통과, 당시 전체 헤드리스 314/314 통과(실패 0, 스킵 0).
- 결과: Damage 단일/복합 카드와 Block 전용 카드의 판정 및 취소 카드 건너뛰기 의미를 효과 구성으로 검증했다.

### Task 3 — 다음 플레이어 피해 카드 강화

- RED: Block 전용 카드를 건너뛰고 다음 복합 피해 카드를 강화하는 테스트를 먼저 추가한 뒤 새 효과 키와 핸들러가
  없어 `CS0117`, `CS0246` 컴파일 실패를 확인했다.
- GREEN: 집중 테스트 5/5 통과, 당시 전체 헤드리스 314/314 통과(실패 0, 스킵 0). 복합 카드는 기본 피해 1에
  보너스가 적용되어 7 피해를 주고 앞선 Block 전용 카드는 0 피해임을 확인했다.
- 결과: 이전 공격 기반 키·타입·raw 문자열은 별칭 없이 제거했고, 이동한 `.cs`/`.meta` GUID를 보존했다.

### Task 4 — 순수 C# 스키마 제거

- RED: 아키텍처 테스트가 기존 `FateWeaver.Core.Cards.CardType` 타입을 발견해 1 실패로 끝나는 것을 확인했다.
- GREEN: 집중 아키텍처 테스트 1/1 통과, 당시 전체 헤드리스 315/315 통과(실패 0, 스킵 0).
- 결과: `CardDefinition`, `CardSpec`, `ZoneCardSpec`과 코어/시뮬레이션/fixture 생성자에서 타입 필드를 제거했다.
  콘텐츠 golden 서명은 타입 열만 제거하고 카드 ID, 이름, 진영, 카테고리, 비용, 실행 순서, 효과와 설명을 유지했다.

### Task 5 — Unity 저작 스키마 제거와 생성기 멱등성

- RED: `CardAsset` reflection 회귀 테스트를 먼저 추가했다. Task 4가 공유 `CardType`을 먼저 제거했으므로 assertion
  실행 전 `Assets/Unity/CardAsset.cs`의 잔여 필드가 `CS0246` 컴파일 실패를 일으켰다. 이 전이 상태를 Task 5의
  예상 RED로 승인해 기록했다.
- GREEN: `CardAsset.Type`, `ToSpec`/생성기 전달과 출력, CardSO 18개의 정확한 `  Type:` YAML 줄을 제거했다.
  집중 Unity EditMode 테스트는 7/7 통과했고 schema reflection 테스트도 통과했다.
- 생성기 1차: `/private/tmp/p0b2-generate-first.log`, Unity 종료 0, `Generated
  Assets/Core/Simulation/Generated/GeneratedCards.cs` 확인.
- 생성기 2차: `/private/tmp/p0b2-generate-second.log`, Unity 종료 0, 같은 생성 메시지 확인. 2회차 뒤
  `GeneratedCards.cs` SHA-256은
  `3569a0e87b68debd11663516ba508a4de47cb20804cde8b6003c55b81d0201c3`로 유지됐고,
  `git diff --exit-code -- Assets/Core/Simulation/Generated/GeneratedCards.cs`는 종료 0이었다. 즉 두 번째 생성은
  추가 diff를 만들지 않았다.

## 최종 전체 검증

모든 최종 검증은 `e448b9e` 위에서 2026-07-20에 새로 실행했다.

### 헤드리스 전체

```bash
DOTNET_CLI_HOME=/private/tmp/p0b2-dotnet-home \
  /usr/local/share/dotnet/x64/dotnet test \
  Tests/Headless/FateWeaver.Tests.Headless.csproj
```

결과: **315/315 통과, 실패 0, 스킵 0**, 프로세스 종료 0. 첫 샌드박스 시도는 NuGet 네트워크 접근이
차단되어 restore 전에 중단됐고, 동일 명령을 승인된 환경에서 다시 실행해 위 결과를 얻었다.

### Unity EditMode 전체

```bash
/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/ish/.codex/worktrees/2b13/rogue-deck \
  -runTests -testPlatform EditMode \
  -testResults /private/tmp/p0b2-editmode.xml \
  -logFile /private/tmp/p0b2-editmode.log
```

결과: **365/365 통과, 실패 0, 스킵 0, inconclusive 0**, 프로세스 종료 0. 결과 XML의 최상위
`test-run`은 `result="Passed" total="365" passed="365" failed="0" skipped="0"`이며, 로그는
`Test run completed. Exiting with code 0 (Ok).`를 기록했다. 결과 파일은
`/private/tmp/p0b2-editmode.xml`, 전체 로그는 `/private/tmp/p0b2-editmode.log`에 있다. 첫 샌드박스 시도는
Licensing Client IPC 연결 실패로 테스트 진입 전에 중단했고, 동일 명령을 승인된 환경에서 새로 실행한 결과가 위
수치다.

### 구조·작업 트리 검사

다음 필수 검색을 각각 실행하고 출력을 직접 확인했다.

```bash
rg -n '\bCardType\b|GrantNext(Player)?Attack|grant_next_player_attack_damage_bonus' \
  Assets --glob '*.cs' --glob '*.asset'
rg -n '^  Type:' Assets/Unity/CardSO --glob '*.asset'
```

두 검색 모두 **출력 없음**(`rg` 종료 1)이었다. 따라서 제품·저작·생성·테스트 C# 및 CardSO asset에 이전
식별자나 직렬화된 `Type` 필드가 남아 있지 않다.

`git diff --check`는 출력 없이 종료 0이었다. 문서 편집 전 `git status --short --branch`는 지정 브랜치와
의도적으로 커밋하지 않는 비추적 `.superpowers/`만 표시했으며, Unity 실행이 추적 파일 변경을 만들지 않았음을
확인했다. 최종 문서 커밋에는 이 기록과 백로그 상태 변경 두 파일만 포함한다.

## 남은 게이트와 주의사항

- 이 작업은 `master`에 머지하지 않았다. 전체 검증 보고 후 사용자 승인이 있어야만 머지한다.
- 전용 worktree에서는 Unity GUI/Play 검증을 수행하지 않았다. 머지 후 메인 체크아웃에서 사용자가 전투 Play,
  Inspector/SO 표시, 카드 생성·실행 연출을 확인해야 한다.
- 따라서 현재 상태는 **구현 완료, 머지 후 사용자 Play 검증 대기**이며 Play 검증 완료로 표시하지 않는다.
- Task 5 RED는 계획의 reflection assertion 실패가 아니라 선행 Task 4 스키마 제거로 인한 예상 가능한 `CS0246`
  컴파일 실패였다. 영구 reflection 회귀 테스트는 GREEN에서 실행되어 통과했다.

## 2026-07-20 — Status key dropdown follow-up

`StatusKeyRef.Id`는 계속 직렬화 스키마로 남아 있으며, 등록된 런타임 상태가 Inspector 드롭다운을 구동한다.
알 수 없는 값은 `Unknown: <key>`로 표시되고, 계속 `AuthoringValidator`를 실패시키며, repaint 중에는 절대 다시
기록되지 않는다. 전투 규칙은 변경하지 않았고, 보류 중인 사용자 Play 검증 게이트도 변경하지 않았다.

### 후속 검증

- Guard 저작 YAML은 `Status:` 다음에 `Id: block`이 오는 구조를 유지한다
  (`Assets/Unity/CardSO/Player/guard.asset:38-39`).
- 2026-07-20 전체 헤드리스 회귀 명령의 첫 샌드박스 시도는 테스트 호스트의
  `System.Net.Sockets.SocketException (13): Permission denied`로 중단되어 종료 1이었다. 동일 명령을 승인된
  환경에서 재시도한 결과는 **316/316 통과, 실패 0, 스킵 0, 종료 0**이었다.
- 2026-07-20 전체 Unity EditMode 명령은 일반 및 승인 재시도 모두 다른 Unity 인스턴스가 이 프로젝트를 열고 있다는
  fatal 오류로 컴파일 전에 중단되어 종료 1이었다. 따라서 Unity EditMode 전체 통과를 주장하지 않는다.
- `git diff master...HEAD --check`는
  `docs/superpowers/plans/2026-07-20-status-key-dropdown-authoring.md:410: new blank line at EOF.`를 보고했다.
  이 기존 브랜치 차이는 이 후속 기록에서 변경하지 않았다.

### 2026-07-20 최종 Unity EditMode 재검증

- Unity 6000 Popup overload 수정 뒤, 남아 있던 batchmode 및 License Client 프로세스를 재설정한 후 전체
  EditMode를 다시 실행했다.
- `/private/tmp/status-key-dropdown-editmode-final.xml`은 **369 통과, 실패 0, 스킵 0**의 성공한 전체 EditMode
  실행을 기록하며, `StatusKeyDropdownOptionsTests`의 세 테스트도 모두 통과했다.
- 앞선 Unity 실행 차단 기록은 당시의 실행 환경 증거로 그대로 유지한다. 이 성공 결과는 2026-07-20 최종 재실행의
  결과다.
