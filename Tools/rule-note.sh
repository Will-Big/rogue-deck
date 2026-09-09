#!/usr/bin/env bash
# 규칙 하나의 근거 절을 docs/agents/에서 찾아 표준출력으로 낸다.
#
# 훅이 규칙 위반을 막을 때 이것을 불러 근거를 배달한다. AGENTS.md에는 명령문과 우회 차단 한 줄만
# 남기고, "왜 이 규칙이 생겼나"는 세션이 규칙을 어기려 한 순간에만 나가게 하는 장치다.
#
# **이 파일은 근거를 담지 않는다.** 근거의 사본은 docs/agents/의 문서 하나뿐이고 여기는 그것을
# 찾아 자르기만 한다. 문구를 스크립트에 복사하면 규칙 하나가 두 자리로 갈라지고, 규칙이 바뀔 때
# 한쪽만 낡는다 — .claude/README.md가 경고한 "복사본은 조용히 썩는다"가 그대로 재현된다.
#
# 문서 쪽 계약: 근거 절의 제목은 `## 규칙 <번호> — <제목>` 꼴이어야 한다. 다음 `## `까지가 한 절이다.
# 이 계약과 절의 1:1 대응은 Tools/verify.sh --lint 의 R-doc 검사가 지킨다.
#
#   Tools/rule-note.sh 15        규칙 15의 근거 절
#   Tools/rule-note.sh 15 --path 절이 있는 파일 경로만

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GREP=/usr/bin/grep   # bare grep이 셸 알리아스로 실패하는 환경이 있어 절대 경로를 쓴다

RULE="${1:-}"
MODE="${2:-}"

case "$RULE" in
  ''|*[!0-9]*)
    echo "사용법: Tools/rule-note.sh <규칙 번호> [--path]" >&2
    exit 2
    ;;
esac

# 절을 가진 문서를 찾는다. 여러 문서가 같은 규칙을 담으면 그것 자체가 위반이므로 R-doc이 잡는다.
FILE="$("$GREP" -rl "^## 규칙 $RULE —" "$ROOT/docs/agents" --include='*.md' 2>/dev/null | head -1)"

if [ -z "$FILE" ]; then
  # 근거 절이 없는 규칙도 있다(문서로 내리지 않은 것). 훅을 세우지 않고 조용히 끝낸다.
  exit 1
fi

if [ "$MODE" = "--path" ]; then
  echo "${FILE#"$ROOT"/}"
  exit 0
fi

awk -v rule="$RULE" '
  $0 ~ "^## 규칙 " rule " —" { inside = 1; print; next }
  inside && /^## / { exit }
  inside { print }
' "$FILE"

echo
echo "(근거 원본: ${FILE#"$ROOT"/} — AGENTS.md 규칙 $RULE)"
