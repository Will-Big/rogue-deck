#!/bin/bash
# graphify 그래프 전체 재생성 파이프라인. 조정자 — 순서대로 호출만 한다 (규칙 30).
# 스펙: docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md §상세 3.
# 삭제 후 재생성인 이유: 증분 union 병합은 유령 노드를 쌓는다 (AGENTS.md 규칙 21).
set -euo pipefail
cd "$(cd "$(dirname "$0")/../.." && pwd)"

rm -f graphify-out/graph.json graphify-out/manifest.json
graphify update .
python3 Tools/graph/prune_graph.py
python3 Tools/graph/extract_card_graph.py
python3 Tools/graph/build_card_view.py
python3 Tools/graph/collapse_architecture.py

for view in view-card view-architecture; do
  graphify cluster-only "graphify-out/$view" --no-label
done
cp graphify-out/view-card/graphify-out/graph.html graphify-out/card-graph.html
cp graphify-out/view-architecture/graphify-out/graph.html graphify-out/architecture.html

echo "재생성 완료: graph.json + card-graph.html + architecture.html"
