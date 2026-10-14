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
            RequireLocales();
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
                // Csv.Export uses CsvHelper's default configuration, which ends records with CRLF; the committed CSVs are LF.
                var csv = new StringWriter();
                Csv.Export(csv, collection, Columns());
                File.WriteAllText(Path.Combine(CsvRoot, role + ".csv"), csv.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            }
            var semanticErrors = LocalizationCsvValidator.Validate();
            if (semanticErrors.Count > 0) throw new InvalidDataException(string.Join("\n", semanticErrors));
            AssetDatabase.SaveAssets();
        }
        // Each locale column imports into that locale's table, so the locales must already exist.
        private static void RequireLocales()
        {
            var missing = Columns().OfType<LocaleColumns>().Select(c => c.LocaleIdentifier.Code)
                .Where(code => LocalizationEditorSettings.GetLocales().All(l => l.Identifier.Code != code)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Missing locales: " + string.Join(", ", missing)
                    + "; run Fate Weaver/Localization/Author Resources and Battle UI.");
        }
    }
}
