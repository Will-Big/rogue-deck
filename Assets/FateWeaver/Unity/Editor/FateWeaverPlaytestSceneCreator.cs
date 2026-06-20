using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FateWeaver.Unity.Editor
{
    public static class FateWeaverPlaytestSceneCreator
    {
        public const string ScenePath = "Assets/FateWeaver/Scenes/FateWeaverPlaytest.unity";

        [MenuItem("Fate Weaver/Create Playtest Scene")]
        public static void Create()
        {
            Directory.CreateDirectory("Assets/FateWeaver/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("FateWeaver Playtest");
            root.AddComponent<FateWeaverPlaytestController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = root;
            Debug.Log("Created Fate Weaver playtest scene at " + ScenePath);
        }

        private static void AddToBuildSettings()
        {
            if (EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
            {
                return;
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
