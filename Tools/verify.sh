#!/usr/bin/env bash
# Fate Weaver 검증 진입점.
#
# 에이전트와 사람이 같은 명령 하나로 "지금 상태가 통과인가"를 얻는다.
# Unity 에디터 없이 도는 것만 넣는다 — Unity EditMode 배치는 AGENTS.md 「명령」 절 참고.
#
#   Tools/verify.sh            규칙 검사 + 헤드리스 테스트 + 노트북 테스트
#   Tools/verify.sh --quick    헤드리스 테스트만 (약 10초)
#   Tools/verify.sh --lint     규칙 검사만 (1초 미만)

set -uo pipefail

ROOT="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
cd "$ROOT"

GREP=/usr/bin/grep   # bare grep이 셸 알리아스로 실패하는 환경이 있어 절대 경로를 쓴다
MODE="${1:-full}"
FAILED=0

step() { printf '\n== %s\n' "$1"; }
fail() { printf 'FAIL  %s\n' "$1"; FAILED=1; }
ok()   { printf 'ok    %s\n' "$1"; }

# ---------------------------------------------------------------- 헤드리스 테스트
run_headless() {
  step "헤드리스 코어 테스트 (dotnet)"
  # 프로젝트 타깃은 net6.0이지만 로컬에 .NET 5 SDK만 있는 머신이 있어 있는 것으로 맞춘다.
  local tfm_arg=""
  if ! dotnet --list-sdks 2>/dev/null | "$GREP" -qE '^[6-9]\.|^[1-9][0-9]+\.'; then
    tfm_arg="-p:TargetFramework=net5.0"
    echo "(.NET 6+ SDK 없음 -> net5.0으로 실행)"
  fi
  if dotnet test Tests/Headless/FateWeaver.Tests.Headless.csproj $tfm_arg --nologo; then
    ok "헤드리스 테스트"
  else
    fail "헤드리스 테스트"
  fi
}

# ------------------------------------------------------------------ 노트북 테스트
run_notebook() {
  step "카드 저작 노트북 테스트 (node)"
  # 글로브가 필수다. 디렉터리를 주면 Node가 모듈 경로로 해석해 MODULE_NOT_FOUND로 죽는다.
  if node --test Tools/card-idea-notebook/*.test.mjs; then
    ok "노트북 테스트"
  else
    fail "노트북 테스트"
  fi
}

# -------------------------------------------------------------------- 규칙 검사
# AGENTS.md의 규칙 중 문자열로 판정 가능한 것만 기계 검사로 승격한 것이다.
# 새 검사를 넣을 때는 위반 0인 상태에서 넣는다 — 처음부터 빨간 게이트는 무시된다.
#
# 알려진 예외(Tools/lint-allow.txt)는 새 위반을 막되 기존 부채로 게이트가 처음부터 빨갛지 않게
# 한다. 항목을 지우는 것이 부채 상환이다 — 늘리는 쪽으로 쓰지 않는다.
ALLOW=Tools/lint-allow.txt

check() { # check <키> <이름> <패턴> <경로...>
  local key="$1"
  local name="$2"
  local pattern="$3"
  shift 3
  local hits
  hits=$("$GREP" -rnE "$pattern" --include=*.cs "$@" 2>/dev/null | "$GREP" -vE '/Tests?/' | "$GREP" -vE ':[[:space:]]*//' | "$GREP" -vE ':[[:space:]]*\*')
  if [ -n "${EXCLUDE:-}" ]; then
    hits=$(echo "$hits" | "$GREP" -vE "$EXCLUDE")
  fi
  local allowed
  allowed=$("$GREP" -E "^$key " "$ALLOW" 2>/dev/null | sed "s/^$key //" | sed 's/[[:space:]]*#.*$//')
  if [ -n "$allowed" ]; then
    hits=$(echo "$hits" | "$GREP" -vFf <(echo "$allowed"))
  fi
  if [ -n "$hits" ]; then
    fail "$name"
    echo "$hits" | sed 's/^/      /'
  else
    ok "$name"
  fi
}

run_lint() {
  step "AGENTS.md 규칙 검사"
  check R3 "규칙 3 — 런타임 문자열 탐색 금지" \
        'GameObject\.Find|FindObjectOfType|FindAnyObjectByType|FindObjectsByType|Resources\.Load' \
        Assets/Unity Assets/Core
  # public 프로퍼티(=>)·const·static·event는 규칙 4의 대상이 아니다. 잡는 것은 인스턴스 필드다.
  EXCLUDE='=>|[[:space:]]const[[:space:]]|[[:space:]]static[[:space:]]|[[:space:]]event[[:space:]]|[[:space:]]delegate[[:space:]]|[[:space:]]class[[:space:]]|[[:space:]]struct[[:space:]]|[[:space:]]enum[[:space:]]'
  check R4 "규칙 4 — public 필드 금지, [SerializeField] private을 쓴다" \
        '^[[:space:]]*public[[:space:]]+[A-Za-z_][A-Za-z0-9_<>,.[:space:]]*[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*[=;]' \
        Assets/Unity
  EXCLUDE=
  check R6 "규칙 6 — 코어는 UnityEngine을 참조하지 않는다" \
        'using[[:space:]]+UnityEngine|UnityEngine\.[A-Za-z]' \
        Assets/Core
}

case "$MODE" in
  --quick) run_headless ;;
  --lint)  run_lint ;;
  full|"") run_lint; run_headless; run_notebook ;;
  *) echo "사용법: Tools/verify.sh [--quick|--lint]" >&2; exit 2 ;;
esac

echo
if [ "$FAILED" -eq 0 ]; then
  echo "모두 통과"
else
  echo "실패 있음"
fi
exit "$FAILED"
