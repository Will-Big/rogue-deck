using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using Newtonsoft.Json.Linq;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using FateWeaver.Core.Authoring;
using FateWeaver.Simulation.Descriptions;
namespace FateWeaver.Unity.Editor
{
    public static class LocalizationCsvValidator
    {
        private static readonly Regex Arguments = new Regex(@"(?<!\{)\{\s*([a-zA-Z_]\w*)");
        public static IReadOnlyList<string> ValidateCsv(TextReader reader)
        {
            var errors = new List<string>();
            var keys = new HashSet<string>(); var ids = new HashSet<long>();
            try
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csv.Read(); csv.ReadHeader();
                    foreach (var header in new[] { "Key", "Id", "Korean(ko)", "English(en)" })
                        if (!csv.Context.HeaderRecord.Contains(header)) errors.Add("Missing CSV column: " + header);
                    if (errors.Count > 0) return errors;
                    while (csv.Read())
                    {
                        var key = csv.GetField("Key"); var id = csv.GetField("Id");
                        var ko = csv.GetField("Korean(ko)"); var en = csv.GetField("English(en)");
                        if (string.IsNullOrWhiteSpace(key) || !keys.Add(key)) errors.Add("Missing/duplicate key: " + key);
                        if (!string.IsNullOrWhiteSpace(id) && id != "0" && (!long.TryParse(id, out var parsed) || parsed < 1 || !ids.Add(parsed))) errors.Add("Invalid/duplicate ID: " + id);
                        if (key != "text.empty" && (string.IsNullOrWhiteSpace(ko) || string.IsNullOrWhiteSpace(en))) errors.Add("Missing translation: " + key);
                        if (!Names(ko).SetEquals(Names(en))) errors.Add("Translation argument mismatch: " + key);
                    }
                }
            }
            catch (Exception exception) { errors.Add("Invalid CSV: " + exception.Message); }
            return errors;
        }
        private static HashSet<string> Names(string template)
            => new HashSet<string>(Arguments.Matches(template ?? "").Cast<Match>().Select(m => m.Groups[1].Value));
        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var loaded = ContentBootstrap.Load(UnityContentRoot.Path);
            if (!loaded.Succeeded) { errors.AddRange(loaded.Errors); return errors; }
            foreach (var role in new[] { "Content", "Descriptions", "UI" })
                using (var reader = File.OpenText(Path.Combine(LocalizationCsvImporter.CsvRoot, role + ".csv"))) errors.AddRange(ValidateCsv(reader));
            var content = loaded.Content;
            foreach (var code in new[] { "ko", "en" })
            {
                var tables = new Dictionary<TranslationTable, StringTable>();
                foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
                {
                    var col = LocalizationEditorSettings.GetStringTableCollection("FW_" + role);
                    var table = col?.GetTable(code) as StringTable;
                    if (table == null) { errors.Add("Missing table: " + role + "/" + code); continue; }
                    tables[role] = table;
                    foreach (var entry in col.SharedData.Entries)
                        if (entry.Key != "text.empty" && string.IsNullOrWhiteSpace(table.GetEntry(entry.Id)?.Value)) errors.Add("Empty table value: " + code + "/" + entry.Key);
                }
                if (tables.Count != 3) continue;
                var renderer = new UnityLocalizedTextRenderer(tables);
                foreach (var card in content.Cards.Cards.Values)
                {
                    try
                    {
                        var doc = DescriptionComposer.Compose(card, DescriptionRequestCatalog.CreateDefault(content.Statuses));
                        foreach (var request in doc.Lines.SelectMany(l => l.Sentences)) ValidateRequest(request, tables, errors);
                        renderer.Render(doc);
                        renderer.Render(DescriptionRequests.Name("card." + card.Id + ".name"));
                    }
                    catch (Exception exception) { errors.Add(code + "/" + card.Id + ": " + exception.Message); }
                }
            }
            var koTable = LocalizationEditorSettings.GetStringTableCollection("FW_Content")?.GetTable("ko") as StringTable;
            foreach (var kind in new[] { ("Cards", "card"), ("Statuses", "status"), ("Characters", "character"), ("Enemies", "enemy") })
                foreach (var path in Directory.GetFiles(Path.Combine(UnityContentRoot.Path, kind.Item1), "*.json"))
                {
                    var json = JObject.Parse(File.ReadAllText(path));
                    var id = (string)(json["id"] ?? json["key"]); var name = (string)(json["name"] ?? json["displayName"]);
                    if (koTable?.GetEntry(kind.Item2 + "." + id + ".name")?.Value != name) errors.Add("JSON/Korean name mismatch: " + path);
                }
            return errors;
        }
        private static void ValidateRequest(LocalizedTextRequest request, IReadOnlyDictionary<TranslationTable, StringTable> tables, List<string> errors)
        {
            if (request.Key == "text.empty" && request.Table == TranslationTable.Descriptions) return;
            var entry = tables[request.Table].GetEntry(request.Key);
            if (entry == null) { errors.Add("Missing request key: " + request.Key); return; }
            if (!Names(entry.Value).SetEquals(request.Arguments.Keys)) errors.Add("Request argument contract mismatch: " + request.Key);
            foreach (var nested in request.Arguments.Values.OfType<LocalizedTextRequest>()) ValidateRequest(nested, tables, errors);
        }
    }
}
