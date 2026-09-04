# Unity MCP

**언제 읽나:** 실행 중인 Unity 에디터를 AI가 직접 조작하려 할 때, MCP 도구가 0개로 보일 때,
그리고 MCP가 **어느 체크아웃의 에디터에 붙는지**를 확인해야 할 때.

이 저장소는 Unity 공식 CLI(`unity`)가 내장한 MCP 서버를 쓴다. 서드파티 Unity MCP 패키지는 쓰지
않는다. 서버는 별도 프로세스가 아니라 `unity mcp` 서브커맨드이며, 붙어 있는 에디터가 노출하는
커맨드를 그대로 MCP 도구로 내보낸다.

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

**작업 디렉터리는 대상을 격리해 주지 않는다. 실행 중인 에디터가 하나뿐이면 cwd와 무관하게 그것에
붙는다.**

2026-09-04에 양방향으로 실측했다. 두 번 다 `editor_status`의 `projectPath`로 확인했다.

| 떠 있는 에디터 | `unity mcp`의 cwd | 실제로 붙은 곳 |
|---|---|---|
| 워크트리 | 메인 체크아웃 | **워크트리** (도구 149개) |
| 메인 체크아웃 | 워크트리 | **메인 체크아웃** |

상위 문서의 "cwd를 포함하는 프로젝트의 에디터를 고른다"는 서술은 **에디터가 여럿일 때 고르는
규칙**이지, 하나일 때의 안전장치가 아니다.

**아래쪽 행이 규칙 15와 부딪히는 지점이고, 그게 평소 상태다.** 열려 있는 에디터는 보통 메인
체크아웃(`/Users/ish/Git/rogue-deck`)의 것 하나뿐이다. 그러면 **워크트리에 갇혀 있다고 믿는 세션이
사용자 전용 체크아웃의 씬·프리팹·에셋을 고치게 되고, 그 세션의 `git status`에는 아무것도 안
뜬다** — 워크트리의 트리를 보고 있기 때문이다.

지켜야 할 것:

- **쓰기 도구를 부르기 전에 대상을 확인한다.** `unity status`로 어느 프로젝트의 에디터가 떠 있는지
  보거나, MCP `editor_status`의 `projectPath`를 읽는다. 확인 없이 `create_prefab`·`create_gameobject`·
  `delete_asset`·`eval` 같은 것을 부르지 않는다.
- **워크트리에서 저작하려면 그 워크트리에 에디터를 따로 띄운다**(아래). 메인 체크아웃 에디터가 함께
  떠 있게 되므로, 그때는 `--project-path`로 대상을 못박는다.
- 에디터가 둘 이상인데 cwd가 어느 쪽에도 안 맞으면 `unity command`는 `AMBIGUOUS_EDITOR`로 실패하고
  후보를 나열한다. **실패하는 편이 안전하다** — 이 동작을 우회하려 하지 말고 대상을 명시한다.
- 배치 실행과 마찬가지로, 끝나면 `git status`로 의도한 변경만 스테이징한다(규칙 17). 단 위와 같이
  **엉뚱한 체크아웃을 고친 경우 이 확인은 아무것도 못 잡는다.** 그래서 대상 확인이 먼저다.

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
