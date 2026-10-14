using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
namespace FateWeaver.Unity.Editor
{
    /// <summary>Idempotent authoring of localization resources and prefab/scene references.</summary>
    public static class LocalizationAuthoring
    {
        private static readonly HashSet<TMP_Text> DynamicLabels = new HashSet<TMP_Text>();
        private static readonly Dictionary<string, string> StaticKeys = new Dictionary<string, string>
        {
            ["뽑을 덱"] = "ui.pile.draw", ["버린 덱"] = "ui.pile.discard", ["전체 덱"] = "ui.pile.all",
            ["초기화"] = "ui.reset", ["스킵"] = "ui.skip", ["확인"] = "ui.confirm",
            ["보상"] = "ui.reward", ["선택"] = "ui.choose", ["건너뛰기"] = "ui.reward.skip",
            ["다음 전투"] = "ui.battle.next", ["패배"] = "ui.defeat", ["처음부터"] = "ui.restart",
            ["실행"] = "ui.execution", ["개입"] = "ui.intervention"
        };
        private static TableReference Reference(string role)
            => LocalizationEditorSettings.GetStringTableCollection("FW_" + role).SharedData.TableCollectionNameGuid;
        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        private static void SetString(UnityEngine.Object target, string property, string value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
        private static void Labels(GameObject root, TableReference fonts)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var font = text.GetComponent<LocalizedFontBinding>() ?? text.gameObject.AddComponent<LocalizedFontBinding>();
                font.Configure(text, fonts); EditorUtility.SetDirty(font);
                if (DynamicLabels.Contains(text))
                {
                    var eventComponent = text.GetComponent<LocalizeStringEvent>();
                    if (eventComponent != null) UnityEngine.Object.DestroyImmediate(eventComponent);
                    continue;
                }
                if (!StaticKeys.TryGetValue(text.text.Trim(), out var key)) continue;
                var localized = text.GetComponent<LocalizeStringEvent>() ?? text.gameObject.AddComponent<LocalizeStringEvent>();
                localized.StringReference = new LocalizedString(Reference("UI"), key);
                while (localized.OnUpdateString.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(localized.OnUpdateString, 0);
                var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, "set_text");
                UnityEventTools.AddPersistentListener(localized.OnUpdateString, setter);
                localized.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                EditorUtility.SetDirty(localized);
            }
        }
        [MenuItem("Fate Weaver/Localization/Author Resources and Battle UI")]
        public static void Author()
        {
            DynamicLabels.Clear();
            LocalizationCsvImporter.ImportAll();
            var root = LocalizationCsvImporter.AssetRoot;
            var bindings = AssetDatabase.LoadAssetAtPath<LocalizationBindings>(root + "/Bindings.asset");
            if (bindings == null) { bindings = ScriptableObject.CreateInstance<LocalizationBindings>(); AssetDatabase.CreateAsset(bindings, root + "/Bindings.asset"); }
            bindings.Configure(Reference("Content"), Reference("Descriptions"), Reference("UI")); EditorUtility.SetDirty(bindings);
            Directory.CreateDirectory(root + "/Fonts"); AssetDatabase.Refresh();
            var fontPath = root + "/Fonts/Main.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath) == null && !AssetDatabase.CopyAsset("Assets/Unity/Resources/Fonts/KoreanTMP.asset", fontPath))
                throw new InvalidOperationException("Could not copy existing font into localization assets.");
            var fonts = LocalizationEditorSettings.GetAssetTableCollection("FW_Fonts") ?? LocalizationEditorSettings.CreateAssetTableCollection("FW_Fonts", root + "/Fonts");
            foreach (var table in fonts.AssetTables)
            { table.AddEntry("font.main", AssetDatabase.AssetPathToGUID(fontPath)); EditorUtility.SetDirty(table); }
            EditorUtility.SetDirty(fonts.SharedData); EditorUtility.SetDirty(fonts);
            var addressables = AddressableAssetSettingsDefaultObject.GetSettings(true);
            addressables.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(fontPath), addressables.DefaultGroup);
            EditorUtility.SetDirty(addressables);
            TableReference fontReference = fonts.SharedData.TableCollectionNameGuid;

            foreach (var path in new[] { "ExecutionCardView", "InterventionCardView", "DescriptionLineView", "UnitView", "RailCardView", "RewardChoiceView", "CombatResultView", "CardStatusTooltipView" })
            {
                var prefabPath = "Assets/Unity/Prefabs/" + path + ".prefab";
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    Labels(prefab, fontReference);
                    foreach (var card in prefab.GetComponentsInChildren<CardView>(true))
                    {
                        var localized = card.GetComponent<CardLocalizationView>() ?? card.gameObject.AddComponent<CardLocalizationView>();
                        Set(localized, "_view", card);
                        Set(localized, "_tooltipPrefab", AssetDatabase.LoadAssetAtPath<CardStatusTooltipView>("Assets/Unity/Prefabs/CardStatusTooltipView.prefab"));
                        var badge = (GameObject)new SerializedObject(card).FindProperty("_lockBadge").objectReferenceValue;
                        if (badge != null)
                        {
                            var icon = badge.GetComponent<CardStatusIconView>() ?? badge.AddComponent<CardStatusIconView>();
                            Set(icon, "_icon", badge.GetComponent<UnityEngine.UI.Image>());
                            Set(localized, "_lockIcon", icon);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            var managerPath = "Assets/Unity/Prefabs/GameLocalization.prefab";
            var managerRoot = new GameObject("GameLocalization");
            var manager = managerRoot.AddComponent<GameLocalization>(); Set(manager, "_bindings", bindings);
            PrefabUtility.SaveAsPrefabAsset(managerRoot, managerPath); UnityEngine.Object.DestroyImmediate(managerRoot);

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FateWeaverBattle.unity", OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var service = roots.SelectMany(r => r.GetComponentsInChildren<GameLocalization>(true)).FirstOrDefault();
            if (service == null) service = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(managerPath), scene)).GetComponent<GameLocalization>();
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            foreach (var component in all)
            {
                if (component is BattlePresenter || component is BattleHudView || component is CombatNodeFlow || component is PileView)
                    Set(component, "_localization", service);
                if (component is PileView pile)
                {
                    var title = new SerializedObject(pile).FindProperty("_title").stringValue;
                    if (!StaticKeys.TryGetValue(title, out var key)) throw new InvalidOperationException("Unknown pile title: " + title);
                    SetString(pile, "_titleKey", key);
                }
            }
            foreach (var component in all)
            {
                var fields = component is BattleHudView ? new[] { "_energyText", "_messageText", "_turnButtonLabel", "_speedButtonLabel" }
                    : component is PileView ? new[] { "_labelText" } : Array.Empty<string>();
                foreach (var field in fields)
                    DynamicLabels.Add((TMP_Text)new SerializedObject(component).FindProperty(field).objectReferenceValue);
            }
            foreach (var gameObject in scene.GetRootGameObjects()) Labels(gameObject, fontReference);
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).First();
            var languages = canvas.GetComponentInChildren<LanguageSelectionView>(true);
            if (languages == null)
            {
                var panel = new GameObject("LanguageSelection", typeof(RectTransform));
                var rect = (RectTransform)panel.transform; rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-24, -18); rect.sizeDelta = new Vector2(230, 42);
                var languageView = panel.AddComponent<LanguageSelectionView>();
                var hud = all.OfType<BattleHudView>().First();
                var reset = (UnityEngine.UI.Button)new SerializedObject(hud).FindProperty("_resetButton").objectReferenceValue;
                foreach (var choice in new[] { ("ko", "한국어", "_korean", "_koreanLocale", -120f), ("en", "English", "_english", "_englishLocale", 0f) })
                {
                    var buttonRoot = UnityEngine.Object.Instantiate(reset.gameObject, panel.transform);
                    buttonRoot.name = choice.Item1;
                    var button = buttonRoot.GetComponent<UnityEngine.UI.Button>(); button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                    var buttonRect = (RectTransform)buttonRoot.transform;
                    buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1, 1);
                    buttonRect.anchoredPosition = new Vector2(choice.Item5, 0); buttonRect.sizeDelta = new Vector2(110, 40);
                    var text = buttonRoot.GetComponentInChildren<TMP_Text>(true); text.text = choice.Item2;
                    var oldEvent = text.GetComponent<LocalizeStringEvent>(); if (oldEvent != null) UnityEngine.Object.DestroyImmediate(oldEvent);
                    Set(languageView, choice.Item3, button);
                    Set(languageView, choice.Item4, LocalizationEditorSettings.GetLocales().First(l => l.Identifier.Code == choice.Item1));
                }
                PrefabUtility.SaveAsPrefabAsset(panel, "Assets/Unity/Prefabs/LanguageSelection.prefab");
                UnityEngine.Object.DestroyImmediate(panel);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Unity/Prefabs/LanguageSelection.prefab"), canvas.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var errors = LocalizationCsvValidator.Validate();
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
        }
        public static void BuildBatch()
        {
            try
            {
                var errors = LocalizationCsvValidator.Validate();
                if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
                UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
                Debug.Log("Localization Addressables build succeeded: " + result.OutputPath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void AuthorBatch()
        {
            try { Author(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
