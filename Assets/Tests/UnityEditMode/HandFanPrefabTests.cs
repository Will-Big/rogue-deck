using System.Linq;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>The battle scene and the HandFan sandbox share one HandFan prefab, so values tuned in
    /// the sandbox reach battle without copying.</summary>
    public class HandFanPrefabTests
    {
        internal const string PrefabPath = "Assets/Unity/Prefabs/HandFan.prefab";

        [Test]
        public void Prefab_wires_hand_layout_and_content()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var hand = prefab.GetComponent<HandFanView>();
            var layout = prefab.GetComponent<HandFanLayoutView>();
            Assert.That(hand, Is.Not.Null);
            Assert.That(CardPrefabCatalogTests.Field<HandFanLayoutView>(hand, "_layout"), Is.SameAs(layout));
            Assert.That(CardPrefabCatalogTests.Field<CardPrefabCatalog>(hand, "_cardPrefabs"),
                Is.SameAs(CardPrefabCatalogTests.LoadCatalog()));
            var content = CardPrefabCatalogTests.Field<RectTransform>(hand, "_content");
            Assert.That(content.parent, Is.SameAs(prefab.transform));
            Assert.That(CardPrefabCatalogTests.Field<RectTransform>(layout, "_content"), Is.SameAs(content));
        }

        [Test]
        public void Sandbox_prefab_nests_the_shared_hand()
        {
            var sandbox = AssetDatabase.LoadAssetAtPath<GameObject>(HandFanSandboxSceneTests.PrefabPath);
            AssertSharedInstance(sandbox.GetComponentInChildren<HandFanView>(true));
        }

        [Test]
        public void Battle_scene_uses_the_shared_hand()
        {
            var scene = EditorSceneManager.OpenScene(CardPrefabCatalogTests.BattleScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                T Single<T>() where T : Component =>
                    roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
                var hand = Single<HandFanView>();
                AssertSharedInstance(hand);
                Assert.That(CardPrefabCatalogTests.Field<HandFanView>(Single<BattleScreenController>(), "_hand"),
                    Is.SameAs(hand));
                Assert.That(CardPrefabCatalogTests.Field<HandFanView>(Single<CardSelectionController>(), "_hand"),
                    Is.SameAs(hand));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertSharedInstance(HandFanView hand)
        {
            Assert.That(hand, Is.Not.Null);
            Assert.That(PrefabUtility.GetNearestPrefabInstanceRoot(hand.gameObject), Is.SameAs(hand.gameObject));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hand.gameObject), Is.EqualTo(PrefabPath));
            // 레이아웃 값을 인스턴스에서 덮어쓰면 프리팹 조정이 이 자리에 닿지 않는다.
            Assert.That(PrefabUtility.GetPropertyModifications(hand.gameObject)
                    .Where(m => m.target is HandFanLayoutView)
                    .Select(m => m.propertyPath),
                Is.Empty);
        }
    }
}
