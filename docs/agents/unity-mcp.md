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

`unity mcp`는 인자가 없으면 **작업 디렉터리를 포함하는 프로젝트의 실행 중인 에디터**를 찾아 붙는다.
등록 파일(`.mcp.json`·`.codex/config.toml`)이 체크아웃마다 하나씩 따라다니므로, 워크트리에서 연
세션은 그 워크트리의 에디터를, 메인 체크아웃에서 연 세션은 메인 체크아웃의 에디터를 잡는다.

**여기가 규칙 15와 부딪히는 지점이다.** 메인 체크아웃(`/Users/ish/Git/rogue-deck`)은 사용자와 에디터
전용이고, 평소 열려 있는 에디터도 거기 하나뿐이다. 그러니 아무 준비 없이 워크트리 세션에서 MCP를
쓰면 붙을 에디터가 없고, 메인 체크아웃 세션에서 쓰면 **사용자 전용 체크아웃을 AI가 직접 고치게
된다.**

지켜야 할 것:

- **메인 체크아웃의 에디터에 MCP로 쓰기를 하지 않는다.** 읽기(`unity status`, 하이어라키 조회)는
  무해하지만, 씬·프리팹·에셋을 바꾸는 도구는 규칙 15 위반이다.
- 워크트리에서 저작하려면 **그 워크트리에 에디터를 따로 띄운다.** GUI로 열거나, 헤드리스 상주
  에디터를 쓴다(아래). 그 뒤 `--project-path`로 대상을 못박는다.
- 에디터가 둘 이상 떠 있으면 `--project-path` 없이 부른 커맨드는 `AMBIGUOUS_EDITOR`로 실패한다.
  실패하는 편이 안전하므로 이 동작에 기대되, **의도한 프로젝트를 항상 명시**한다.
- 배치 실행과 마찬가지로, 끝나면 `git status`로 의도한 변경만 스테이징한다(규칙 17).

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
- **`unity mcp configure claude-code`를 그냥 돌리지 않는다.** 이 클라이언트에 한해 CLI는 파일을
  쓰지 않고 `claude mcp add --scope user --transport stdio unity-editor-mcp unity mcp`를 대신 실행한다
  — 사용자 전역에 `unity-editor-mcp`라는 **다른 이름으로** 하나 더 붙어 도구가 중복된다. 이
  저장소는 프로젝트 스코프 `.mcp.json`에 `unity`라는 이름으로 등록해 두었으므로 그걸 쓴다.
- **등록 파일의 `command`는 절대 경로다**(`/Users/ish/.unity/bin/unity`). MCP 클라이언트가 로그인
  셸 없이 프로세스를 띄우는 경우가 있어 `~/.unity/env`가 PATH에 넣어 주는 것을 믿을 수 없기
  때문이다. 다른 머신에서 클론하면 `.mcp.json`과 `.codex/config.toml`의 이 경로 두 줄을 고친다.
- **Codex는 프로젝트를 trusted로 표시해야 `.codex/config.toml`을 읽는다.** 처음 열 때 뜨는 신뢰
  프롬프트를 승인하지 않으면 이 파일이 조용히 무시된다.
- **`com.unity.pipeline`은 실험 버전이다**(도입 시점 `0.6.0-exp.1`). 에디터에 임의 C#을 실행하는
  `eval` 계열 커맨드를 노출하므로, 릴리스 빌드에 들어갈 코드가 아니라 저작 도구로만 취급한다.

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
