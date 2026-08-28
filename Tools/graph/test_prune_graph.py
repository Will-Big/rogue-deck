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
        self.assertTrue(is_noise(node("g1", source_file="tools/graph/prune_graph.py")))
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
