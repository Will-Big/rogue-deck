# graphify 카드 그래프 통합 구현 계획

- 작성일: 2026-08-28
- 상태: archived — 2026-08-28 구현 완료
- 범위: [graphify 카드 그래프 통합 설계](../specs/2026-08-28-graphify-card-graph-design.md)의 구현 — 프루너, 카드 추출기, 뷰 2장, 재생성 스크립트, AGENTS.md 규칙 개정

## 설계 개요 (사람 검수용)

이 절만 읽고 실행 구조를 승인할 수 있어야 한다. 아래 `## 상세` 이후는 세션 인계용이며 사람은
읽지 않아도 된다. 무엇을 왜 만드는지는 스펙의 개요 절이 권위이고, 이 절은 **어떤 순서로
무엇이 만들어지고 각 단계를 무엇으로 믿을 수 있는지**만 답한다.

- **무엇을 만드나** — 도구 스크립트 5개를 태스크 5개로 만든다. C#·Unity·게임 콘텐츠는 한 줄도
  건드리지 않는다. 전 과정 LLM 토큰 0, 외부 패키지 추가 없음.

- **구조** — 태스크 순서대로:

  | 태스크 | 만드는 것 (한 줄) | 이 태스크가 모르는 것 |
  |---|---|---|
  | 1. 프루너 | 그래프에서 스텁·테스트·외부·도구 노드를 걷어낸다 | 카드 JSON, 뷰 |
  | 2. 추출기 | 카드·상태 JSON을 읽어 그래프에 관계 엣지로 병합한다 | 프루닝 규칙, 뷰 |
  | 3. 뷰 서브그래프 | 카드 관계망과 커뮤니티 접기 그래프를 추려낸다 | JSON 원본, 렌더 방식 |
  | 4. 재생성 스크립트 | 1~3과 graphify를 순서대로 호출만 한다 | 각 단계의 내부 |
  | 5. 규칙 개정 | AGENTS.md 21·22와 문서 색인을 새 명령 기준으로 고친다 | — (문서) |

- **의존 방향** — `태스크 4 → (graphify → 1 → 2 → 3)`, 5는 4의 결과 경로만 인용.
  1과 2는 서로 독립이라 순서를 바꿔도 된다.

- **각 단계를 무엇으로 믿나** — 태스크 1~3은 합성 그래프 단위 테스트(총 ~21개)가 먼저 실패하고
  구현 후 통과한다. 태스크 4는 실물 저장소에서 스펙의 수용 기준 6개(카드 엣지 존재, explain·path
  응답, 잡음 0, HTML 2장, ~15초)를 일괄 검증한다. 태스크 1·2는 실물 그래프 수치가 스펙 실측치와
  맞는지도 대조한다.

- **사람 몫** — HTML 2장의 시각 품질 평가와, 카드 관계망이 게임 지식과 맞는지의 내용 검수.
  그리고 완료 후 워크트리의 master 머지 승인.

- **대안과 기각 이유** — 스펙 개요와 같다(유료 추출 기각, merge-graphs 기각·직접 병합).
  계획 수준의 갈림길은 없다 — 렌더 경로·병합 방식 모두 계획 전에 실증으로 닫았다.

- **이 선택으로 나중에 어려워지는 것** — 스펙 개요의 세 가지(포맷 추종, 파싱 규약 종속,
  미인식 필드 침묵)에 더해: 추출기의 경고가 stderr로만 나가므로 재생성을 자동화된 곳에서
  돌리면 경고를 읽는 사람이 없다 — 수동 명령 하나로 유지하는 현 결정과 묶여 있는 약점이다.

---

## 상세 (세션 인계용)

위 `## 설계 개요`에 이 문서의 구조 요약이 있다. 실행 근거는 이 절 이후에만 있다.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 카드·상태·풀·덱·캐릭터 JSON을 graphify 그래프에 결정론적으로 편입시키고, 잡음(스텁·테스트·외부 패키지·도구)을 걷어내며, 재생성을 명령 하나로 묶고, 사람용 시각화 2장을 만든다.

**Architecture:** stdlib 전용 Python 스크립트 4개(`prune_graph`, `extract_card_graph`, `build_card_view`, `collapse_architecture`)와 조정자 셸 스크립트 1개. graphify CLI는 AST 재생성(`update`)과 뷰 렌더(`cluster-only --no-label`)에만 쓴다. `merge-graphs`는 쓰지 않는다(노드에 `repo::` 접두사를 강제해 다리가 끊김 — 스펙 §상세 3 실증).

**Tech Stack:** Python 3 표준 라이브러리만 (`json`, `re`, `pathlib`, `unittest`, `collections`). graphify CLI (이미 설치됨). 의존성 추가 금지 (AGENTS.md 규칙 14).

**권위 스펙:** `docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md` — 실행 근거는 §상세.

## Global Constraints

- **LLM 토큰 0.** 어떤 단계도 유료 시맨틱 추출(`/graphify --update`)을 호출하지 않는다.
- **stdlib 전용.** pip 설치 불가 (AGENTS.md 규칙 14).
- **태그(`tags[]`) 제외.** 읽되 노드·엣지를 만들지 않는다 (스펙 §설계 개요, 사용자 결정).
- **모든 엣지는 `confidence: "EXTRACTED"`, `confidence_score: 1.0`.**
- **커밋 메시지는 한국어** — `타입(범위): …한다` (AGENTS.md 규칙 27). 이 계획은 `feat(tools):`·`test(tools):`·`docs:`를 쓴다.
- **작업은 전용 워크트리에서** (AGENTS.md 규칙 15 — 코드 파일 추가이므로 master 직접 커밋 불가). 실행 시작 시 superpowers:using-git-worktrees로 워크트리를 만든다.
- **테스트 실행:** 저장소 루트에서 `python3 -m unittest discover -s Tools/graph -v` (dotnet 테스트와 무관 — 규칙 12의 헤드리스 경로는 이 계획에 해당 없음).
- graph.json 포맷(스펙 §상세 "graphify graph.json 포맷" 실측): 최상위 `{directed, multigraph, graph, nodes, links, hyperedges}`. 노드 필수 필드 `id, label, file_type, source_file, source_location, _origin`. 엣지 필수 필드 `relation, confidence, confidence_score, weight, source, target, source_file, source_location, _origin`.

---

### Task 1: 그래프 프루너

**Files:**
- Create: `Tools/graph/prune_graph.py`
- Test: `Tools/graph/test_prune_graph.py`

**Interfaces:**
- Produces: `is_noise(node: dict) -> bool`, `prune(graph: dict) -> int` (제거한 노드 수 반환, graph를 in-place 수정), CLI `python3 Tools/graph/prune_graph.py [graph_path]` (기본 `graphify-out/graph.json`, in-place).
- Task 3의 rebuild 스크립트와 Task 5의 접기가 프루닝된 graph.json을 전제한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`Tools/graph/test_prune_graph.py`:

```python
import unittest

from prune_graph import is_noise, prune


def node(nid, file_type="code", source_file=""):
    return {"id": nid, "label": nid, "file_type": file_type,
            "source_file": source_file, "source_location": "L1", "_origin": "ast"}


def edge(s, t):
    return {"relation": "calls", "confidence": "EXTRACTED", "confidence_score": 1.0,
            "weight": 1.0, "source": s, "target": t,
            "source_file": "", "source_location": "", "_origin": "ast"}


class IsNoiseTests(unittest.TestCase):
    def test_소스_없는_코드_노드는_스텁이다(self):
        self.assertTrue(is_noise(node("stub")))

    def test_테스트_경로와_테스트_파일명은_잡음이다(self):
        self.assertTrue(is_noise(node("t1", source_file="Assets/Core/Tests/EditMode/StatusTests.cs")))
        self.assertTrue(is_noise(node("t2", source_file="Assets/Unity/CardViewTests.cs")))
        self.assertTrue(is_noise(node("t3", source_file="Assets/Unity/SmokeTest.cs")))

    def test_외부_패키지와_플러그인은_잡음이다(self):
        self.assertTrue(is_noise(node("p1", source_file="Packages/packages-lock.json")))
        self.assertTrue(is_noise(node("p2", source_file="Assets/Plugins/Demigiant/DOTween/DOTweenModuleUI.cs")))

    def test_도구_코드는_잡음이다(self):
        self.assertTrue(is_noise(node("g1", source_file="Tools/graph/prune_graph.py")))
        self.assertTrue(is_noise(node("g2", source_file="Tools/Something/Foo.cs")))

    def test_게임_로직과_문서는_남는다(self):
        self.assertFalse(is_noise(node("core", source_file="Assets/Core/Combat/CombatState.cs")))
        self.assertFalse(is_noise(node("ui", source_file="Assets/Unity/CardView.cs")))
        self.assertFalse(is_noise(node("doc", file_type="document")))  # 문서는 source_file이 비어도 남는다


class PruneTests(unittest.TestCase):
    def test_잡음_노드와_딸린_엣지를_함께_걷어낸다(self):
        graph = {
            "nodes": [node("keep", source_file="Assets/Core/A.cs"),
                      node("stub"),
                      node("test", source_file="Assets/Core/Tests/T.cs")],
            "links": [edge("keep", "stub"), edge("keep", "keep"), edge("test", "keep")],
            "hyperedges": [{"nodes": ["keep", "stub"]}, {"nodes": ["keep"]}],
        }
        removed = prune(graph)
        self.assertEqual(removed, 2)
        self.assertEqual([n["id"] for n in graph["nodes"]], ["keep"])
        self.assertEqual(len(graph["links"]), 1)
        self.assertEqual(graph["hyperedges"], [{"nodes": ["keep"]}])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: 실패 확인**

Run: `python3 -m unittest discover -s Tools/graph -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'prune_graph'`

- [ ] **Step 3: 구현**

`Tools/graph/prune_graph.py`:

```python
"""graphify graph.json에서 게임 로직 아키텍처와 무관한 잡음 노드를 걷어낸다.

스펙: docs/superpowers/specs/2026-08-28-graphify-card-graph-design.md §상세 2.
제거 대상: 참조 스텁(소속 파일 없는 코드 노드), 테스트, 외부 패키지·플러그인, 도구 코드.
제거 규칙은 is_noise 한 곳에 모은다 — 새 잡음 유형이 나타나면 여기만 고친다.
"""
import json
import sys

NOISE_PREFIXES = ("Packages/", "Assets/Plugins/", "tools/", "Tools/")
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
```

- [ ] **Step 4: 통과 확인**

Run: `python3 -m unittest discover -s Tools/graph -v`
Expected: PASS (테스트 7개)

- [ ] **Step 5: 실물 검증** — 실제 그래프에 돌려 스펙 실측치와 대조:

```bash
rm -f graphify-out/graph.json graphify-out/manifest.json && graphify update . && python3 Tools/graph/prune_graph.py
```

Expected: `prune_graph: 24xx개 노드 제거, 30xx개 남음` — 제거 수는 스텁 751 + 테스트 1,196 + 외부 531 + 도구 ~27 ≈ 2,505 부근이어야 한다. 1,000 이상 벗어나면 규칙 오류이니 멈추고 분류를 다시 확인한다.

- [ ] **Step 6: 커밋**

```bash
git add Tools/graph/prune_graph.py Tools/graph/test_prune_graph.py
git commit -m "feat(tools): graphify 그래프에서 스텁·테스트·외부 노드를 걷어내는 프루너를 추가한다"
```

---

### Task 2: 카드 그래프 추출기

**Files:**
- Create: `Tools/graph/extract_card_graph.py`
- Test: `Tools/graph/test_extract_card_graph.py`

**Interfaces:**
- Consumes: 없음 (Task 1과 독립 — 파이프라인에서만 프루닝 이후에 실행될 뿐).
- Produces:
  - `parse_key_constants(cs_text: str, type_name: str) -> dict` — `{"Damage": "damage", ...}`
  - `parse_key_owners(files: list[Path], keys_class: str, field_to_id: dict, warn) -> dict` — `{"damage": "DamageHandler", ...}`
  - `find_code_node_id(graph: dict, class_name: str) -> str | None`
  - `build(content_root: Path, effect_keys: dict, effect_owners: dict, status_owners: dict, ast_graph: dict, warn) -> (list, list)` — (nodes, links)
  - `merge_into(graph: dict, nodes: list, links: list) -> None` — `_origin="card_extractor"` 기존 산출물 제거 후 추가(멱등)
  - CLI `python3 Tools/graph/extract_card_graph.py` — `graphify-out/card-graph.json`을 쓰고 `graphify-out/graph.json`에 병합
- 노드 ID 규약 (Task 4가 의존): `card:<id>`, `status:<key>`, `pool:<id>`, `deck:<id>`, `character:<id>`, `effect_kind:<kind>`. `_origin`은 `"card_extractor"`.
- 엣지 relation (Task 4·수용 기준이 의존): `applies_status`, `consumes_status`, `triggers_status`, `uses_effect`, `contains_card`, `owns_deck`, `handled_by`.

- [ ] **Step 1: 실패하는 테스트 작성**

`Tools/graph/test_extract_card_graph.py`:

```python
import json
import tempfile
import unittest
from pathlib import Path

from extract_card_graph import (
    build, find_code_node_id, merge_into, parse_key_constants, parse_key_owners,
)

EFFECT_KEY_CS = '''
public static class EffectKeys
{
    public static readonly EffectKey Damage = new EffectKey("damage");
    public static readonly EffectKey ApplyStatus = new EffectKey("apply_status");
    public static readonly EffectKey NullifyNextPlayerConditionReward =
        new EffectKey("nullify_next_player_condition_reward");
}
'''

HANDLER_CS = '''
public sealed class DamageHandler : IEffectHandler
{
    public EffectKey Key => EffectKeys.Damage;
}
'''


def ast_node(nid, label, source_file):
    return {"id": nid, "label": label, "file_type": "code",
            "source_file": source_file, "source_location": "L1", "_origin": "ast"}


class ParseTests(unittest.TestCase):
    def test_키_상수를_줄바꿈_포함해_파싱한다(self):
        keys = parse_key_constants(EFFECT_KEY_CS, "EffectKey")
        self.assertEqual(keys["Damage"], "damage")
        self.assertEqual(keys["NullifyNextPlayerConditionReward"],
                         "nullify_next_player_condition_reward")

    def test_핸들러_소유_클래스를_파싱한다(self):
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "DamageHandler.cs"
            p.write_text(HANDLER_CS, encoding="utf-8")
            owners = parse_key_owners([p], "EffectKeys",
                                      {"Damage": "damage"}, warn=lambda m: None)
        self.assertEqual(owners, {"damage": "DamageHandler"})


class FindCodeNodeTests(unittest.TestCase):
    def test_라벨과_파일명이_맞는_노드를_고른다(self):
        graph = {"nodes": [
            ast_node("wrong", "DamageHandler", "Assets/Core/Other.cs"),
            ast_node("right", "DamageHandler", "Assets/Core/Effects/DamageHandler.cs"),
        ]}
        self.assertEqual(find_code_node_id(graph, "DamageHandler"), "right")

    def test_없으면_None(self):
        self.assertIsNone(find_code_node_id({"nodes": []}, "Nope"))


def write_content(root):
    """합성 콘텐츠: 카드 2, 상태 1, 풀 1, 덱 1, 캐릭터 1."""
    for d in ("Cards", "Statuses", "Pools", "Decks", "Characters"):
        (root / d).mkdir(parents=True)
    (root / "Cards" / "venom.json").write_text(json.dumps({
        "id": "venom", "name": "맹독", "effects": [
            {"kind": "damage", "value": 2, "selector": "FrontOne"},
            {"kind": "apply_status", "status": "poison", "count": 1,
             "target": "TargetEnemy", "selector": "FrontOne"},
        ], "tags": ["독"]}), encoding="utf-8")
    (root / "Cards" / "odd.json").write_text(json.dumps({
        "id": "odd", "name": "이상한", "effects": [
            {"kind": "apply_status", "status": "ghost"},
            {"kind": "damage", "mystery_field": 1},
        ]}), encoding="utf-8")
    (root / "Statuses" / "poison.json").write_text(json.dumps(
        {"key": "poison", "displayName": "독"}), encoding="utf-8")
    (root / "Pools" / "p1.json").write_text(json.dumps(
        {"id": "p1", "cards": ["venom", "missing_card"]}), encoding="utf-8")
    (root / "Decks" / "d1.json").write_text(json.dumps(
        {"id": "d1", "cards": ["venom"]}), encoding="utf-8")
    (root / "Characters" / "c1.json").write_text(json.dumps(
        {"id": "c1", "displayName": "파티원", "deck": "d1"}), encoding="utf-8")


class BuildTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        write_content(root)
        self.warnings = []
        ast_graph = {"nodes": [
            ast_node("h_damage", "DamageHandler", "Assets/Core/Effects/DamageHandler.cs"),
            ast_node("b_poison", "PoisonBehavior", "Assets/Core/Status/PoisonBehavior.cs"),
        ], "links": []}
        self.nodes, self.links = build(
            content_root=root,
            effect_keys={"Damage": "damage", "ApplyStatus": "apply_status"},
            effect_owners={"damage": "DamageHandler", "apply_status": "ApplyStatusHandler"},
            status_owners={"poison": "PoisonBehavior"},
            ast_graph=ast_graph,
            warn=self.warnings.append)

    def tearDown(self):
        self._tmp.cleanup()

    def rels(self, relation):
        return {(e["source"], e["target"]) for e in self.links if e["relation"] == relation}

    def test_카드가_상태를_건다(self):
        self.assertIn(("card:venom", "status:poison"), self.rels("applies_status"))

    def test_존재하지_않는_상태는_엣지_없이_경고한다(self):
        self.assertNotIn(("card:odd", "status:ghost"), self.rels("applies_status"))
        self.assertTrue(any("ghost" in w for w in self.warnings))

    def test_미인식_효과_필드를_경고한다(self):
        self.assertTrue(any("mystery_field" in w for w in self.warnings))

    def test_효과_kind가_핸들러_코드_노드로_이어진다(self):
        self.assertIn(("effect_kind:damage", "h_damage"), self.rels("handled_by"))

    def test_상태가_행동_코드_노드로_이어진다(self):
        self.assertIn(("status:poison", "b_poison"), self.rels("handled_by"))

    def test_AST에_없는_핸들러는_경고한다(self):
        self.assertTrue(any("ApplyStatusHandler" in w for w in self.warnings))

    def test_풀과_덱이_카드를_담고_없는_카드는_경고한다(self):
        self.assertIn(("pool:p1", "card:venom"), self.rels("contains_card"))
        self.assertIn(("deck:d1", "card:venom"), self.rels("contains_card"))
        self.assertTrue(any("missing_card" in w for w in self.warnings))

    def test_캐릭터가_덱을_소유한다(self):
        self.assertIn(("character:c1", "deck:d1"), self.rels("owns_deck"))

    def test_태그는_노드도_엣지도_되지_않는다(self):
        self.assertFalse(any("독" == n["label"] and n["id"].startswith("tag") for n in self.nodes))
        self.assertFalse(any(e["relation"] == "has_tag" for e in self.links))

    def test_모든_엣지는_EXTRACTED_1점0이다(self):
        for e in self.links:
            self.assertEqual(e["confidence"], "EXTRACTED")
            self.assertEqual(e["confidence_score"], 1.0)


class MergeTests(unittest.TestCase):
    def test_재실행해도_중복되지_않는다(self):
        graph = {"nodes": [ast_node("a", "A", "Assets/Core/A.cs")], "links": []}
        nodes = [{"id": "card:x", "label": "x", "file_type": "concept",
                  "source_file": "", "source_location": "L1", "_origin": "card_extractor"}]
        merge_into(graph, nodes, [])
        merge_into(graph, nodes, [])
        self.assertEqual(len([n for n in graph["nodes"] if n["id"] == "card:x"]), 1)
        self.assertEqual(len(graph["nodes"]), 2)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: 실패 확인**

Run: `python3 -m unittest discover -s Tools/graph -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'extract_card_graph'` (Task 1 테스트는 PASS 유지)

- [ ] **Step 3: 구현**

`Tools/graph/extract_card_graph.py`:

```python
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
KNOWN_EFFECT_FIELDS = {"kind", "value", "selector", "status", "count", "target"}
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
```

- [ ] **Step 4: 통과 확인**

Run: `python3 -m unittest discover -s Tools/graph -v`
Expected: PASS (Task 1 포함 전체)

- [ ] **Step 5: 실물 검증** — Task 1 Step 5를 이미 돌린 상태(프루닝된 graph.json 존재)에서:

```bash
python3 Tools/graph/extract_card_graph.py
python3 - <<'EOF'
import json
g = json.load(open('graphify-out/graph.json'))
cards = [n for n in g['nodes'] if n['id'].startswith('card:')]
bridges = [e for e in g['links'] if e['relation'] == 'handled_by']
print('카드', len(cards), '| handled_by', len(bridges))
EOF
```

Expected: 카드 ~27개, handled_by는 효과 kind(~8) + 상태(11) 부근. stderr 경고 목록을 읽고, 콘텐츠 실데이터 문제(존재하지 않는 참조)가 나오면 **고치지 말고 기록만** 한다 — 콘텐츠 수정은 이 계획의 범위 밖이다.

- [ ] **Step 6: 커밋**

```bash
git add Tools/graph/extract_card_graph.py Tools/graph/test_extract_card_graph.py
git commit -m "feat(tools): 콘텐츠 JSON을 graphify 그래프에 편입시키는 카드 추출기를 추가한다"
```

---

### Task 3: 뷰 서브그래프 생성기 (카드 관계망 + 아키텍처 접기)

**Files:**
- Create: `Tools/graph/build_card_view.py`
- Create: `Tools/graph/collapse_architecture.py`
- Test: `Tools/graph/test_views.py`

**Interfaces:**
- Consumes: Task 2의 노드 ID 규약(`_origin="card_extractor"`)과 병합된 graph.json, graphify가 붙인 노드 필드 `community`(int)·`community_name`(str).
- Produces:
  - `select_card_view(graph: dict) -> dict` — 카드 서브그래프 + handled_by 대상 코드 노드
  - `collapse(graph: dict) -> dict` — 코드 노드를 커뮤니티 단위로 접은 그래프
  - CLI 각각 `graphify-out/view-card/graphify-out/graph.json`, `graphify-out/view-architecture/graphify-out/graph.json`을 쓴다 (cluster-only가 `<dir>/graphify-out/graph.json` 구조를 요구 — 스펙 §상세 4 실증).

- [ ] **Step 1: 실패하는 테스트 작성**

`Tools/graph/test_views.py`:

```python
import unittest

from build_card_view import select_card_view
from collapse_architecture import collapse


def n(nid, origin="ast", file_type="code", community=None, community_name=None):
    d = {"id": nid, "label": nid, "file_type": file_type, "source_file": "x",
         "source_location": "L1", "_origin": origin}
    if community is not None:
        d["community"] = community
        d["community_name"] = community_name
    return d


def e(s, t, relation="calls", origin="ast"):
    return {"relation": relation, "confidence": "EXTRACTED", "confidence_score": 1.0,
            "weight": 1.0, "source": s, "target": t,
            "source_file": "", "source_location": "", "_origin": origin}


class CardViewTests(unittest.TestCase):
    def test_카드_노드와_다리_건너_코드_노드만_남긴다(self):
        graph = {"nodes": [
            n("card:a", origin="card_extractor", file_type="concept"),
            n("effect_kind:damage", origin="card_extractor", file_type="concept"),
            n("h_damage"), n("unrelated_code"),
        ], "links": [
            e("card:a", "effect_kind:damage", "uses_effect", "card_extractor"),
            e("effect_kind:damage", "h_damage", "handled_by", "card_extractor"),
            e("unrelated_code", "h_damage"),
        ]}
        view = select_card_view(graph)
        ids = {x["id"] for x in view["nodes"]}
        self.assertEqual(ids, {"card:a", "effect_kind:damage", "h_damage"})
        self.assertEqual(len(view["links"]), 2)  # unrelated_code 엣지는 빠진다


class CollapseTests(unittest.TestCase):
    def test_커뮤니티가_노드_하나로_접히고_교차_엣지가_가중치로_모인다(self):
        graph = {"nodes": [
            n("a1", community=1, community_name="전투"),
            n("a2", community=1, community_name="전투"),
            n("b1", community=2, community_name="카드 UI"),
            n("doc", file_type="document", community=3, community_name="문서"),
        ], "links": [e("a1", "b1"), e("a2", "b1"), e("a1", "a2")]}
        arch = collapse(graph)
        self.assertEqual({x["id"] for x in arch["nodes"]},
                         {"community:1", "community:2"})  # 문서 커뮤니티는 제외
        labels = {x["id"]: x["label"] for x in arch["nodes"]}
        self.assertEqual(labels["community:1"], "전투 (2)")
        self.assertEqual(len(arch["links"]), 1)
        self.assertEqual(arch["links"][0]["weight"], 2)  # 같은 커뮤니티 내부 엣지는 접힌다


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: 실패 확인**

Run: `python3 -m unittest discover -s Tools/graph -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'build_card_view'`

- [ ] **Step 3: 구현**

`Tools/graph/build_card_view.py`:

```python
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
```

`Tools/graph/collapse_architecture.py`:

```python
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
```

- [ ] **Step 4: 통과 확인**

Run: `python3 -m unittest discover -s Tools/graph -v`
Expected: PASS (전체)

- [ ] **Step 5: 커밋**

```bash
git add Tools/graph/build_card_view.py Tools/graph/collapse_architecture.py Tools/graph/test_views.py
git commit -m "feat(tools): 카드 관계망·아키텍처 뷰 서브그래프 생성기를 추가한다"
```

---

### Task 4: 재생성 조정 스크립트

**Files:**
- Create: `Tools/graph/rebuild-graph.sh` (실행 권한 `chmod +x`)

**Interfaces:**
- Consumes: Task 1~3의 CLI 전부, graphify CLI (`update`, `cluster-only`).
- Produces: 실행 한 번으로 `graphify-out/graph.json`(통합·프루닝·병합본), `graphify-out/card-graph.html`, `graphify-out/architecture.html`. AGENTS.md 규칙 21이 이 경로를 새 재생성 명령으로 가리킨다(Task 5).

- [ ] **Step 1: 구현** (셸 조정자 — 로직 없음, 단위 테스트 대신 Step 2의 통합 검증)

`Tools/graph/rebuild-graph.sh`:

```bash
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
```

`--no-label`은 LLM 커뮤니티 이름 짓기를 생략하는 플래그다 — 전 과정 LLM 토큰 0을 지키는
필수 인자이며, 임의 graph.json에 HTML을 만들어 주는 것은 스펙 §상세 4에서 합성 그래프로
실증했다.

- [ ] **Step 2: 통합 검증** — 수용 기준(스펙 §검증) 확인:

```bash
chmod +x Tools/graph/rebuild-graph.sh && time Tools/graph/rebuild-graph.sh
```

Expected: 종료 코드 0, 벽시계 ~15초 이내. 이어서:

```bash
python3 - <<'EOF'
import json
g = json.load(open('graphify-out/graph.json'))
noise = [n for n in g['nodes'] if n.get('file_type') == 'code' and (
    not n.get('source_file') or '/Tests/' in n.get('source_file', '')
    or n.get('source_file', '').startswith(('Packages/', 'Assets/Plugins/', 'tools/', 'Tools/')))]
cards = [n for n in g['nodes'] if n['id'].startswith('card:')]
print('총', len(g['nodes']), '| 잡음', len(noise), '| 카드', len(cards))
assert not noise and cards, '수용 기준 실패'
EOF
graphify explain "맹독 찌르기" | head -20
graphify path "PoisonBehavior" "맹독 찌르기" | head -10
open graphify-out/card-graph.html graphify-out/architecture.html
```

Expected: 잡음 0, 카드 ~27. explain이 poison·damage 관계를 답하고, path가 카드↔코드 경로를 잇고, HTML 2장이 브라우저에서 열린다. **HTML 2장의 시각 품질(레이아웃·가독성)은 사용자 몫이다** — 열리는지까지만 확인하고 화면 평가는 사용자에게 요청한다 (AGENTS.md 규칙 17).

- [ ] **Step 3: 커밋**

```bash
git add Tools/graph/rebuild-graph.sh
git commit -m "feat(tools): 그래프 재생성 전 과정을 명령 하나로 묶는다"
```

---

### Task 5: AGENTS.md 규칙 개정과 색인 정리

**Files:**
- Modify: `AGENTS.md` (규칙 21 재생성 명령 문단, 규칙 22 갈림길 목록)
- Modify: `docs/superpowers/README.md` (이 계획 완료 처리)

**Interfaces:**
- Consumes: Task 4의 `Tools/graph/rebuild-graph.sh` 경로와 산출물 이름.

- [ ] **Step 1: 규칙 21의 재생성 명령 교체**

규칙 21에서 다음 블록을:

```bash
rm -f graphify-out/graph.json graphify-out/manifest.json && graphify update .
```

아래로 교체한다 (앞뒤 산문은 유지):

```bash
Tools/graph/rebuild-graph.sh
```

그리고 그 블록 바로 다음 문단("실측 결과 5,300노드…"로 시작)을 다음으로 교체한다:

> 실측 결과(2026-08-28) 재생성은 AST에 더해 잡음 프루닝(참조 스텁·테스트·외부 패키지·도구,
> 총 ~2,500노드)과 카드·상태·풀·덱 JSON의 서브그래프 병합, 사람용 시각화 2장
> (`graphify-out/card-graph.html` 카드 관계망, `graphify-out/architecture.html` 커뮤니티
> 단위 아키텍처)까지 만든다. 최종 그래프는 게임 로직 코드 + 문서 + 카드 서브그래프
> ~3,000노드에 죽은 노드 0%다. 문서 노드가 무료로 살아나는 건 `cache/semantic/`이 커밋되어
> 있기 때문이다(규칙 24). 증분 갱신은 union 병합이라 유령이 쌓이므로 삭제 후 재생성이
> 증분보다 싸고 깨끗하다. 갱신을 대신해 주는 훅은 없다 — 그래프 최신성은 오직 위 스크립트로
> 보장한다.

- [ ] **Step 2: 규칙 22에 카드 질의 항목 추가**

규칙 22의 갈림길 목록 첫 항목("심볼 이름을 알고…") 앞에 다음 항목을 추가한다:

> - **카드·상태의 관계 질문이면**(어느 카드가 X를 쌓나/소모하나, 이 핸들러·행동을 고치면
>   어느 카드가 영향받나, 시너지 경로) → 그래프가 1순위다. 엣지가 엔진이 실행하는 JSON과
>   레지스트리에서만 나오므로(태그 불포함) 답이 정확하고, 같은 답을 Read로 얻으려면 콘텐츠
>   JSON 수십 개를 통독해야 한다. `graphify explain "<카드 한글명>"` 또는
>   `graphify path "<행동 클래스>" "<카드 한글명>"`.

- [ ] **Step 3: README 색인 갱신** — `docs/superpowers/README.md`의 활성 계획 표에서 이 계획 행을 지우고, 계획 파일을 `docs/superpowers/archive/plans/`로 옮긴 뒤 완료 기록 관례에 따라 처리한다 (규칙 20). 작업 환경과 도구 표의 스펙 행은 `current`로 유지한다.

- [ ] **Step 4: 검증** — AGENTS.md 개정이 실제 산출물과 일치하는지 재확인: 인용한 경로 4개(`Tools/graph/rebuild-graph.sh`, `graphify-out/card-graph.html`, `graphify-out/architecture.html`, `cache/semantic/`)가 모두 존재한다.

```bash
ls Tools/graph/rebuild-graph.sh graphify-out/card-graph.html graphify-out/architecture.html && ls -d graphify-out/cache/semantic
```

Expected: 4개 경로 모두 출력, 종료 코드 0.

- [ ] **Step 5: 커밋**

```bash
git add AGENTS.md docs/superpowers/README.md docs/superpowers/archive/plans/2026-08-28-graphify-card-graph.md
git rm docs/superpowers/plans/2026-08-28-graphify-card-graph.md
git commit -m "docs: 그래프 재생성 규정을 rebuild-graph.sh 기준으로 갱신한다"
```

---

## 완료 후

- 전체 테스트 최종 확인: `python3 -m unittest discover -s Tools/graph -v` 전부 PASS.
- 워크트리 브랜치를 master에 머지하기 전 사용자 승인을 받는다 (규칙 19). 이 계획은 C#을 건드리지 않으므로 dotnet 헤드리스 테스트 재실행은 불필요하다 — 단 머지 시점에 master가 코어 변경을 품고 있으면 규칙 19의 전체 테스트 통과 확인을 따른다.
- HTML 2장의 시각 품질 평가와 카드 관계망의 내용 검수(엣지가 게임 지식과 맞는지)는 사용자에게 요청한다.
