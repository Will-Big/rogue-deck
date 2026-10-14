using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FateWeaver.Simulation.Descriptions;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
namespace FateWeaver.Unity
{
    /// <summary>Formats immutable meaning requests with Unity's locale-specific Smart Strings.</summary>
    public sealed class UnityLocalizedTextRenderer
    {
        private readonly IReadOnlyDictionary<TranslationTable, StringTable> _tables;
        private readonly IReadOnlyDictionary<TranslationTable, StringTable> _fallback;
        public UnityLocalizedTextRenderer(IReadOnlyDictionary<TranslationTable, StringTable> tables,
            IReadOnlyDictionary<TranslationTable, StringTable> fallback = null)
        { _tables = tables ?? throw new ArgumentNullException(nameof(tables)); _fallback = fallback; }

        public string Render(LocalizedTextRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var missing = FindMissing(request);
            if (missing != null)
            {
                var locale = _tables.TryGetValue(missing.Table, out var table) ? table.LocaleIdentifier.Code : "unbound";
                Debug.LogWarning("Missing translation: " + locale + "/" + missing.Key);
                if (_fallback != null) return new UnityLocalizedTextRenderer(_fallback).Render(request);
                throw new KeyNotFoundException("Missing translation key: " + missing.Key);
            }
            return RenderPrimary(request);
        }
        private LocalizedTextRequest FindMissing(LocalizedTextRequest request)
        {
            if (request.Table == TranslationTable.Descriptions && request.Key == "text.empty") return null;
            if (!_tables.TryGetValue(request.Table, out var table)
                || string.IsNullOrWhiteSpace(table.GetEntry(request.Key)?.Value)) return request;
            foreach (var nested in request.Arguments.Values.OfType<LocalizedTextRequest>())
            {
                var missing = FindMissing(nested);
                if (missing != null) return missing;
            }
            return null;
        }
        private string RenderPrimary(LocalizedTextRequest request)
        {
            if (request.Table == TranslationTable.Descriptions && request.Key == "text.empty") return string.Empty;
            var table = _tables[request.Table];
            var entry = table.GetEntry(request.Key);
            var arguments = new Dictionary<string, object>();
            foreach (var pair in request.Arguments)
                arguments.Add(pair.Key, pair.Value is LocalizedTextRequest nested ? RenderPrimary(nested) : pair.Value);
            return entry.GetLocalizedString(table.LocaleIdentifier.CultureInfo, new object[] { arguments }, null);
        }
        public CardDescriptionLayout Render(LocalizedCardDescription document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var lines = document.Lines.Select(line =>
            {
                var text = Render(line.Sentences[0]);
                for (var i = 1; i < line.Sentences.Count; i++)
                    text = Render(DescriptionRequests.Text("list.sentences", ("first", text), ("next", Render(line.Sentences[i]))));
                return new CardDescriptionLine(line.Target, text);
            }).ToArray();
            return new CardDescriptionLayout(document.TargetEntries, lines,
                string.Join("\n", lines.Select(l => l.Target.HasValue ? "[◆] " + l.Text : l.Text)));
        }
        public static async Task<UnityLocalizedTextRenderer> LoadAsync(LocalizationBindings bindings, Locale locale)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            if (locale == null) throw new ArgumentNullException(nameof(locale));
            await LocalizationSettings.InitializationOperation.Task;
            var tables = new Dictionary<TranslationTable, StringTable>();
            var fallback = new Dictionary<TranslationTable, StringTable>();
            var ko = LocalizationSettings.AvailableLocales.GetLocale("ko");
            foreach (TranslationTable role in Enum.GetValues(typeof(TranslationTable)))
            {
                tables.Add(role, await LocalizationSettings.StringDatabase.GetTableAsync(bindings.TableFor(role), locale).Task);
                fallback.Add(role, await LocalizationSettings.StringDatabase.GetTableAsync(bindings.TableFor(role), ko).Task);
                if (tables[role] == null || fallback[role] == null) throw new InvalidOperationException("Could not load localization table: " + role);
            }
            return new UnityLocalizedTextRenderer(tables, fallback);
        }
        public Task<string> RenderAsync(LocalizedTextRequest request, Locale locale)
        {
            if (_tables.Values.Any(t => t.LocaleIdentifier != locale.Identifier)) throw new ArgumentException("Renderer locale differs from requested locale.");
            return Task.FromResult(Render(request));
        }
        public Task<CardDescriptionLayout> RenderAsync(LocalizedCardDescription document, Locale locale)
        {
            if (_tables.Values.Any(t => t.LocaleIdentifier != locale.Identifier)) throw new ArgumentException("Renderer locale differs from requested locale.");
            return Task.FromResult(Render(document));
        }
    }
}
