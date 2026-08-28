"""콘텐츠 JSON을 graphify 그래프에 결정론적으로 편입시키는 추출기.

스펙: docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md §상세 1.
LLM 없이 동작한다. 태그(tags)는 사람이 쓰는 분류 메모라 읽되 무시한다(사용자 결정).
graphify merge-graphs는 노드에 repo:: 접두사를 강제해 다리가 끊기므로 직접 병합한다(§상세 3).
"""
import json
import re
import sys
from pathlib import Path

CONTENT_ROOT = Path("Assets/StreamingAssets/Content")
EFFECT_KEY_CS = Path("Assets/Core/Effects/EffectKey.cs")
EFFECTS_DIR = Path("Assets/Core/Effects")
STATUS_KEY_CS = Path("Assets/Core/Status/StatusKey.cs")
STATUS_DIR = Path("Assets/Core/Status")
GRAPH_PATH = Path("graphify-out/graph.json")
CARD_GRAPH_PATH = Path("graphify-out/card-graph.json")

ORIGIN = "card_extractor"
KNOWN_EFFECT_FIELDS = {"kind", "value", "selector", "status", "count", "target", "condition", "maxAmount", "damageBonusPerConsumed"}
STATUS_EDGE_BY_KIND = {
    "apply_status": "applies_status",
    "consume_status": "consumes_status",
    "trigger_status": "triggers_status",
}


def stderr_warn(msg):
    print(f"extract_card_graph: 경고 — {msg}", file=sys.stderr)


def parse_key_constants(cs_text, type_name):
    """'<type_name> Name = new <type_name>("id")' 정적 필드 → {Name: id}. 줄바꿈 허용."""
    pattern = type_name + r'\s+(\w+)\s*=\s*new\s+' + type_name + r'\("([^"]+)"\)'
    return dict(re.findall(pattern, cs_text))


def parse_key_owners(files, keys_class, field_to_id, warn):
    """'Key => <keys_class>.<Field>'를 가진 클래스 파일들 → {key_id: 클래스명}."""
    owners = {}
    for path in files:
        text = path.read_text(encoding="utf-8")
        cls = re.search(r'class\s+(\w+)', text)
        field = re.search(r'Key\s*=>\s*' + keys_class + r'\.(\w+)', text)
        if not (cls and field):
            continue
        key_id = field_to_id.get(field.group(1))
        if key_id is None:
            warn(f"{path.name}: {keys_class}.{field.group(1)}의 문자열 id를 찾지 못했다")
            continue
        owners[key_id] = cls.group(1)
    return owners


def find_code_node_id(graph, class_name):
    """AST 그래프에서 클래스 노드 id — label 정확 일치 + 파일명 일치 우선."""
    candidates = [n for n in graph["nodes"]
                  if n.get("label") == class_name and n.get("source_file")]
    for n in candidates:
        if n["source_file"].endswith("/" + class_name + ".cs"):
            return n["id"]
    return candidates[0]["id"] if candidates else None


def node(node_id, label, source_file):
    return {"id": node_id, "label": label, "file_type": "concept",
            "source_file": source_file, "source_location": "L1",
            "_origin": ORIGIN, "norm_label": label.lower()}


def edge(source, target, relation, source_file):
    return {"relation": relation, "confidence": "EXTRACTED", "confidence_score": 1.0,
            "weight": 1.0, "source": source, "target": target,
            "source_file": source_file, "source_location": "L1", "_origin": ORIGIN}


def load_json_dir(dir_path):
    """{경로: 파싱 결과} — 이름순 정렬로 결정론 유지. 폴더가 없으면 빈 dict."""
    if not dir_path.is_dir():
        return {}
    return {p: json.loads(p.read_text(encoding="utf-8"))
            for p in sorted(dir_path.glob("*.json"))}


def bridge_to_code(owner_by_key, key, source_id, ast_graph, links, rel_path, warn, kind_label):
    """source_id → (owner 클래스의 AST 노드) handled_by 엣지. 못 찾으면 경고만."""
    owner = owner_by_key.get(key)
    if owner is None:
        warn(f"{kind_label} '{key}'의 담당 클래스를 찾지 못했다")
        return
    code_id = find_code_node_id(ast_graph, owner)
    if code_id is None:
        warn(f"{kind_label} '{key}'의 담당 클래스 {owner}가 AST 그래프에 없다")
        return
    links.append(edge(source_id, code_id, "handled_by", rel_path))


def build(content_root, effect_keys, effect_owners, status_owners, ast_graph, warn):
    nodes, links = [], []
    known_kinds = set(effect_keys.values())

    status_ids = set()
    for path, data in load_json_dir(content_root / "Statuses").items():
        key = data["key"]
        status_ids.add(key)
        nodes.append(node(f"status:{key}", data.get("displayName", key), str(path)))
        bridge_to_code(status_owners, key, f"status:{key}", ast_graph, links,
                       str(path), warn, "상태")

    used_kinds = set()
    card_ids = set()
    for path, data in load_json_dir(content_root / "Cards").items():
        cid = data["id"]
        card_ids.add(cid)
        rel = str(path)
        nodes.append(node(f"card:{cid}", data.get("name", cid), rel))
        for eff in data.get("effects", []):
            unknown = set(eff) - KNOWN_EFFECT_FIELDS
            if unknown:
                warn(f"{rel}: 미인식 효과 필드 {sorted(unknown)} — 그래프에 반영되지 않는다")
            kind = eff.get("kind")
            if kind is None:
                warn(f"{rel}: kind 없는 효과")
                continue
            if known_kinds and kind not in known_kinds:
                warn(f"{rel}: EffectKeys에 없는 kind '{kind}'")
            used_kinds.add(kind)
            cond = eff.get("condition")
            if isinstance(cond, dict) and "status" in cond:
                warn(f"{rel}: condition이 상태를 참조한다 — 그래프에 엣지로 반영되지 않는다")
            links.append(edge(f"card:{cid}", f"effect_kind:{kind}", "uses_effect", rel))
            status_rel = STATUS_EDGE_BY_KIND.get(kind)
            if status_rel:
                skey = eff.get("status")
                if skey is None:
                    warn(f"{rel}: {kind} 효과에 status 필드가 없다")
                elif skey not in status_ids:
                    warn(f"{rel}: 존재하지 않는 상태 '{skey}' 참조")
                else:
                    links.append(edge(f"card:{cid}", f"status:{skey}", status_rel, rel))

    for kind in sorted(used_kinds):
        nodes.append(node(f"effect_kind:{kind}", kind, str(EFFECT_KEY_CS)))
        bridge_to_code(effect_owners, kind, f"effect_kind:{kind}", ast_graph, links,
                       str(EFFECT_KEY_CS), warn, "효과 kind")

    deck_ids = set()
    for folder, prefix in (("Pools", "pool"), ("Decks", "deck")):
        for path, data in load_json_dir(content_root / folder).items():
            oid = data["id"]
            rel = str(path)
            if prefix == "deck":
                deck_ids.add(oid)
            label = ("풀" if prefix == "pool" else "덱") + f" {oid}"
            nodes.append(node(f"{prefix}:{oid}", label, rel))
            for cid in data.get("cards", []):
                if cid not in card_ids:
                    warn(f"{rel}: 존재하지 않는 카드 '{cid}' 참조")
                    continue
                links.append(edge(f"{prefix}:{oid}", f"card:{cid}", "contains_card", rel))

    for path, data in load_json_dir(content_root / "Characters").items():
        cid = data["id"]
        rel = str(path)
        nodes.append(node(f"character:{cid}", data.get("displayName", cid), rel))
        deck = data.get("deck")
        if deck is None:
            continue
        if deck not in deck_ids:
            warn(f"{rel}: 존재하지 않는 덱 '{deck}' 참조")
            continue
        links.append(edge(f"character:{cid}", f"deck:{deck}", "owns_deck", rel))

    return nodes, links


def merge_into(graph, nodes, links):
    """재실행 멱등: 이전 card_extractor 산출물을 걷어내고 새로 넣는다."""
    graph["nodes"] = [n for n in graph["nodes"] if n.get("_origin") != ORIGIN] + nodes
    graph["links"] = [e for e in graph["links"] if e.get("_origin") != ORIGIN] + links


def main():
    ast_graph = json.loads(GRAPH_PATH.read_text(encoding="utf-8"))
    effect_keys = parse_key_constants(EFFECT_KEY_CS.read_text(encoding="utf-8"), "EffectKey")
    status_keys = parse_key_constants(STATUS_KEY_CS.read_text(encoding="utf-8"), "StatusKey")
    effect_owners = parse_key_owners(sorted(EFFECTS_DIR.glob("*Handler.cs")),
                                    "EffectKeys", effect_keys, stderr_warn)
    status_owners = parse_key_owners(sorted(STATUS_DIR.glob("*Behavior.cs")),
                                    "StatusKeys", status_keys, stderr_warn)
    nodes, links = build(CONTENT_ROOT, effect_keys, effect_owners, status_owners,
                         ast_graph, stderr_warn)
    card_graph = {"directed": False, "multigraph": False, "graph": {},
                  "nodes": nodes, "links": links, "hyperedges": []}
    CARD_GRAPH_PATH.write_text(json.dumps(card_graph, ensure_ascii=False), encoding="utf-8")
    merge_into(ast_graph, nodes, links)
    GRAPH_PATH.write_text(json.dumps(ast_graph, ensure_ascii=False), encoding="utf-8")
    print(f"extract_card_graph: 노드 {len(nodes)}·엣지 {len(links)}를 graph.json에 병합")


if __name__ == "__main__":
    main()
