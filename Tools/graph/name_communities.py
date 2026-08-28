"""커뮤니티에 사람이 읽을 이름을 입힌다 — 번호가 아니라 내용(대표 심볼)에 키를 건다.

graphify의 .graphify_labels.json은 커뮤니티 번호에 묶여 있어, 삭제 후 재생성(규칙 21)마다
번호가 뒤섞이면 이름이 엉뚱한 커뮤니티에 붙는다(2026-08-28 실측 — 'Manifest'가 코드
254노드에 붙어 있었다). 그래서 이름의 원본을 저장소에 커밋되는 community-names.json
(대표 심볼 → 이름)으로 두고, 이 스크립트가 재생성 때마다 결정론적으로 다시 입힌다.
LLM 토큰 0. 매핑 미스는 대표 심볼 이름 그대로 남는다 — 그때 매핑에 항목을 추가한다.

두 모드:
  python3 Tools/graph/name_communities.py                # graphify-out/graph.json 명명 (in-place)
  python3 Tools/graph/name_communities.py --view <dir> <html>  # 뷰의 "Community N" 자리표시자를
                                                         # 그룹 대표 라벨로 치환 (json+html)
"""
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

GRAPH_PATH = Path("graphify-out/graph.json")
MAPPING_PATH = Path(__file__).resolve().parent / "community-names.json"

_MEMBER_COUNT_RE = re.compile(r"\s*\(\d+\)$")


def load_mapping(path=MAPPING_PATH):
    mapping = json.loads(Path(path).read_text(encoding="utf-8"))
    mapping.pop("_설명", None)
    return mapping


def degrees(graph):
    deg = Counter()
    for e in graph["links"]:
        deg[e["source"]] += 1
        deg[e["target"]] += 1
    return deg


def dominant_label(members, deg):
    """최고 연결 노드부터 훑어 '.'로 시작하지 않는 첫 라벨 — 커뮤니티의 대표 심볼."""
    for n in sorted(members, key=lambda n: -deg[n["id"]]):
        label = n.get("label") or ""
        if label and not label.startswith("."):
            return label
    return members[0].get("label", "") if members else ""


def apply_names(graph, mapping):
    """모든 커뮤니티의 community_name을 대표 심볼 기반으로 결정론 덮어쓰기.

    매핑 히트 → 큐레이션 이름, 미스 → 대표 심볼 자체. 낡은 번호 기반 이름을 남기지 않는다.
    """
    deg = degrees(graph)
    comms = defaultdict(list)
    for n in graph["nodes"]:
        if "community" in n:
            comms[n["community"]].append(n)
    renamed = 0
    for members in comms.values():
        sig = dominant_label(members, deg)
        name = mapping.get(sig, sig)
        for n in members:
            if n.get("community_name") != name:
                n["community_name"] = name
                renamed += 1
    return renamed


def rename_view(view_dir, html_path):
    """뷰 그래프의 "Community N" 자리표시자를 그룹 대표 라벨로 치환한다 (json+html 텍스트).

    아키텍처 뷰 노드 라벨의 "(멤버수)" 접미는 대표성 점수로 쓰고 이름에서는 뗀다.
    """
    json_path = Path(view_dir) / "graphify-out" / "graph.json"
    graph = json.loads(json_path.read_text(encoding="utf-8"))
    deg = degrees(graph)
    best = {}  # community id -> (score, label)
    for n in graph["nodes"]:
        if "community" not in n:
            continue
        label = n.get("label") or ""
        m = re.search(r"\((\d+)\)$", label)
        score = int(m.group(1)) if m else deg[n["id"]]
        cid = n["community"]
        if cid not in best or score > best[cid][0]:
            best[cid] = (score, _MEMBER_COUNT_RE.sub("", label))
    mapping = {f"Community {cid}": label for cid, (_, label) in best.items() if label}
    for path in (json_path, Path(html_path)):
        text = path.read_text(encoding="utf-8")
        for placeholder in sorted(mapping, key=len, reverse=True):  # "Community 10"을 "Community 1"보다 먼저
            text = text.replace(json.dumps(placeholder, ensure_ascii=False),
                                json.dumps(mapping[placeholder], ensure_ascii=False))
        path.write_text(text, encoding="utf-8")
    return len(mapping)


def main(argv):
    if argv[:1] == ["--view"]:
        named = rename_view(argv[1], argv[2])
        print(f"name_communities: 뷰 그룹 {named}개 명명 — {argv[2]}")
        return
    graph = json.loads(GRAPH_PATH.read_text(encoding="utf-8"))
    renamed = apply_names(graph, load_mapping())
    GRAPH_PATH.write_text(json.dumps(graph, ensure_ascii=False), encoding="utf-8")
    print(f"name_communities: 노드 {renamed}개의 커뮤니티 이름 갱신")


if __name__ == "__main__":
    main(sys.argv[1:])
