import json
import tempfile
import unittest
from pathlib import Path

from name_communities import apply_names, dominant_label, rename_view


def node(nid, label, community=None, file_type="code"):
    d = {"id": nid, "label": label, "file_type": file_type,
         "source_file": "x", "source_location": "L1", "_origin": "ast"}
    if community is not None:
        d["community"] = community
        d["community_name"] = f"Community {community}"
    return d


def edge(s, t):
    return {"relation": "calls", "confidence": "EXTRACTED", "confidence_score": 1.0,
            "weight": 1.0, "source": s, "target": t,
            "source_file": "", "source_location": "", "_origin": "ast"}


class DominantLabelTests(unittest.TestCase):
    def test_점으로_시작하는_메서드_라벨은_건너뛴다(self):
        members = [node("a", ".Load()"), node("b", "CardSpec")]
        deg = {"a": 9, "b": 1}
        self.assertEqual(dominant_label(members, deg), "CardSpec")


class ApplyNamesTests(unittest.TestCase):
    def test_매핑_히트는_큐레이션_이름_미스는_대표_심볼(self):
        graph = {"nodes": [
            node("a", "CombatState", community=1), node("b", ".Resolve()", community=1),
            node("c", "듣보Widget", community=2),
        ], "links": [edge("b", "a")]}
        renamed = apply_names(graph, {"CombatState": "전투 상태·턴 해결"})
        names = {n["id"]: n["community_name"] for n in graph["nodes"]}
        self.assertEqual(names["a"], "전투 상태·턴 해결")
        self.assertEqual(names["b"], "전투 상태·턴 해결")
        self.assertEqual(names["c"], "듣보Widget")  # 미스 → 대표 심볼 그대로
        self.assertEqual(renamed, 3)


class RenameViewTests(unittest.TestCase):
    def test_자리표시자를_그룹_대표_라벨로_치환한다(self):
        with tempfile.TemporaryDirectory() as tmp:
            view = Path(tmp) / "view"
            (view / "graphify-out").mkdir(parents=True)
            graph = {"nodes": [
                node("a", "효과 적용 핸들러 (21)", community=0),
                node("b", "카드 더미 뷰 (5)", community=0),
                node("c", "독", community=1),
            ], "links": [edge("a", "b")]}
            jp = view / "graphify-out" / "graph.json"
            jp.write_text(json.dumps(graph, ensure_ascii=False), encoding="utf-8")
            hp = Path(tmp) / "out.html"
            hp.write_text(json.dumps(
                {"legend": ["Community 0", "Community 1"]}, ensure_ascii=False), encoding="utf-8")
            named = rename_view(str(view), str(hp))
            self.assertEqual(named, 2)
            html = hp.read_text(encoding="utf-8")
            self.assertIn("효과 적용 핸들러", html)   # (21) 접미 제거 + 최다 멤버 그룹명
            self.assertIn("독", html)
            self.assertNotIn("Community 0", html)
            patched = json.loads(jp.read_text(encoding="utf-8"))
            self.assertEqual(patched["nodes"][0]["community_name"], "효과 적용 핸들러")


if __name__ == "__main__":
    unittest.main()
