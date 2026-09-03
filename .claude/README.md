# `.claude/` — Claude Code 어댑터

**여기에 원본은 없다.** 규칙은 `AGENTS.md`, 절차는 `docs/agents/`, 검증은 `Tools/verify.sh`와
`.githooks/`에 있고, 이 폴더는 Claude Code가 그것들을 자동으로 물어 오게 하는 껍데기다. 저장소를
Codex·Cursor·Gemini CLI 등으로 열면 이 폴더는 무시되고 나머지가 그대로 동작해야 한다 — 그래서
여기에 내용을 복사해 두지 않는다. 복사본은 조용히 썩는다.

| 경로 | 역할 | 원본 | 커밋 |
|---|---|---|---|
| `settings.json` | 권한 허용·확인 목록, Stop 훅 등록 | (Claude 전용) | O |
| `hooks/verify-core-on-stop.sh` | 코어 C#을 건드린 세션의 턴 종료를 헤드리스 테스트에 건다 | `Tools/verify.sh` | O |
| `skills/graphify-usage/` | 그래프 조회가 필요할 때 문서를 물어 온다 | `docs/agents/graphify-usage.md` | O |
| `skills/unity-batch-runs/` | Unity 배치·라이선싱 장애 때 문서를 물어 온다 | `docs/agents/unity-batch-runs.md` | O |
| `settings.local.json` | 개인 설정 | — | X (`.gitignore`) |
| `worktrees/` | 세션 워크트리 | — | X (`.gitignore`) |

## 왜 이렇게 나눴나

**권한 목록.** 되돌릴 것이 없는 명령(테스트·검사·읽기 전용 git)만 미리 허용한다. 열 번째 승인부터
사람은 검토를 그만두고 클릭만 하게 되므로, 승인 프롬프트는 실제로 판단이 필요한 곳에만 남긴다.
`git push`·`git merge`·`git checkout`은 허용이 아니라 `ask`에 둔다 — 규칙 15(메인 체크아웃의 브랜치
전환 금지)와 규칙 19(머지는 사용자 승인 후)를 문서가 아니라 프롬프트로 강제하는 자리다. 이 층에는
도구 간 표준이 없어 이식되지 않는다. 다른 도구에서 같은 것을 원하면 그 도구의 설정에서 따로 해야
한다.

**Stop 훅과 pre-commit 훅은 잡는 순간이 다르다.** Stop 훅은 **턴이 끝날 때** 검사한다 — 에이전트가
깨진 코드를 두고 "다 됐습니다"라고 말하는 것을 막는 자리이며, 커밋을 하지 않는 세션에서도 걸린다.
`.githooks/pre-commit`은 **커밋할 때** 검사하고, 어떤 도구로 작업하든 걸린다. 겹치는 만큼 같은
테스트를 두 번 돌 수 있지만 10초짜리라 그대로 둔다. 진짜 게이트는 우회할 수 없는 CI다.

**스킬은 포인터다.** AGENTS.md는 매 세션 전량 로드되므로, 세션 대부분에서 쓰지 않는 상황 지식을
거기 두면 정작 지켜야 할 규칙이 묻힌다. 그래서 판단 기준만 규칙에 남기고 절차는 `docs/agents/`로
내렸다. Claude Code는 스킬로 그것을 필요할 때 찾아 오고, 다른 도구는 AGENTS.md의 링크를 따라간다.
