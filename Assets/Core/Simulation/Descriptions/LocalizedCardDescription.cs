using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Sentence requests sharing a structural target; no language-specific joins.</summary>
    public sealed class LocalizedDescriptionLine
    {
        public CardTargetKey? Target { get; }
        public IReadOnlyList<LocalizedTextRequest> Sentences { get; }

        public LocalizedDescriptionLine(
            CardTargetKey? target,
            IReadOnlyList<LocalizedTextRequest> sentences)
        {
            if (sentences == null) throw new ArgumentNullException(nameof(sentences));
            var snapshot = sentences.ToArray();
            if (snapshot.Length == 0 || snapshot.Any(sentence => sentence == null))
                throw new ArgumentException("A line requires non-null sentence requests.", nameof(sentences));
            Target = target;
            Sentences = Array.AsReadOnly(snapshot);
        }
    }

    /// <summary>Language-neutral card description preserving target and sentence ordering.</summary>
    public sealed class LocalizedCardDescription
    {
        public IReadOnlyList<CardTargetKey> TargetEntries { get; }
        public IReadOnlyList<LocalizedDescriptionLine> Lines { get; }

        public LocalizedCardDescription(
            IReadOnlyList<CardTargetKey> targetEntries,
            IReadOnlyList<LocalizedDescriptionLine> lines)
        {
            if (targetEntries == null) throw new ArgumentNullException(nameof(targetEntries));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            var snapshot = lines.ToArray();
            if (snapshot.Any(line => line == null))
                throw new ArgumentException("Description lines cannot contain null.", nameof(lines));
            TargetEntries = Array.AsReadOnly(targetEntries.ToArray());
            Lines = Array.AsReadOnly(snapshot);
        }
    }
}
