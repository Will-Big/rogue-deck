#!/usr/bin/env bash
# 저장소를 처음 받았을 때 한 번 돌린다. 사람이든 어떤 AI 도구든 같다.
#
#   Tools/setup-dev.sh
#
# 하는 일은 둘뿐이다: 공유 git 훅을 켜고, 검증에 필요한 도구가 있는지 확인한다.
# 설치는 하지 않는다 — 무엇이 없는지 알려주고 끝낸다.

set -uo pipefail

ROOT="$(git -C "$(dirname "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
cd "$ROOT"

echo "== 공유 git 훅 활성화"
git config core.hooksPath .githooks
chmod +x .githooks/* 2>/dev/null
echo "   core.hooksPath = $(git config core.hooksPath)"
echo "   커밋할 때 .githooks/pre-commit이 바뀐 영역만 검증한다. 워크트리 전부에 함께 적용된다."

echo
echo "== 필요한 도구"
MISSING=0
have() {
  if command -v "$1" >/dev/null 2>&1; then
    printf '   ok    %-8s %s\n' "$1" "$($2 2>/dev/null | head -1)"
  else
    printf '   없음  %-8s %s\n' "$1" "$3"
    MISSING=1
  fi
}
have dotnet "dotnet --version" ".NET SDK — 헤드리스 코어 테스트에 필요 (5.0 이상이면 된다)"
have node "node --version" "Node — 카드 저작 노트북 테스트에 필요 (24 권장)"
have git "git --version" ""

echo
echo "   Unity 6000.5.2f1은 씬·프리팹·에셋을 건드릴 때만 필요하다. 게임 규칙 검증에는 없어도 된다."
echo "   Unity 배치 실행 방법: docs/agents/unity-batch-runs.md"

echo
echo "== 스모크 테스트 (규칙 검사, 1초 미만)"
Tools/verify.sh --lint
STATUS=$?

echo
echo "== 다음"
echo "   Tools/verify.sh          규칙 검사 + 헤드리스 + 노트북 (약 10초)"
echo "   AGENTS.md                반드시 지켜야 하는 규칙과 명령"
echo "   docs/superpowers/README.md  설계·계획 문서 색인"
[ "$MISSING" -eq 0 ] || echo
[ "$MISSING" -eq 0 ] || echo "   위에서 '없음'으로 나온 도구를 설치해야 전체 검증이 돈다."
exit "$STATUS"
