using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
namespace FateWeaver.Unity.Editor
{
    /// <summary>Brings Unity Localization's project settings to this game's baseline: the settings asset,
    /// the ko/en locales, and the startup order saved choice → system language → Korean.</summary>
    public static class LocalizationSettingsBaseline
    {
        public static void Ensure()
        {
            var root = LocalizationCsvImporter.AssetRoot;
            Directory.CreateDirectory(root);
            AssetDatabase.Refresh();
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, root + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }
            foreach (var code in new[] { "ko", "en" })
            {
                if (LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == code)) continue;
                var locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, root + "/" + code + ".asset");
                LocalizationEditorSettings.AddLocale(locale);
            }
            var active = LocalizationEditorSettings.ActiveLocalizationSettings;
            var selectors = active.GetStartupLocaleSelectors();
            // Rewriting identical selectors only churns their serialized reference ids.
            if (HasStartupSelectors(selectors)) return;
            selectors.Clear();
            selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = "fateweaver.locale" });
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = "ko" });
            EditorUtility.SetDirty(active);
        }
        private static bool HasStartupSelectors(IList<IStartupLocaleSelector> selectors)
            => selectors.Count == 3
                && selectors[0] is PlayerPrefLocaleSelector prefs && prefs.PlayerPreferenceKey == "fateweaver.locale"
                && selectors[1] is SystemLocaleSelector
                && selectors[2] is SpecificLocaleSelector specific && specific.LocaleId.Code == "ko";
    }
}
