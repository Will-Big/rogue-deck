# Unity MCP

**언제 읽나:** 실행 중인 Unity 에디터를 AI가 직접 조작하려 할 때, MCP 도구가 0개로 보일 때,
그리고 MCP가 **어느 체크아웃의 에디터에 붙는지**를 확인해야 할 때.

이 저장소는 Unity 공식 CLI(`unity`)가 내장한 MCP 서버를 쓴다. 서드파티 Unity MCP 패키지는 쓰지
않는다. 서버는 별도 프로세스가 아니라 `unity mcp` 서브커맨드이며, 붙어 있는 에디터가 노출하는
커맨드를 그대로 MCP 도구로 내보낸다.

> **먼저: 이 문서는 소수의 세션에만 해당한다.** 최근 100커밋 중 씬·프리팹을 건드린 것은 3건이다.
> 코어 로직·문서·노트북 작업은 MCP도 에디터도 `Library/`도 필요 없고 `Tools/verify.sh`가 Unity
> 없이 10초에 답한다. **아래의 "에디터를 먼저 띄운다"는 MCP로 에디터를 조작하기로 한 세션에만
> 적용되는 조건부 규칙이지, 워크트리를 만들 때마다 치러야 하는 비용이 아니다.**

## 구성 요소

| 자리 | 무엇 | 커밋 |
|---|---|---|
| `Packages/manifest.json`의 `com.unity.pipeline` | 에디터 쪽 절반. 이게 로드돼야 에디터가 Pipeline 서버를 연다 | O |
| `.mcp.json` | Claude Code용 MCP 서버 등록(프로젝트 스코프) | O |
| `.codex/config.toml`의 `[mcp_servers.unity]` | Codex CLI용 MCP 서버 등록(프로젝트 스코프) | O |
| `.claude/skills/unity-cli/` | Unity CLI 사용법 문서. Claude Code가 읽는 자리 | O |
| `.agents/skills/unity-cli/` | 같은 문서. Codex가 읽는 자리 | O |
| `~/.unity/bin/unity` | CLI 본체 겸 MCP 서버. 저장소 밖, 머신 로컬 | — |

`.claude/skills/unity-cli/`와 `.agents/skills/unity-cli/`는 **내용이 같은 사본 둘**이다. 클라이언트마다
스킬을 찾는 경로가 다르고 `unity skill install`이 심볼릭 링크를 통한 쓰기를 거부하므로, 사본을
합치지 않는다. 이 둘은 `.claude/skills/graphify-usage/`처럼 `docs/agents/`를 가리키는 포인터가
아니라 **업스트림 문서의 벤더링 사본**이다 — 손으로 고치지 말고 아래 「갱신」대로 다시 렌더한다.

## 서버가 어느 에디터에 붙나 — 규칙 15와 겹치는 자리

**규칙은 하나다. 작업 중인 체크아웃에 에디터를 띄워라. 그러면 격리가 성립하고, 안 띄우면 남의
에디터를 잡는다.**

에디터는 하나로 한정되지 않는다. 여러 대가 동시에 Pipeline 서버를 열고 포트를 자동 배분한다
(실측: 메인 7800, 워크트리 7801). 대상 선택은 떠 있는 에디터 수에 따라 갈린다.

2026-09-04 실측. 전부 `editor_status`의 `projectPath`로 확인했다.

| 떠 있는 에디터 | `unity mcp`의 cwd | 결과 |
|---|---|---|
| 워크트리 하나 | 메인 체크아웃 | 워크트리에 붙음 (도구 149개) |
| 메인 체크아웃 하나 | 워크트리 | **메인 체크아웃에 붙음 — 위험** |
| 메인 + 워크트리 | 워크트리 | 워크트리에 붙음 — 올바름 |
| 메인 + 워크트리 | 메인 체크아웃 | 메인 체크아웃에 붙음 — 올바름 |
| 메인 + 워크트리 | 둘 다 아닌 곳 | **도구 0개 + 후보를 나열한 에러** — 안전 |

읽는 법:

- **에디터가 하나면 cwd를 무시하고 그것에 붙는다.** 여기서만 조용히 엉뚱한 곳을 고칠 수 있다.
  둘째 행이 그 경우이고, 열려 있는 에디터가 사용자의 메인 체크아웃 것 하나뿐인 평소 상태가 정확히
  이것이다. 워크트리에 갇혀 있다고 믿는 세션이 사용자 전용 체크아웃의 씬·프리팹·에셋을 고치고,
  **그 세션의 `git status`에는 아무것도 안 뜬다.** 워크트리의 트리를 보고 있기 때문이다.
- **에디터가 둘 이상이면 cwd로 고르고, 못 고르면 실패한다.** 셋째·넷째 행처럼 cwd의 프로젝트에
  에디터가 있으면 그것을 고른다. 다섯째 행처럼 어느 쪽도 아니면 **추측하지 않고** 도구 0개와 함께
  후보 목록과 `--project-path`를 쓰라는 에러를 낸다. 상위 문서의 "cwd를 포함하는 프로젝트의
  에디터를 고른다"는 서술은 이 경우의 규칙이지, 에디터가 하나일 때의 안전장치가 아니다.

그래서 지켜야 할 것:

- **워크트리에서 MCP로 저작하려면 그 워크트리에 에디터를 먼저 띄운다**(아래). 이것이 유일한
  구조적 방어다. 띄우고 나면 사용자가 메인 체크아웃에서 자기 에디터를 어떻게 쓰든 서로 간섭하지
  않는다.
- **에디터를 안 띄웠으면 쓰기 도구를 부르기 전에 대상을 확인한다.** `unity status`로 어느
  프로젝트의 에디터가 떠 있는지 보거나 `editor_status`의 `projectPath`를 읽는다. 확인 없이
  `create_prefab`·`create_gameobject`·`delete_asset`·`eval` 같은 것을 부르지 않는다.
- **후보 나열 에러를 우회하려 하지 않는다.** 그것은 고장이 아니라 안전장치다. 대상을
  `--project-path`로 명시하거나, 그 프로젝트 디렉터리에서 부른다.
- 배치 실행과 마찬가지로, 끝나면 `git status`로 의도한 변경만 스테이징한다(규칙 17). 단 둘째 행에
  걸린 경우 **이 확인은 아무것도 못 잡는다.** 그래서 에디터를 띄우는 쪽이 먼저다.

### 비용과 정리

에디터 한 대마다 그 체크아웃의 `Library/`가 따로 필요하다. 이 프로젝트 실측으로 **디스크 2.3GB,
상주 메모리 약 450MB**, 콜드 임포트에 몇 분이다. 워크트리 서넛을 동시에 돌리면 그만큼 곱해진다 —
병행 세션 수를 정할 때 대상 선택이 아니라 이쪽이 실질적인 한계다.

**한 번이라도 Unity를 돌린 워크트리는 일이 끝나면 반드시 제거한다.** 머지했다면 규칙 19의 정리에
`Library/` 2.3GB가 함께 사라진다. 남겨 두면 그것만 쌓여 수십 GB가 된다. Unity를 돌리지 않은
워크트리는 약 51MB라 이 부담이 없다.

```bash
git worktree remove <워크트리 경로>     # 안에서 돌던 에디터를 먼저 죽인다
git worktree list                       # 남은 것 확인
du -sh .claude/worktrees/*/Library      # Library가 남아 있는 워크트리 찾기
```

### 워크트리에 상주 헤드리스 에디터 띄우기

`-quit`를 **붙이지 않아야** 에디터가 남아 Pipeline API를 계속 서빙한다.

```bash
nohup '/Applications/Unity/Hub/Editor/6000.5.2f1/Unity.app/Contents/MacOS/Unity' \
  -batchmode \
  -projectPath <워크트리 절대 경로> \
  -logFile /private/tmp/<이름>.log > /dev/null 2>&1 &
```

준비될 때까지 기다린 뒤 확인한다. 첫 실행은 `Library/`가 없어 임포트에 몇 분이 걸린다.

```bash
until unity status --json | grep -q '"state"'; do sleep 5; done
unity status                      # state가 ready인지
unity command                     # 이 에디터가 노출하는 커맨드 목록
```

일이 끝나면 그 에디터는 죽인다. **남의 워크트리 에디터는 죽이지 않는다**(규칙 26).

## 함정

- **도구가 0개로 보이면 거의 항상 에디터가 없는 것이다.** `unity mcp`는 에디터가 없어도 정상
  기동해 핸드셰이크에 응답하고 도구 목록만 비운다. 서버 고장으로 오진하지 말고 `unity status`부터
  본다.
- **Safe Mode에서는 아예 못 붙는다.** C# 컴파일 에러가 있으면 에디터가 Safe Mode로 뜨고 Pipeline
  패키지가 로드되지 않아 `unity status`·`unity command`가 연결에 실패한다. `unity pipeline list`로
  확인하고, 이때는 파일 직접 편집으로 도망가지 말고 컴파일 에러를 고친 뒤 에디터를 재시작한다.
- **`unity mcp configure claude-code`를 그냥 돌리지 않는다.** 이 클라이언트만 설정 파일을 쓰지 않고
  `claude mcp add --scope user --transport stdio unity-editor-mcp unity mcp`를 대신 실행한다
  (`--dry-run`으로 확인한 실제 명령이다). 결과는 셋 다 나쁘다:
  1. 이름이 `unity-editor-mcp`라 우리 `.mcp.json`의 `unity`와 **다른 서버로 취급된다.** 둘 다 붙어
     같은 도구 149개가 두 벌, 298개가 모든 세션 컨텍스트에 들어간다.
  2. `--scope user`는 **모든 프로젝트에 적용된다.** Unity와 무관한 저장소를 열어도 프로세스가 뜬다.
  3. 명령이 절대 경로가 아닌 `unity`라 PATH에 의존한다. 여기서는 `~/.zshrc`가 `~/.unity/env`를
     읽어 넣어 주지만, 로그인 셸을 거치지 않고 뜨는 클라이언트에서는 깨진다.

  이미 붙였다면 `claude mcp remove --scope user unity-editor-mcp`로 지운다.
- **등록 파일의 `command`는 절대 경로다**(`/Users/ish/.unity/bin/unity`). MCP 클라이언트가 로그인
  셸 없이 프로세스를 띄우는 경우가 있어 `~/.unity/env`가 PATH에 넣어 주는 것을 믿을 수 없기
  때문이다. 다른 머신에서 클론하면 `.mcp.json`과 `.codex/config.toml`의 이 경로 두 줄을 고친다.
- **Codex는 프로젝트를 trusted로 표시해야 `.codex/config.toml`을 읽는다.** 처음 열 때 뜨는 신뢰
  프롬프트를 승인하지 않으면 이 파일이 조용히 무시된다.
- **`com.unity.pipeline`은 실험 버전이다**(도입 시점 `0.6.0-exp.1`). `-exp` 패키지는 마이너 갱신에서
  API가 깨질 수 있으니, `unity pipeline upgrade` 뒤에는 MCP 도구가 그대로인지 확인한다.
- **빌드에 서버가 들어가지는 않는다 — 조건 셋이 모두 맞아야 켜진다.** 이 패키지의 `Runtime`
  어셈블리는 `includePlatforms`가 비어 있어 플레이어에 **컴파일은 된다.** 다만 서버·eval·핫리로드
  코드는 `#if UNITY_EDITOR || (UNITY_STANDALONE && DEBUG)`로 감싸여 릴리스 빌드에서는 아예 사라지고,
  마스터 스위치 `enableInBuilds`의 기본값은 `false`다. 켜려면 **개발 빌드 + 스탠드얼론 타깃 +
  `enableInBuilds = true`** 셋이 동시에 필요하다. 우리는 이 셋 중 어느 것도 켜지 않으므로 기본
  상태로 둔다. 설정 원본은 `ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json`이며,
  값을 실제로 바꾸기 전에는 파일 자체가 생기지 않는다.
- **빌드가 만들었다 지우는 임시 에셋 둘이 있다.** `Assets/Settings/Pipeline/Resources/`의
  `RuntimePipelineConfig.asset`과 `RuntimePipelineBuildInfo.asset`은 빌드가 끝나며 지워지지만 빌드가
  중간에 죽으면 남는다. `.gitignore`에 넣어 두었다 — `git status`에 뜨면 지워도 된다.
- **에디터 서버는 루프백 전용이고 요청마다 베어러 토큰을 요구한다.** 포트는 디스크립터 파일
  (`Library/Pipeline/.unity-pipeline-port`)로 발견되며, 원격 접근 경로가 아니다. 다만 `eval` 호출은
  `Library/Pipeline/eval-usage.jsonl`에 기록되고, Unity 에디터 애널리틱스로 커맨드 실행이 집계된다.

## 갱신

CLI를 올리면 벤더링된 스킬 사본이 낡는다. **`unity self-update` 뒤에는 반드시 다시 렌더한다.**

```bash
unity self-update
unity skill refresh --yes          # 추적 중인 모든 설치본을 다시 렌더
git status                         # 두 사본의 변경을 함께 커밋
```

Pipeline 패키지는 따로 올린다. `manifest.json`이 바뀌므로 워크트리에서 하고 커밋한다.

```bash
unity pipeline upgrade
```

처음부터 다시 깔아야 할 때의 명령은 이렇다(모두 워크트리 루트에서).

```bash
unity pipeline install             # Packages/manifest.json에 com.unity.pipeline 추가
unity skill install claude-code --local --yes
unity skill install codex --local --yes
unity mcp configure codex --local --yes
# Claude Code는 .mcp.json을 직접 쓴다 — 위 「함정」의 이유
```
