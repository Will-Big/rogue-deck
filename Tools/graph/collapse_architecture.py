"""프루닝된 그래프의 게임 로직 코드를 커뮤니티 단위 노드로 접는다.

스펙: docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md §상세 5.
문서·개념 노드는 접지 않는다 — 아키텍처 뷰는 코드만 담는다.
"""
import json
from collections import Counter
from pathlib import Path

GRAPH_PATH = Path("graphify-out/graph.json")
VIEW_GRAPH_PATH = Path("graphify-out/view-architecture/graphify-out/graph.json")
ORIGIN = "architecture_collapse"


def collapse(graph):
    community_of, name_of, members = {}, {}, Counter()
    for n in graph["nodes"]:
        if n.get("file_type") != "code" or "community" not in n:
            continue
        cid = n["community"]
        community_of[n["id"]] = cid
        members[cid] += 1
        name_of.setdefault(cid, n.get("community_name") or f"Community {cid}")

    weights = Counter()
    for e in graph["links"]:
        s = community_of.get(e["source"])
        t = community_of.get(e["target"])
        if s is None or t is None or s == t:
            continue
        weights[tuple(sorted((s, t)))] += 1

    nodes = [{"id": f"community:{cid}", "label": f"{name_of[cid]} ({members[cid]})",
              "file_type": "concept", "source_file": "", "source_location": "",
              "_origin": ORIGIN} for cid in sorted(members)]
    links = [{"relation": "connected", "confidence": "EXTRACTED", "confidence_score": 1.0,
              "weight": w, "source": f"community:{a}", "target": f"community:{b}",
              "source_file": "", "source_location": "", "_origin": ORIGIN}
             for (a, b), w in sorted(weights.items())]
    return {"directed": False, "multigraph": False, "graph": {},
            "nodes": nodes, "links": links, "hyperedges": []}


def main():
    graph = json.loads(GRAPH_PATH.read_text(encoding="utf-8"))
    arch = collapse(graph)
    VIEW_GRAPH_PATH.parent.mkdir(parents=True, exist_ok=True)
    VIEW_GRAPH_PATH.write_text(json.dumps(arch, ensure_ascii=False), encoding="utf-8")
    print(f"collapse_architecture: 커뮤니티 {len(arch['nodes'])}·연결 {len(arch['links'])}")


if __name__ == "__main__":
    main()
