using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Logical table roles; Unity maps them to serialized table references.</summary>
    public enum TranslationTable
    {
        Content,
        Descriptions,
        UI
    }

    /// <summary>Immutable meaning supplied to a localized template, without final sentence copy.</summary>
    public sealed class LocalizedTextRequest
    {
        public TranslationTable Table { get; }
        public string Key { get; }
        public IReadOnlyDictionary<string, object> Arguments { get; }

        public LocalizedTextRequest(
            TranslationTable table,
            string key,
            IReadOnlyDictionary<string, object> arguments = null)
        {
            if (!Enum.IsDefined(typeof(TranslationTable), table))
                throw new ArgumentOutOfRangeException(nameof(table));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A translation key is required.", nameof(key));

            var snapshot = new Dictionary<string, object>(StringComparer.Ordinal);
            if (arguments != null)
            {
                foreach (var pair in arguments)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        throw new ArgumentException("An argument name is required.", nameof(arguments));
                    if (!(pair.Value is string || pair.Value is int || pair.Value is long
                        || pair.Value is decimal || pair.Value is LocalizedTextRequest))
                        throw new ArgumentException(
                            "Arguments must be identifiers, integer/decimal values, or nested requests.",
                            nameof(arguments));
                    snapshot.Add(pair.Key, pair.Value);
                }
            }

            Table = table;
            Key = key;
            Arguments = new ReadOnlyDictionary<string, object>(snapshot);
        }
    }
}
