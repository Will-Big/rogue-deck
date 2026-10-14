using System;
using System.Collections.Generic;
using NUnit.Framework;
using FateWeaver.Core.Cards;
using FateWeaver.Simulation.Descriptions;

namespace FateWeaver.Tests.EditMode
{
    public class LocalizedTextRequestTests
    {
        [Test]
        public void Requests_preserve_semantics_without_sentence_copy()
        {
            var status = new LocalizedTextRequest(TranslationTable.Content, "status.poison.name");
            var request = new LocalizedTextRequest(TranslationTable.Descriptions, "effect.apply_status",
                new Dictionary<string, object> { ["status"] = status, ["amount"] = 1 });

            Assert.AreEqual(TranslationTable.Descriptions, request.Table);
            Assert.AreEqual("effect.apply_status", request.Key);
            Assert.AreSame(status, request.Arguments["status"]);
            Assert.AreEqual(1, request.Arguments["amount"]);
            Assert.IsEmpty(status.Arguments);
        }

        [Test]
        public void Requests_snapshot_arguments()
        {
            var source = new Dictionary<string, object> { ["amount"] = 1 };
            var request = new LocalizedTextRequest(TranslationTable.Descriptions, "effect.damage", source);
            source["amount"] = 99;
            source.Clear();

            Assert.AreEqual(1, request.Arguments["amount"]);
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, object>)request.Arguments)["amount"] = 99);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Requests_reject_missing_keys(string key)
            => Assert.Throws<ArgumentException>(() =>
                new LocalizedTextRequest(TranslationTable.Content, key));

        [Test]
        public void Requests_reject_invalid_contracts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LocalizedTextRequest((TranslationTable)99, "key"));
            Assert.Throws<ArgumentException>(() => Request(" ", 1));
            Assert.Throws<ArgumentException>(() => Request("amount", null));
            Assert.Throws<ArgumentException>(() => Request("amount", new List<int> { 1 }));
            Assert.Throws<ArgumentException>(() => Request("amount", double.NaN));
        }

        [Test]
        public void Requests_accept_identifiers_and_numeric_values()
        {
            Assert.AreEqual("poison", Request("id", "poison").Arguments["id"]);
            Assert.AreEqual(long.MaxValue, Request("amount", long.MaxValue).Arguments["amount"]);
            Assert.AreEqual(1.5m, Request("amount", 1.5m).Arguments["amount"]);
        }

        [Test]
        public void Documents_snapshot_lines_and_targets()
        {
            var target = new CardTargetKey(CardTargetFaction.Enemy, CardTargetRange.FrontOne);
            var sentences = new List<LocalizedTextRequest> { Request("amount", 2) };
            var line = new LocalizedDescriptionLine(target, sentences);
            var lines = new List<LocalizedDescriptionLine> { line };
            var targets = new List<CardTargetKey> { target };
            var document = new LocalizedCardDescription(targets, lines);
            sentences.Clear();
            lines.Clear();
            targets.Clear();

            Assert.AreEqual(1, document.Lines.Count);
            Assert.AreEqual(target, document.TargetEntries[0]);
            Assert.AreEqual(2, document.Lines[0].Sentences[0].Arguments["amount"]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LocalizedDescriptionLine>)document.Lines).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LocalizedTextRequest>)line.Sentences).Clear());
        }

        [Test]
        public void Documents_reject_incomplete_lines()
        {
            Assert.Throws<ArgumentNullException>(() => new LocalizedDescriptionLine(null, null));
            Assert.Throws<ArgumentException>(() =>
                new LocalizedDescriptionLine(null, Array.Empty<LocalizedTextRequest>()));
            Assert.Throws<ArgumentException>(() =>
                new LocalizedDescriptionLine(null, new LocalizedTextRequest[] { null }));
            Assert.Throws<ArgumentNullException>(() => new LocalizedCardDescription(null,
                Array.Empty<LocalizedDescriptionLine>()));
            Assert.Throws<ArgumentException>(() => new LocalizedCardDescription(
                Array.Empty<CardTargetKey>(), new LocalizedDescriptionLine[] { null }));
        }

        private static LocalizedTextRequest Request(string name, object value)
            => new LocalizedTextRequest(TranslationTable.Descriptions, "effect.damage",
                new Dictionary<string, object> { [name] = value });
    }
}
