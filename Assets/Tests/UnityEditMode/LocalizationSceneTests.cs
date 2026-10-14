using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using FateWeaver.Unity;
namespace FateWeaver.Tests.UnityEditMode
{
    public class LocalizationSceneTests
    {
        [Test]
        public void Locked_card_has_localized_tooltip_source()
        {
            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity/Prefabs/ExecutionCardView.prefab");
            var localized = card.GetComponent<CardLocalizationView>();
            Assert.IsNotNull(localized);
            var tooltip = new SerializedObject(localized).FindProperty("_tooltipPrefab");
            Assert.IsNotNull(tooltip, "Localized cards must reference an authored tooltip prefab.");
            Assert.IsNotNull(tooltip.objectReferenceValue);
        }
        [Test]
        public void Battle_scene_has_localization_and_language_buttons_wired()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FateWeaverBattle.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var localization = roots.SelectMany(r => r.GetComponentsInChildren<GameLocalization>(true)).SingleOrDefault();
                Assert.IsNotNull(localization);
                Assert.IsNotNull(new SerializedObject(localization).FindProperty("_bindings").objectReferenceValue);
                foreach (var presenter in roots.SelectMany(r => r.GetComponentsInChildren<BattlePresenter>(true)))
                    Assert.AreEqual(localization, new SerializedObject(presenter).FindProperty("_localization").objectReferenceValue);
                var languages = roots.SelectMany(r => r.GetComponentsInChildren<LanguageSelectionView>(true)).Single();
                foreach (var name in new[] { "_korean", "_english", "_koreanLocale", "_englishLocale" })
                    Assert.IsNotNull(new SerializedObject(languages).FindProperty(name).objectReferenceValue, name);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        // Re-baking the font re-creates its material sub-asset under a new fileID; labels keep the
        // old one. With one font for every locale nothing swaps the font at runtime to hide it.
        [Test]
        public void Text_labels_reference_an_existing_font_material()
        {
            var labels = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Unity/Prefabs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .SelectMany(prefab => prefab.GetComponentsInChildren<TMP_Text>(true)).ToList();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FateWeaverBattle.unity", OpenSceneMode.Additive);
            try
            {
                labels.AddRange(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)));
                Assert.IsNotEmpty(labels);
                foreach (var label in labels)
                {
                    var material = new SerializedObject(label).FindProperty("m_sharedMaterial");
                    Assert.IsNotNull(material.objectReferenceValue,
                        AssetDatabase.GetAssetPath(label) + " " + label.name + ": missing m_sharedMaterial");
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
