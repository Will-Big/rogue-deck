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

# 규칙 번호는 AGENTS.md 한 곳에서만 정의되고, 훅·도구·근거 문서·코어 주석은 그것을 번호로만
# 가리킨다. 그 인용은 컴파일러도 테스트도 건드리지 않아, 규칙을 지우거나 번호를 옮겨도 아무 일이
# 일어나지 않는다 — 규칙 21이 낡은 그래프에서 겪은 실패와 같다. 낡은 참조는 침묵하지 않고 틀린
# 것을 확신 있게 말한다. 근거를 AGENTS.md 밖으로 내보낸 이상 분산을 붙들어 두는 것은 이 검사뿐이다.
#
# docs/superpowers/.archive/는 보지 않는다. 폐기된 문서의 규칙 번호는 작성 당시 기준이며 지금
# AGENTS.md와 어긋나는 것이 정상이다 — 그것까지 잡으면 고칠 수 없는 위반으로 게이트가 상시 빨갛다.
RULE_CITE_DIRS=(.githooks .claude Tools docs/agents Assets)

check_rule_docs() {
  local name="R-doc — 규칙 번호 인용과 근거 문서가 AGENTS.md와 맞는가"
  local problems=""

  # AGENTS.md의 규칙 정의는 줄 시작의 "N. **" 꼴이다. 이 목록이 유일한 원본이다.
  # awk에 넘기므로 한 줄로 편다 — BSD awk의 -v는 값에 개행이 들어가면 그 자리에서 죽는다.
  local rules
  rules=$("$GREP" -oE '^[0-9]+\.[[:space:]]+\*\*' AGENTS.md | "$GREP" -oE '^[0-9]+' | sort -un | tr '\n' ' ')

  # (1) 인용된 번호가 실재하는가. "규칙 15·17"처럼 묶인 인용은 낱개로 쪼개 하나씩 본다.
  #     -I로 바이너리를 건너뛰고, 워크트리는 남의 체크아웃이라 보지 않는다.
  local bad_cites
  bad_cites=$(
    "$GREP" -rInoE '규칙 [0-9]+([·,~][0-9]+)*|[Rr]ule [0-9]+' \
      --exclude-dir=worktrees --exclude-dir=.archive \
      "${RULE_CITE_DIRS[@]}" 2>/dev/null \
    | awk -v rules="$rules" '
        BEGIN { n = split(rules, a, " "); for (i = 1; i <= n; i++) have[a[i] + 0] = 1 }
        {
          if (!match($0, /^[^:]+:[0-9]+:/)) next
          loc = substr($0, 1, RLENGTH - 1)
          rest = substr($0, RLENGTH + 1)
          gsub(/[^0-9]/, " ", rest)
          m = split(rest, nums, " ")
          for (i = 1; i <= m; i++)
            if (!have[nums[i] + 0]) print loc "  -> 규칙 " nums[i] " 는 AGENTS.md에 없다"
        }' | sort -u
  )
  [ -n "$bad_cites" ] && problems="$problems$bad_cites
"

  # (2) AGENTS.md가 건 docs/agents 링크가 실제 파일로 풀리는가. 규칙 본문을 문서로 내린 뒤로는
  #     이 링크가 끊기면 규칙이 근거 없이 남는다.
  local link
  while IFS= read -r link; do
    [ -n "$link" ] || continue
    [ -f "$link" ] || problems="$problems AGENTS.md -> $link  (그런 파일이 없다)
"
  done < <("$GREP" -oE 'docs/agents/[A-Za-z0-9._-]+\.md' AGENTS.md | sort -u)

  # (3) 근거 절과 규칙이 1:1인가. 훅이 "## 규칙 N " 제목으로 절을 잘라 내므로, 같은 번호가 두
  #     문서에 있으면 어느 쪽이 나갈지 정해지지 않고, 없는 번호의 절은 아무도 읽지 않는다.
  local bad_sections
  bad_sections=$(
    "$GREP" -nE '^## 규칙 [0-9]+[[:space:]]' docs/agents/*.md 2>/dev/null \
    | awk -v rules="$rules" '
        BEGIN { n = split(rules, a, " "); for (i = 1; i <= n; i++) have[a[i] + 0] = 1 }
        {
          if (!match($0, /^[^:]+:[0-9]+:/)) next
          loc = substr($0, 1, RLENGTH - 1)
          rest = substr($0, RLENGTH + 1)
          sub(/^## 규칙 /, "", rest)
          num = rest + 0
          if (!have[num]) print loc "  -> 규칙 " num " 절인데 AGENTS.md에 그 규칙이 없다"
          else if (num in seen) print loc "  -> 규칙 " num " 절이 " seen[num] " 와 겹친다"
          else seen[num] = loc
        }'
  )
  [ -n "$bad_sections" ] && problems="$problems$bad_sections
"

  if [ -n "$(echo "$problems" | tr -d '[:space:]')" ]; then
    fail "$name"
    echo "$problems" | "$GREP" -v '^[[:space:]]*$' | sed 's/^[[:space:]]*/      /'
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
  # 계획 3b·3c가 지운 SO 저작 타입이다. 이름이 다시 나타나면 카드 원본이 JSON 하나라는 규칙 5의
  # 전제가 조용히 깨진다 — 지운 파이프라인은 되살리기가 쉽고, 되살아난 뒤에는 티가 안 난다.
  check R5 "규칙 5 — 지워진 SO 저작 타입을 되살리지 않는다" \
        'CardAsset|CardPoolAsset|DeckAsset' \
        Assets
  # 무인자 생성·시각·GUID만 잡는다. `new Random(seed)`까지 잡으면 CombatState·RunState의 시드
  # RNG가 걸리는데 그 둘은 위반이 아니라 규칙 7이 요구하는 물건 자체다. 예외 목록으로 빼면
  # "부채 목록"에 영구히 옳은 항목이 섞여 목록의 뜻이 망가지므로, 패턴을 좁히는 쪽으로 푼다.
  check R7 "규칙 7 — 결정론: 즉석 Random·DateTime·Guid 금지" \
        'new[[:space:]]+(System\.)?Random\(\)|DateTime\.(Now|UtcNow|Today)|Guid\.NewGuid' \
        Assets/Core
  check R6 "규칙 6 — 코어는 UnityEngine을 참조하지 않는다" \
        'using[[:space:]]+UnityEngine|UnityEngine\.[A-Za-z]' \
        Assets/Core
  check_rule_docs
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
