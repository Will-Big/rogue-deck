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

# 루트는 이 스크립트의 위치에서 구한다. git에게 묻지 않는다 — git 훅 안에서는 GIT_DIR이 설정돼
# 있어 `rev-parse --show-toplevel`이 워크트리가 아니라 메인 체크아웃을 가리킬 수 있고, 그러면
# 훅이 엉뚱한 트리를 검사하고 조용히 통과한다(2026-09-04 실측).
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
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

# 규칙 4는 줄 하나로 판정할 수 없다 — 예외인 중첩 [Serializable] DTO를 가리려면 중괄호 깊이라는
# 문맥이 필요하다. 그 판정은 Tools/lint-public-fields.awk에 있다.
check_public_fields() {
  local name="규칙 4 — public 필드 금지, [SerializeField] private을 쓴다"
  local hits
  hits=$(find Assets/Unity -name '*.cs' -exec awk -f Tools/lint-public-fields.awk {} + 2>/dev/null | "$GREP" -vE '/Tests?/')
  local allowed
  allowed=$("$GREP" -E "^R4 " "$ALLOW" 2>/dev/null | sed "s/^R4 //" | sed 's/[[:space:]]*#.*$//')
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

# 규칙 3이 남긴 위험을 좁힌다. 문자열로 부르는 Resources 경로는 컴파일러도 테스트도 검사하지
# 않아, 파일을 옮기거나 이름을 바꾸면 예외 없이 null이 돌아오고 화면에서만 티가 난다(폰트가
# 빠지면 한글이 네모로, 아이콘이 빠지면 그냥 안 보인다). 문자열과 파일이 어긋나는 순간 여기서
# 잡는다. 경로를 변수로 조립하는 호출은 잡지 못하며, 그런 경로는 인스펙터 참조로 옮기는 것이 답이다.
check_resource_paths() {
  local name="규칙 3 보조 — Resources 경로에 실제 파일이 있는가"
  local paths missing=""
  paths=$(
    {
      "$GREP" -rhoE 'Resources\.Load[A-Za-z]*(<[^>]*>)?\("[^"]+"' --include=*.cs Assets/Unity Assets/Core
      "$GREP" -rhoE 'ResourcePath[[:space:]]*=[[:space:]]*"[^"]+"' --include=*.cs Assets/Unity Assets/Core
    } 2>/dev/null | sed 's/.*"\([^"]*\)"$/\1/' | sort -u
  )
  local roots
  roots=$(find Assets -type d -name Resources)
  local p root f found
  while IFS= read -r p; do
    [ -n "$p" ] || continue
    found=0
    while IFS= read -r root; do
      [ -n "$root" ] || continue
      for f in "$root/$p".*; do
        [ -e "$f" ] || continue
        [ "${f##*.}" = "meta" ] && continue
        found=1
        break
      done
      [ "$found" -eq 1 ] && break
    done <<ROOTS
$roots
ROOTS
    [ "$found" -eq 1 ] || missing="$missing
      $p"
  done <<PATHS
$paths
PATHS
  if [ -n "$missing" ]; then
    fail "$name"
    echo "$missing"
    echo "      (Assets/**/Resources 아래에 해당 파일이 없다)"
  else
    ok "$name"
  fi
}

run_lint() {
  step "AGENTS.md 규칙 검사"
  check R3 "규칙 3 — 런타임 문자열 탐색 금지" \
        'GameObject\.Find|FindObjectOfType|FindAnyObjectByType|FindObjectsByType|Resources\.Load' \
        Assets/Unity Assets/Core
  check_resource_paths
  check_public_fields
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
