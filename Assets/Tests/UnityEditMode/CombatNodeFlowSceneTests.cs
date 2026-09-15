using System.Linq;
using System.Reflection;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace FateWeaver.Tests.UnityEditMode
{
    public class CombatNodeFlowSceneTests
    {
        [Test]
        public void Battle_scene_wires_the_combat_node_flow()
        {
            var scene = EditorSceneManager.OpenScene(CardPrefabCatalogTests.BattleScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                T Single<T>() where T : UnityEngine.Component
                    => roots.SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

                var flow = Single<CombatNodeFlow>();
                var controller = Single<BattleScreenController>();
                var reward = Single<RewardChoiceView>();
                var result = Single<CombatResultView>();
                var presenter = Single<BattlePresenter>();

                Assert.AreSame(controller, CardPrefabCatalogTests.Field<BattleScreenController>(flow, "_battle"));
                Assert.AreSame(reward, CardPrefabCatalogTests.Field<RewardChoiceView>(flow, "_reward"));
                Assert.AreSame(result, CardPrefabCatalogTests.Field<CombatResultView>(flow, "_result"));
                Assert.AreEqual(2, CardPrefabCatalogTests.Field<CharacterAsset[]>(flow, "_party").Length);
                Assert.IsTrue(reward.IsBound, "씬 인스턴스는 presenter·catalog까지 채워져야 한다.");
                Assert.AreSame(presenter, CardPrefabCatalogTests.Field<BattlePresenter>(reward, "_presenter"));
                Assert.IsTrue(result.IsBound);
                Assert.IsFalse(reward.gameObject.activeSelf);
                Assert.IsFalse(result.gameObject.activeSelf);
                Assert.IsNull(
                    typeof(BattleScreenController).GetField("_party", BindingFlags.Instance | BindingFlags.NonPublic),
                    "파티 구성은 컨트롤러가 아니라 흐름이 든다.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
