using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEngine.Localization.Tables;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using FateWeaver.Simulation.Descriptions;
using FateWeaver.Unity;
using FateWeaver.Unity.Editor;
namespace FateWeaver.Tests.UnityEditMode
{
    public class LocalizationPipelineTests
    {
        [Test]
        public void Csv_round_trip_preserves_multiline_and_ids()
        {
            var col = LocalizationEditorSettings.GetStringTableCollection("FW_UI");
            var en = col.GetTable("en") as StringTable;
            var key = "test.roundtrip";
            var entry = en.AddEntry(key, "One, two\nthree");
            var id = entry.KeyId;
            try
            {
                var output = new StringWriter();
                Csv.Export(output, col, LocalizationCsvImporter.Columns());
                Csv.ImportInto(new StringReader(output.ToString()), col, LocalizationCsvImporter.Columns());
                Assert.AreEqual("One, two\nthree", en.GetEntry(key).Value);
                Assert.AreEqual(id, en.GetEntry(key).KeyId);
            }
            finally { col.RemoveEntry(key); }
        }
        [Test]
        public void Import_is_repeatable_and_removes_deleted_keys()
        {
            var col = LocalizationEditorSettings.GetStringTableCollection("FW_UI");
            var id = col.SharedData.GetId("ui.turn.execute");
            (col.GetTable("en") as StringTable).AddEntry("obsolete.key", "Old");
            LocalizationCsvImporter.ImportAll();
            Assert.AreEqual(id, col.SharedData.GetId("ui.turn.execute"));
            Assert.IsNull(col.SharedData.GetEntry("obsolete.key"));
            Assert.IsEmpty(LocalizationCsvValidator.Validate());
        }
        [Test]
        public void Missing_translation_or_argument_contract_fails()
        {
            var errors = LocalizationCsvValidator.ValidateCsv(new StringReader("Key,Id,Shared Comments,Korean(ko),English(en)\nx,1,,{amount},\n"));
            Assert.IsNotEmpty(errors);
            errors = LocalizationCsvValidator.ValidateCsv(new StringReader("Key,Id,Shared Comments,Korean(ko),English(en)\nx,1,,{amount},{different}\n"));
            Assert.IsNotEmpty(errors);
        }
        [TestCase("01")]
        [TestCase("+1")]
        [TestCase(" 1 ")]
        public void Equivalent_numeric_ids_are_rejected(string secondId)
        {
            var csv = "Key,Id,Shared Comments,Korean(ko),English(en)\nid.a,1,,가,A\nid.b," + secondId + ",,나,B\n";
            Assert.IsNotEmpty(LocalizationCsvValidator.ValidateCsv(new StringReader(csv)));
        }
        [Test]
        public void Same_meaning_uses_short_or_sentence_template_without_code_change()
        {
            var col = LocalizationEditorSettings.GetStringTableCollection("FW_Descriptions");
            var ko = col.GetTable("ko") as StringTable;
            var content = LocalizationEditorSettings.GetStringTableCollection("FW_Content").GetTable("ko") as StringTable;
            var renderer = new UnityLocalizedTextRenderer(new Dictionary<TranslationTable, StringTable>
            { [TranslationTable.Content] = content, [TranslationTable.Descriptions] = ko });
            var request = DescriptionRequests.Text("effect.apply_status", ("status", DescriptionRequests.Name("status.poison.name")), ("amount", 1));
            var entry = ko.GetEntry("effect.apply_status");
            var original = entry.Value;
            try
            {
                entry.Value = "{status} {amount}";
                Assert.AreEqual("독 1", renderer.Render(request));
                entry.Value = "{status} {amount}을 부여합니다.";
                Assert.AreEqual("독 1을 부여합니다.", renderer.Render(request));
            }
            finally { entry.Value = original; }
        }
        [Test]
        public void Missing_parent_translation_falls_back_as_one_locale()
        {
            var primary = new Dictionary<TranslationTable, StringTable>();
            var fallback = new Dictionary<TranslationTable, StringTable>();
            foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection("FW_" + role);
                primary[role] = collection.GetTable("en") as StringTable;
                fallback[role] = collection.GetTable("ko") as StringTable;
            }
            var entry = primary[TranslationTable.Descriptions].GetEntry("effect.apply_status");
            var original = entry.Value;
            try
            {
                entry.Value = "";
                LogAssert.Expect(LogType.Warning, "Missing translation: en/effect.apply_status");
                var renderer = new UnityLocalizedTextRenderer(primary, fallback);
                Assert.AreEqual("독 1", renderer.Render(DescriptionRequests.Text("effect.apply_status",
                    ("status", DescriptionRequests.Name("status.poison.name")), ("amount", 1))));
            }
            finally { entry.Value = original; }
        }
        [Test]
        public void Missing_nested_translation_falls_back_the_whole_sentence()
        {
            var primary = new Dictionary<TranslationTable, StringTable>();
            var fallback = new Dictionary<TranslationTable, StringTable>();
            foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection("FW_" + role);
                primary[role] = collection.GetTable("en") as StringTable;
                fallback[role] = collection.GetTable("ko") as StringTable;
            }
            var entry = primary[TranslationTable.Content].GetEntry("status.poison.name");
            var original = entry.Value;
            try
            {
                entry.Value = "";
                LogAssert.Expect(LogType.Warning, "Missing translation: en/status.poison.name");
                var renderer = new UnityLocalizedTextRenderer(primary, fallback);
                Assert.AreEqual("독 1", renderer.Render(DescriptionRequests.Text("effect.apply_status",
                    ("status", DescriptionRequests.Name("status.poison.name")), ("amount", 1))));
            }
            finally { entry.Value = original; }
        }

        [TestCase("side.enemy", "the last executed card was an enemy damage card")]
        [TestCase("side.player", "the last executed card was a player damage card")]
        public void English_side_conditions_choose_the_article_from_the_side(string side, string expected)
        {
            var renderer = TestRenderer("en");
            Assert.AreEqual(expected, renderer.Render(DescriptionRequests.Text("condition.previous",
                ("side", DescriptionRequests.Text(side)), ("kind", DescriptionRequests.Text("kind.damage_card")))));
        }

        [Test]
        public void English_conditions_follow_the_body_so_the_body_keeps_its_sentence_case()
        {
            var content = UnityTestContent.Content();
            var requests = DescriptionRequestCatalog.CreateDefault(content.Statuses);
            var renderer = TestRenderer("en");
            StringAssert.Contains("Deal 7 damage if the last executed card was an enemy damage card.",
                renderer.Render(DescriptionComposer.Compose(content.Cards.Get("riposte"), requests)).PlainText);
            StringAssert.Contains("Gain 1 Fate next turn if any stacks were consumed.",
                renderer.Render(DescriptionComposer.Compose(content.Cards.Get("distill"), requests)).PlainText);
        }

        [Test]
        public void Korean_effect_names_are_display_names_not_raw_effect_ids()
        {
            var content = LocalizationEditorSettings.GetStringTableCollection("FW_Content");
            var ko = (StringTable)content.GetTable("ko");
            var effectNames = content.SharedData.Entries.Where(e => e.Key.StartsWith("effect.") && e.Key.EndsWith(".name")).ToArray();
            Assert.IsNotEmpty(effectNames);
            foreach (var entry in effectNames)
                StringAssert.DoesNotMatch("[A-Za-z_]", ko.GetEntry(entry.Id).Value, entry.Key);
            Assert.AreEqual("상태 부여 효과 카드", TestRenderer("ko").Render(DescriptionRequests.Text("kind.effect_card",
                ("effect", DescriptionRequests.Name("effect.apply_status.name")))));
        }

        private static UnityLocalizedTextRenderer TestRenderer(string code)
        {
            var tables = new Dictionary<TranslationTable, StringTable>();
            foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
                tables[role] = LocalizationEditorSettings.GetStringTableCollection("FW_" + role).GetTable(code) as StringTable;
            return new UnityLocalizedTextRenderer(tables);
        }

        [Test]
        public void Numeric_plural_and_signed_values_use_actual_smart_format()
        {
            var en = LocalizationEditorSettings.GetStringTableCollection("FW_Descriptions").GetTable("en") as StringTable;
            var renderer = new UnityLocalizedTextRenderer(new Dictionary<TranslationTable, StringTable> { [TranslationTable.Descriptions] = en });
            Assert.AreEqual("Move forward 1 position", renderer.Render(DescriptionRequests.Text("effect.move.forward", ("amount", 1))));
            Assert.AreEqual("Move forward 2 positions", renderer.Render(DescriptionRequests.Text("effect.move.forward", ("amount", 2))));
            Assert.AreEqual("Change one card's execution order by -2", renderer.Render(DescriptionRequests.Text("intervention.change_order", ("amount", -2))));
        }

        [Test]
        public void Locale_fonts_cover_authored_copy_and_have_saved_materials()
        {
            var collection = LocalizationEditorSettings.GetAssetTableCollection("FW_Fonts");
            foreach (var code in new[] { "ko", "en" })
            {
                var table = collection.GetTable(code) as AssetTable;
                var path = AssetDatabase.GUIDToAssetPath(table.GetEntry("font.main").Guid);
                Assert.IsFalse(path.Contains("/Resources/"));
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                Assert.IsNotNull(font);
                Assert.IsTrue(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Any());
                var copy = string.Concat(LocalizationEditorSettings.GetStringTableCollections()
                    .SelectMany(c => ((StringTable)c.GetTable(code)).Values.Select(e => e.Value)));
                copy = new string(copy.Where(c => !char.IsControl(c)).Distinct().ToArray());
                Assert.IsTrue(font.HasCharacters(copy, out uint[] missing, false, true),
                    code + " missing glyphs: " + string.Join(",", (missing ?? Array.Empty<uint>()).Select(c => c.ToString("X"))));
            }
        }

        [Test]
        public void Actual_tables_render_every_card_in_both_languages()
        {
            var content = UnityTestContent.Content();
            foreach (var code in new[] { "ko", "en" })
            {
                var tables = new Dictionary<TranslationTable, StringTable>();
                foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
                    tables[role] = LocalizationEditorSettings.GetStringTableCollection("FW_" + role).GetTable(code) as StringTable;
                var renderer = new UnityLocalizedTextRenderer(tables);
                foreach (var card in content.Cards.Cards.Values)
                {
                    var doc = DescriptionComposer.Compose(card, DescriptionRequestCatalog.CreateDefault(content.Statuses));
                    Assert.IsNotEmpty(renderer.Render(doc).PlainText, code + ": " + card.Id);
                }
            }
        }
    }
}
