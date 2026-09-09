#!/usr/bin/env bash
# Stop 훅 — 코어 C#을 건드린 세션은 헤드리스 테스트를 통과해야 턴이 끝난다 (규칙 12).
#
# 왜 Stop인가: 편집마다 돌리면 10초 × 편집 횟수를 매번 물고, 문서만 고친 세션까지 붙잡는다.
# 턴이 끝나는 순간 한 번만, 그것도 Assets/Core의 .cs가 변했을 때만 돌린다.
#
# 여기서 규칙 18(워킹 트리를 깨끗이 남긴다)은 검사하지 않는다. 규칙 18은 "**세션**을 마칠 때"의
# 규칙인데 Stop 훅은 **턴**마다 돌고 둘을 구분할 수 없다. 작업 중인 턴에 트리가 더러운 것은
# 결함이 아니라 정상이라, 그것까지 막으면 게이트가 일상적으로 울려 곧 무시된다
# (Tools/verify.sh의 "처음부터 빨간 게이트는 무시된다"와 같은 실패다). 반대로 아래 테스트 실패는
# 턴 끝에 있으면 진짜 결함이다 — 깨진 코드를 두고 "다 됐습니다"라고 말하는 것을 막는 자리다.
#
# 종료 코드 규약: 0이면 그대로 끝, 2면 stderr를 근거로 턴을 이어가게 한다.

set -uo pipefail

INPUT="$(cat)"

# 훅이 이미 한 번 턴을 막아 이어가는 중이면 다시 막지 않는다 (무한 루프 방지).
case "$INPUT" in
  *'"stop_hook_active":true'*|*'"stop_hook_active": true'*) exit 0 ;;
esac

ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null)}"
[ -d "$ROOT" ] || exit 0
cd "$ROOT" || exit 0

# 커밋되지 않은 변경만 본다. 이미 커밋했다면 그 커밋 시점에 검증했다고 본다.
CHANGED="$(git status --porcelain -- Assets/Core 2>/dev/null | /usr/bin/grep -c '\.cs$')"
[ "${CHANGED:-0}" -gt 0 ] || exit 0

OUT="$(Tools/verify.sh --quick 2>&1)"
STATUS=$?
[ "$STATUS" -eq 0 ] && exit 0

{
  echo "코어 C#이 변경됐는데 헤드리스 테스트가 실패했다 (AGENTS.md 규칙 12). 고치고 다시 돌려라."
  echo "재현: Tools/verify.sh --quick"
  echo "$OUT" | tail -30
} >&2
exit 2
