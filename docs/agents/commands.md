# 명령과 검증

**언제 읽나:** 저장소를 처음 받았을 때, 개별 도구를 직접 부를 때, 검증이 어디서 걸리는지 알아야
할 때. AGENTS.md 「명령」 절이 이 문서를 가리킨다.

## 처음 한 번

```bash
Tools/setup-dev.sh
```

공유 git 훅을 켜고(`git config core.hooksPath .githooks`) 필요한 도구가 있는지 확인한다. 약 2초.
이걸 돌리지 않으면 커밋 시점 검사가 전부 꺼진 상태로 작업하게 된다.

## 검증은 하나로

```bash
Tools/verify.sh
```

규칙 검사 + 헤드리스 코어 테스트 + 카드 저작 노트북 테스트를 순서대로 돌고, 실패하면 0이 아닌
코드로 끝난다. **Unity 에디터는 필요 없다.**

| 명령 | 무엇을 하나 | 걸리는 시간 |
|---|---|---|
| `Tools/setup-dev.sh` | 공유 git 훅 활성화 + 도구 확인 (최초 1회) | 약 2초 |
| `Tools/verify.sh` | 규칙 검사 + 헤드리스 + 노트북 전부 | 약 10초 |
| `Tools/verify.sh --quick` | 헤드리스 코어 테스트만 | 약 10초 |
| `Tools/verify.sh --lint` | 기계 검사만 (규칙 3·4·5·6·7 + R-doc) | 1초 미만 |
| `Tools/rule-note.sh <번호>` | 규칙 하나의 근거 절을 문서에서 꺼낸다 | 즉시 |
| `Tools/graph/rebuild-graph.sh` | graphify 그래프 재생성 (규칙 21) | 약 11초 |

## 개별 도구를 직접 부를 때

```bash
# 헤드리스 코어 테스트. 이 머신에는 .NET 5 SDK만 있어 타깃 오버라이드가 필수다.
# 빠뜨리면 NETSDK1045로 실패한다. --filter로 한 테스트만 돌릴 수 있다.
dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj -p:TargetFramework=net5.0 --nologo
```

```bash
# 카드 저작 노트북 테스트. 글로브가 필수다 — 디렉터리를 주면 Node가 모듈 경로로 해석해 죽는다.
node --test "Tools/card-idea-notebook/"*.test.mjs
```

## 밸런스 변경은 Compare로 본다 (규칙 12)

`ScenarioRunner.Compare` / `MultiTurnRunner.Compare`가 **무조작 실행과 조작 실행을 나란히** 돌려
차이를 낸다. 밸런스에 영향을 주는 변경은 이걸로 확인한다 — 쓰는 법은 기존 테스트를 따르면 된다:
[`ScenarioComparisonTests.cs`](../../Assets/Core/Tests/EditMode/ScenarioComparisonTests.cs) ·
[`DesignInvariantTests.cs`](../../Assets/Core/Tests/EditMode/DesignInvariantTests.cs) ·
[`MultiTurnRunnerTests.cs`](../../Assets/Core/Tests/EditMode/MultiTurnRunnerTests.cs).
`ScenarioCliReport.Build(scenarioId, statusContent)`가 그 결과를 마크다운으로 만든다.

**CLI로 부르지 마라.** `Tools/FateWeaver.Headless/`는 `.csproj`가 없어 빌드되지 않고, 남아 있는
`Program.cs`는 인자 하나짜리 옛 시그니처를 부른다(2026-09-09 확인). 커밋된 `bin/`은 그 시절의
산출물이다. Compare는 **테스트에서** 쓴다.

Unity EditMode 배치 실행(씬·프리팹·에셋을 건드렸을 때, 규칙 17)은 `-quit`를 붙이면 테스트 없이
exit 0으로 끝나는 함정이 있다. 검증된 전체 명령과 실행 장애 대응은
[`unity-batch-runs.md`](unity-batch-runs.md)에 있다.

## 검증이 걸리는 자리

어느 도구로 작업하든 같다.

| 자리 | 무엇을 잡나 | 우회 |
|---|---|---|
| `.githooks/pre-commit` | 커밋 직전, 바뀐 영역만 (`Tools/setup-dev.sh`로 활성화) | `--no-verify`로 가능 |
| `.githooks/commit-msg` | 커밋 제목 형식 (규칙 27) | `--no-verify`로 가능 |
| CI (`.github/workflows/verify.yml`) | push·PR마다 전체 | 불가 — 실질 게이트 |
| Claude Code 훅 (`.claude/`) | 브랜치 전환 시도, 턴 종료 시 코어 C# 변경 | Claude 세션 전용 |

훅이 규칙을 막을 때는 `Tools/rule-note.sh`로 `docs/agents/`의 근거 절을 꺼내 stderr에 낸다.
**훅 스크립트 자체에는 근거 문장이 없다** — 사본은 문서 하나뿐이다.

기존 예외는 `Tools/lint-allow.txt`에 있고 **그 목록은 부채 목록이며 줄이는 쪽으로만 쓴다.**

## 절차 문서는 왜 `docs/agents/`에 있나

어떤 도구로 열어도 읽히는 자리다. `.claude/`의 스킬·훅·권한은 Claude Code에서 그것을 자동으로
물어 오게 하는 껍데기일 뿐이며, **내용을 복사해 두지 않는다.** 복사본은 조용히 썩는다.
