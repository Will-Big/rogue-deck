using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class EffectDescriptionFragment
    {
        public CardTargetKey? Target { get; }
        public string Text { get; }

        public EffectDescriptionFragment(CardTargetKey? target, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Description text is required.", nameof(text));
            Target = target;
            Text = text;
        }
    }

    public sealed class CardDescriptionLine
    {
        public CardTargetKey? Target { get; }
        public string Text { get; }

        public CardDescriptionLine(CardTargetKey? target, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Description line text is required.", nameof(text));
            Target = target;
            Text = text;
        }
    }

    public sealed class CardDescriptionLayout
    {
        public IReadOnlyList<CardTargetKey> TargetEntries { get; }
        public IReadOnlyList<CardDescriptionLine> Lines { get; }
        public string PlainText { get; }

        public CardDescriptionLayout(
            IReadOnlyList<CardTargetKey> targetEntries,
            IReadOnlyList<CardDescriptionLine> lines,
            string plainText)
        {
            if (targetEntries == null) throw new ArgumentNullException(nameof(targetEntries));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            TargetEntries = targetEntries.ToArray();
            Lines = lines.ToArray();
            PlainText = plainText ?? throw new ArgumentNullException(nameof(plainText));
        }
    }
}
