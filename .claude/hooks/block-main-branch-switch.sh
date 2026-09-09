#!/usr/bin/env bash
# PreToolUse(Bash) 훅 — 메인 체크아웃에서 브랜치를 바꾸려는 명령을 도구 실행 직전에 막는다 (규칙 15).
#
# 왜 여기인가: git 훅은 커밋 시점에만 돌아 브랜치 전환을 볼 기회가 아예 없다. 지금 이 규칙을
# 지탱하는 것은 settings.json의 `ask` 프롬프트뿐인데, 그것은 클릭 한 번으로 통과한다 — 규칙 15가
# 막으려는 사고가 정확히 거기서 난다. 명령이 실행되기 전이 유일하게 잡을 수 있는 자리다.
#
# 왜 근거 문장이 여기 없나: 막을 때 나가는 문구는 Tools/rule-note.sh가 docs/agents/에서 잘라 온다.
# 근거를 이 파일에 복사하면 규칙 하나가 두 자리로 갈라지고 한쪽이 조용히 낡는다(.claude/README.md).
#
# 종료 코드 규약: 0이면 그대로 실행, 2면 stderr를 근거로 도구 실행을 막는다.
#
# 판정이 애매하면 통과시킨다. 오탐이 몇 번 쌓이면 사람은 훅 자체를 꺼 버리고, 그러면 진짜 위반도
# 같이 놓친다. 이 훅은 조용히 실패하는 쪽이 낫다.

set -uo pipefail

INPUT="$(cat)"

# JSON을 손으로 자르면 따옴표·이스케이프에서 틀리고, 틀린 결과는 오탐이 된다. 파서가 없으면 통과.
CMD=""
if command -v python3 >/dev/null 2>&1; then
  CMD="$(printf '%s' "$INPUT" | python3 -c '
import json, sys
try:
    d = json.load(sys.stdin)
except Exception:
    sys.exit(0)
if not isinstance(d, dict):
    sys.exit(0)
ti = d.get("tool_input") or {}
if isinstance(ti, dict):
    sys.stdout.write(str(ti.get("command") or ""))
' 2>/dev/null)"
elif command -v jq >/dev/null 2>&1; then
  CMD="$(printf '%s' "$INPUT" | jq -r '.tool_input.command // ""' 2>/dev/null)"
fi
[ -n "$CMD" ] || exit 0

ROOT="${CLAUDE_PROJECT_DIR:-$PWD}"
cd "$ROOT" 2>/dev/null || exit 0

# 명령이 스스로 디렉터리를 옮기면(`cd <경로>`, `git -C <경로>`) 판정 대상은 세션의 cwd가 아니라
# 그 경로다. 양방향으로 필요하다 — 워크트리에서 메인으로 들어가는 것도, 메인에서 워크트리로
# 나가는 것도 여기서 갈린다. 경로를 못 풀면 cwd로 되돌아간다.
TARGET_DIR="$ROOT"
set -f
NEXT_IS_DIR=0
# shellcheck disable=SC2086
for TOK in $(printf '%s' "$CMD" | tr ';|&\n' '    '); do
  if [ "$NEXT_IS_DIR" -eq 1 ]; then
    NEXT_IS_DIR=0
    P="$TOK"
    P="${P%\"}"; P="${P#\"}"; P="${P%\'}"; P="${P#\'}"
    RESOLVED="$(cd "$P" 2>/dev/null && pwd)" || RESOLVED=""
    [ -n "$RESOLVED" ] && TARGET_DIR="$RESOLVED"
    continue
  fi
  case "$TOK" in
    cd|-C) NEXT_IS_DIR=1 ;;
  esac
done
set +f

cd "$TARGET_DIR" 2>/dev/null || exit 0

# 메인 체크아웃은 두 경로가 같고, 링크된 워크트리는 갈린다(실측 확인).
GITDIR="$(git rev-parse --absolute-git-dir 2>/dev/null)" || exit 0
COMMON_RAW="$(git rev-parse --git-common-dir 2>/dev/null)" || exit 0
[ -n "$GITDIR" ] && [ -n "$COMMON_RAW" ] || exit 0
COMMON="$(cd "$COMMON_RAW" 2>/dev/null && pwd)" || exit 0
[ "$GITDIR" = "$COMMON" ] || exit 0

# 여기부터는 메인 체크아웃이 대상일 때만 돈다. 명령 안의 각 조각을 따로 본다.
BLOCK=0
set -f
while IFS= read -r SEG; do
  # shellcheck disable=SC2086
  set -- $SEG
  [ "$#" -gt 0 ] || continue
  [ "$1" = "git" ] || continue
  shift

  # git 전역 옵션을 건너뛰고 서브커맨드를 찾는다.
  SUB=""
  while [ "$#" -gt 0 ]; do
    case "$1" in
      -C|-c) shift 2 2>/dev/null || break ;;
      -*) shift ;;
      *) SUB="$1"; shift; break ;;
    esac
  done

  case "$SUB" in
    switch)
      # switch는 브랜치 전용 명령이라 전부 전환으로 본다(파일 복원은 restore가 맡는다).
      BLOCK=1
      break
      ;;
    checkout) ;;
    *) continue ;;
  esac

  # checkout은 전환과 파일 복원이 한 명령에 섞여 있다. 복원 형태를 먼저 걸러낸다.
  IS_SWITCH=1
  for ARG in "$@"; do
    case "$ARG" in
      --|-p|--patch) IS_SWITCH=0; break ;;          # 명시적 파일 복원 / 조각 복원
      -*) ;;                                        # -b, -f 등 플래그는 판정에 쓰지 않는다
      *) [ -e "$ARG" ] && { IS_SWITCH=0; break; } ;; # 실재하는 경로면 `--` 없는 파일 복원이다
    esac
  done
  [ "$IS_SWITCH" -eq 1 ] && { BLOCK=1; break; }
done <<EOF
$(printf '%s' "$CMD" | tr ';|&\n' '\n\n\n\n')
EOF
set +f

[ "$BLOCK" -eq 1 ] || exit 0

{
  echo "메인 체크아웃($TARGET_DIR)에서 브랜치를 전환하려 한다 — AGENTS.md 규칙 15."
  echo "막힌 명령: $CMD"
  echo
  "$ROOT/Tools/rule-note.sh" 15
} >&2
exit 2
