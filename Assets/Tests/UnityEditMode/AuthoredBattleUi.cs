using System.Linq;
using FateWeaver.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FateWeaver.Tests.UnityEditMode
{
    /// <summary>Loads authored UI for tests without changing the saved assets or open scenes.</summary>
    internal static class AuthoredBattleUi
    {
        internal static T Instantiate<T>(Transform parent) where T : Component
        {
            var path = $"Assets/Unity/Prefabs/{typeof(T).Name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            var instance = Object.Instantiate(prefab, parent, false);
            var view = instance.GetComponent<T>();
            Assert.IsNotNull(view, path);
            return view;
        }

        internal static ExecutionRailView InstantiateRail(
            Transform parent, RectTransform previewLayer, CardPrefabCatalog catalog = null)
        {
            var scene = EditorSceneManager.OpenPreviewScene(CardPrefabCatalogTests.BattleScenePath);
            try
            {
                var source = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ExecutionRailView>(true))
                    .Single();
                source.gameObject.SetActive(false);
                var rail = Object.Instantiate(source, parent, false);
                var serialized = new SerializedObject(rail);
                serialized.FindProperty("_previewLayer").objectReferenceValue = previewLayer;
                serialized.FindProperty("_cardPrefabs").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var content = (RectTransform)serialized.FindProperty("_content").objectReferenceValue;
                // Tests own the cards in the clone, including when the authored scene has sample cards.
                for (int i = content.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(content.GetChild(i).gameObject);
                rail.gameObject.SetActive(true);
                return rail;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
