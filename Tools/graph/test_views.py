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
