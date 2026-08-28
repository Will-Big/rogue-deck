"""graphify graph.json에서 게임 로직 아키텍처와 무관한 잡음 노드를 걷어낸다.

스펙: docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md §상세 2.
제거 대상: 참조 스텁(소속 파일 없는 코드 노드), 테스트, 외부 패키지·플러그인, 도구 코드.
제거 규칙은 is_noise 한 곳에 모은다 — 새 잡음 유형이 나타나면 여기만 고친다.
"""
import json
import sys

NOISE_PREFIXES = ("Packages/", "Assets/Plugins/", "tools/", "Tools/", "Tests/")
NOISE_SUFFIXES = ("Tests.cs", "Test.cs")


def is_noise(node):
    if node.get("file_type") != "code":
        return False
    src = node.get("source_file") or ""
    if not src:
        return True  # 참조 스텁 — 실제 클래스 노드와 연결되지 않는 막다른 복제
    if "/Tests/" in src or src.endswith(NOISE_SUFFIXES):
        return True
    return src.startswith(NOISE_PREFIXES)


def prune(graph):
    removed = {n["id"] for n in graph["nodes"] if is_noise(n)}
    graph["nodes"] = [n for n in graph["nodes"] if n["id"] not in removed]
    graph["links"] = [e for e in graph["links"]
                      if e["source"] not in removed and e["target"] not in removed]
    graph["hyperedges"] = [h for h in graph.get("hyperedges", [])
                           if not (set(h.get("nodes", [])) & removed)]
    return len(removed)


def main(path="graphify-out/graph.json"):
    with open(path, encoding="utf-8") as f:
        graph = json.load(f)
    removed = prune(graph)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(graph, f, ensure_ascii=False)
    print(f"prune_graph: {removed}개 노드 제거, {len(graph['nodes'])}개 남음")


if __name__ == "__main__":
    main(*sys.argv[1:])
