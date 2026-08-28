"""병합된 graph.json에서 카드 관계망 서브그래프를 뷰 디렉터리로 추린다.

스펙: docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md §상세 4.
결과는 graphify cluster-only가 렌더할 수 있도록 <view>/graphify-out/graph.json에 놓는다.
"""
import json
from pathlib import Path

GRAPH_PATH = Path("graphify-out/graph.json")
VIEW_GRAPH_PATH = Path("graphify-out/view-card/graphify-out/graph.json")
ORIGIN = "card_extractor"


def select_card_view(graph):
    ids = {n["id"] for n in graph["nodes"] if n.get("_origin") == ORIGIN}
    bridged = {e["target"] for e in graph["links"] if e.get("_origin") == ORIGIN}
    keep = ids | bridged
    return {"directed": False, "multigraph": False, "graph": {},
            "nodes": [n for n in graph["nodes"] if n["id"] in keep],
            "links": [e for e in graph["links"]
                      if e["source"] in keep and e["target"] in keep],
            "hyperedges": []}


def main():
    graph = json.loads(GRAPH_PATH.read_text(encoding="utf-8"))
    view = select_card_view(graph)
    VIEW_GRAPH_PATH.parent.mkdir(parents=True, exist_ok=True)
    VIEW_GRAPH_PATH.write_text(json.dumps(view, ensure_ascii=False), encoding="utf-8")
    print(f"build_card_view: 노드 {len(view['nodes'])}·엣지 {len(view['links'])}")


if __name__ == "__main__":
    main()
