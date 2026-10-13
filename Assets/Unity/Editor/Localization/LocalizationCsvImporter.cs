using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
namespace FateWeaver.Unity.Editor
{
    public static class LocalizationCsvImporter
    {
        public const string AssetRoot = "Assets/Unity/Localization";
        public static string CsvRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Localization", "Csv");
        public static IList<CsvColumns> Columns() => new List<CsvColumns>
        {
            new KeyIdColumns { KeyFieldName = "Key", IdFieldName = "Id", SharedCommentFieldName = "Shared Comments" },
            new LocaleColumns { LocaleIdentifier = "ko", FieldName = "Korean(ko)", IncludeComments = false },
            new LocaleColumns { LocaleIdentifier = "en", FieldName = "English(en)", IncludeComments = false }
        };
        [MenuItem("Fate Weaver/Localization/Import CSV")]
        public static void ImportAll()
        {
            var errors = new List<string>();
            foreach (var role in new[] { "Content", "Descriptions", "UI" })
            {
                using (var reader = new StreamReader(Path.Combine(CsvRoot, role + ".csv"), Encoding.UTF8))
                    errors.AddRange(LocalizationCsvValidator.ValidateCsv(reader));
            }
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            EnsureSettings();
            foreach (var role in new[] { "Content", "Descriptions", "UI" })
            {
                var name = "FW_" + role;
                var collection = LocalizationEditorSettings.GetStringTableCollection(name)
                    ?? LocalizationEditorSettings.CreateStringTableCollection(name, AssetRoot + "/" + role);
                using (var reader = new StreamReader(Path.Combine(CsvRoot, role + ".csv"), Encoding.UTF8))
                    Csv.ImportInto(reader, collection, Columns(), removeMissingEntries: true);
                foreach (var table in collection.StringTables)
                {
                    foreach (var entry in table.Values) entry.IsSmart = entry.Value.Contains("{");
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
                EditorUtility.SetDirty(collection);
                LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(typeof(LocalizationCsvImporter), collection);
                // Persist Unity-assigned Id values to the authoritative CSV.
                using (var writer = new StreamWriter(Path.Combine(CsvRoot, role + ".csv"), false, new UTF8Encoding(false)))
                    Csv.Export(writer, collection, Columns());
            }
            var semanticErrors = LocalizationCsvValidator.Validate();
            if (semanticErrors.Count > 0) throw new InvalidDataException(string.Join("\n", semanticErrors));
            AssetDatabase.SaveAssets();
        }
        private static void EnsureSettings()
        {
            Directory.CreateDirectory(AssetRoot);
            AssetDatabase.Refresh();
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null)
            {
                var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, AssetRoot + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }
            foreach (var code in new[] { "ko", "en" })
            {
                if (LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == code)) continue;
                var locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, AssetRoot + "/" + code + ".asset");
                LocalizationEditorSettings.AddLocale(locale);
            }
            var active = LocalizationEditorSettings.ActiveLocalizationSettings;
            active.GetStartupLocaleSelectors().Clear();
            active.GetStartupLocaleSelectors().Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = "fateweaver.locale" });
            active.GetStartupLocaleSelectors().Add(new SystemLocaleSelector());
            active.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector { LocaleId = "ko" });
            EditorUtility.SetDirty(active);
        }
        public static void ImportBatch()
        {
            try { ImportAll(); var errors = LocalizationCsvValidator.Validate(); if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors)); AssetDatabase.SaveAssets(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }
    }
}
