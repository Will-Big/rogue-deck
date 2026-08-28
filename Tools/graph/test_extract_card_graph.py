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
    (root / "Cards" / "new_fields.json").write_text(json.dumps({
        "id": "new_fields", "name": "새필드", "effects": [
            {"kind": "damage", "value": 1, "condition": {"threshold": 5}},
            {"kind": "damage", "value": 2, "maxAmount": 10},
            {"kind": "damage", "value": 3, "damageBonusPerConsumed": 1},
        ]}), encoding="utf-8")
    (root / "Cards" / "cond_status.json").write_text(json.dumps({
        "id": "cond_status", "name": "조건참조", "effects": [
            {"kind": "damage", "condition": {"status": "poison"}},
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

    def test_condition_maxAmount_damageBonusPerConsumed_필드는_미인식_경고가_없다(self):
        # new_fields 카드의 3개 effect가 KNOWN_EFFECT_FIELDS에 포함되므로
        # "미인식 효과 필드" 경고가 없어야 한다.
        unrecognized = [w for w in self.warnings if "미인식 효과 필드" in w and "new_fields" in w]
        self.assertEqual(len(unrecognized), 0)

    def test_condition이_상태를_참조하면_경고한다(self):
        # cond_status 카드의 condition이 "status" 키를 가지므로 경고가 나야 한다.
        condition_status_warns = [w for w in self.warnings if "condition이 상태를 참조한다" in w]
        self.assertTrue(len(condition_status_warns) > 0)


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
