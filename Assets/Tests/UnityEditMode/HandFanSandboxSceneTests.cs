using System.Linq;
using System.Reflection;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using TMPro;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    public class HandFanSandboxSceneTests
    {
        internal const string ScenePath = "Assets/Scenes/HandFanSandbox.unity";
        internal const string PrefabPath = "Assets/Unity/Prefabs/Testing/HandFanSandbox.prefab";

        [Test]
        public void Saved_scene_has_reusable_hand_and_ready_input_without_battle_flow()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
                var hand = All<HandFanView>().Single();
                var controller = All<HandFanSandboxController>().Single();
                var panel = All<HandFanSandboxPanel>().Single();
                Assert.That(panel.IsBound, Is.True);
                Assert.That(CardPrefabCatalogTests.Field<HandFanView>(controller, "_hand"), Is.SameAs(hand));
                Assert.That(CardPrefabCatalogTests.Field<HandFanSandboxPanel>(controller, "_panel"), Is.SameAs(panel));
                Assert.That(CardPrefabCatalogTests.Field<HandFanSandboxCardSource>(controller, "_source"), Is.SameAs(All<HandFanSandboxCardSource>().Single()));
                var catalog = CardPrefabCatalogTests.Field<CardPrefabCatalog>(hand, "_cardPrefabs");
                Assert.That(catalog, Is.SameAs(CardPrefabCatalogTests.LoadCatalog()));
                Assert.DoesNotThrow(catalog.ValidateOrThrow);
                Assert.That(CardPrefabCatalogTests.Field<RectTransform>(hand, "_content"), Is.Not.Null);
                Assert.That(All<CombatNodeFlow>(), Is.Empty);
                Assert.That(All<Canvas>().Single().GetComponent<GraphicRaycaster>(), Is.Not.Null);
                Assert.That(All<EventSystem>(), Has.Length.EqualTo(1));
                var input = All<BaseInputModule>().Single();
                Assert.That(input.GetType().Name, Is.EqualTo("InputSystemUIInputModule"));
                var serialized = new SerializedObject(input);
                Assert.That(serialized.FindProperty("m_ActionsAsset").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("m_PointAction").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("m_LeftClickAction").objectReferenceValue, Is.Not.Null);
                Assert.That(All<Camera>(), Has.Length.EqualTo(1));
                foreach (var text in All<TMP_Text>())
                {
                    Assert.That(text.font, Is.Not.Null);
                    Assert.That(text.raycastTarget, Is.False);
                }
                Assert.That(All<Button>(), Has.Length.EqualTo(3));
                foreach (var button in All<Button>())
                {
                    Assert.That(button.targetGraphic, Is.Not.Null);
                    Assert.That(button.targetGraphic.raycastTarget, Is.True);
                }
                foreach (var transform in All<Transform>())
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero);
                Assert.That(roots.Any(r => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r) == PrefabPath), Is.True);
                Assert.That(All<HandFanSandboxCardSource>().Single().TryLoad(out var samples, out var error), Is.True, error);
                Assert.That(samples.Count, Is.GreaterThan(0));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Prefab_stores_persistent_input_references_without_default_action_fallback()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var module = prefab.GetComponentInChildren<BaseInputModule>(true);
            var serialized = new SerializedObject(module);
            foreach (var field in new[] { "m_ActionsAsset", "m_PointAction", "m_LeftClickAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_ScrollWheelAction" })
            {
                var reference = serialized.FindProperty(field).objectReferenceValue;
                Assert.That(reference, Is.Not.Null, field);
                Assert.That(EditorUtility.IsPersistent(reference), Is.True, field);
                Assert.That(AssetDatabase.GetAssetPath(reference), Is.EqualTo(AssetDatabase.GetAssetPath(serialized.FindProperty("m_ActionsAsset").objectReferenceValue)), field);
            }
        }

        [Test]
        public void Saved_prefab_reenables_without_duplicate_commands_and_creates_real_card()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            var panel = root.GetComponentInChildren<HandFanSandboxPanel>();
            var controller = root.GetComponentInChildren<HandFanSandboxController>();
            try
            {
                Invoke(panel, "OnEnable");
                Invoke(controller, "Start");
                for (int i = 0; i < 3; i++)
                {
                    Invoke(controller, "OnDisable"); Invoke(panel, "OnDisable");
                    Invoke(panel, "OnEnable"); Invoke(controller, "OnEnable");
                }
                var add = CardPrefabCatalogTests.Field<Button>(panel, "_addButton");
                add.onClick.Invoke();
                Assert.That(root.GetComponentsInChildren<CardView>(), Has.Length.EqualTo(1));
                var state = CardPrefabCatalogTests.Field<HandFanSandboxState>(controller, "_state");
                Assert.That(state.Cards.Count, Is.EqualTo(1));
                Assert.That(state.Cards[0].Id, Is.EqualTo("brace"));
                Assert.That(CardPrefabCatalogTests.Field<TMP_Text>(panel, "_countText").text, Does.Contain("1"));
                // Click the real card through its bound input rather than calling State.Select.
                var cardButton = root.GetComponentInChildren<CardView>().GetComponent<Button>();
                Assert.That(cardButton, Is.Not.Null);
                cardButton.onClick.Invoke();
                Assert.That(state.SelectedIndex, Is.EqualTo(0));
                Assert.That(CardPrefabCatalogTests.Field<Button>(panel, "_removeButton").interactable, Is.True);
            }
            finally
            {
                Invoke(controller, "OnDisable"); Invoke(panel, "OnDisable");
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Missing_source_is_reported_once_and_disables_controls()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            try
            {
                var controller = root.GetComponentInChildren<HandFanSandboxController>();
                var panel = root.GetComponentInChildren<HandFanSandboxPanel>();
                var so = new SerializedObject(controller);
                so.FindProperty("_source").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                LogAssert.Expect(LogType.Error, "HandFan 테스트 씬의 필수 참조가 누락되었습니다.");
                Invoke(controller, "Start");
                Assert.That(controller.enabled, Is.False);
                Assert.That(root.GetComponentsInChildren<Button>().All(b => !b.interactable), Is.True);
                Assert.That(CardPrefabCatalogTests.Field<TMP_Text>(panel, "_errorText").text, Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Invoke(Component component, string method)
            => component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
    }
}
