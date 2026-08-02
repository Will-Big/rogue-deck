using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Builds immutable card-description layouts from registered effect handlers.</summary>
    public static class DescriptionComposer
    {
        public static string Describe(CardDefinition def, KoreanDescriptionCatalog catalog)
            => Compose(def, catalog).PlainText;

        public static CardDescriptionLayout Compose(
            CardDefinition def,
            KoreanDescriptionCatalog catalog)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var context = catalog.ContextFor(def);
            if (def.Category == CardCategory.Intervention)
                return ComposeIntervention(def, catalog, context);

            if (def.Category != CardCategory.Execution)
                throw new ArgumentException(
                    "Card category must be execution or intervention.",
                    nameof(def));
            if (def.InterventionAction != null)
                throw new ArgumentException(
                    "Execution card cannot contain an intervention action.",
                    nameof(def));
            if (def.Effects == null)
                throw new ArgumentException(
                    "Execution card requires an effects collection.",
                    nameof(def));
            if (def.Effects.Count == 0)
                return new CardDescriptionLayout(
                    Array.Empty<CardTargetKey>(),
                    Array.Empty<CardDescriptionLine>(),
                    string.Empty);

            var lineTargets = new List<CardTargetKey?>();
            var lineTexts = new List<StringBuilder>();
            foreach (var effect in def.Effects)
            {
                var handler = catalog.Effects.Resolve(effect.Key);
                var skipBasic = effect.SkipOnBasic
                    && effect.Condition != null
                    && effect.SuccessEffectValue.HasValue;
                if (!skipBasic)
                {
                    AppendSentence(
                        lineTargets,
                        lineTexts,
                        Fragment(handler, effect, effect.EffectValue, context),
                        null);
                }

                if (effect.Condition != null && effect.SuccessEffectValue.HasValue)
                {
                    AppendSentence(
                        lineTargets,
                        lineTexts,
                        Fragment(handler, effect, effect.SuccessEffectValue.Value, context),
                        context.Condition(effect.Condition));
                }
            }

            return Layout(lineTargets, lineTexts, context);
        }

        private static CardDescriptionLayout ComposeIntervention(
            CardDefinition def,
            KoreanDescriptionCatalog catalog,
            DescriptionContext context)
        {
            if (def.InterventionAction == null)
                throw new ArgumentException(
                    "Intervention card requires an intervention action.",
                    nameof(def));

            var action = def.InterventionAction;
            var fragment = catalog.Interventions.Resolve(action.Key).Describe(action, context);
            if (string.IsNullOrWhiteSpace(fragment))
                throw new InvalidOperationException(
                    "Card '" + context.CardId
                    + "' intervention description handler returned an empty fragment for '"
                    + action.Key + "'.");

            var lines = new[] { new CardDescriptionLine(null, fragment + ".") };
            return new CardDescriptionLayout(Array.Empty<CardTargetKey>(), lines, lines[0].Text);
        }

        private static void AppendSentence(
            List<CardTargetKey?> lineTargets,
            List<StringBuilder> lineTexts,
            EffectDescriptionFragment fragment,
            string condition)
        {
            var sentence = string.IsNullOrEmpty(condition)
                ? fragment.Text + "."
                : condition + " " + fragment.Text + ".";
            var lineIndex = lineTargets.FindIndex(
                target => Nullable.Equals(target, fragment.Target));
            if (lineIndex >= 0)
            {
                lineTexts[lineIndex].Append(' ').Append(sentence);
                return;
            }

            lineTargets.Add(fragment.Target);
            lineTexts.Add(new StringBuilder(sentence));
        }

        private static EffectDescriptionFragment Fragment(
            IEffectDescriptionHandler handler,
            EffectData effect,
            int amount,
            DescriptionContext context)
        {
            EffectDescriptionFragment fragment;
            try
            {
                fragment = handler.Describe(effect, amount, context);
            }
            catch (ArgumentException ex) when (ex.ParamName == "text")
            {
                throw new InvalidOperationException(
                    "Card '" + context.CardId
                    + "' effect description handler returned an empty fragment for '"
                    + effect.Key + "'.",
                    ex);
            }

            if (fragment == null || string.IsNullOrWhiteSpace(fragment.Text))
                throw new InvalidOperationException(
                    "Card '" + context.CardId
                    + "' effect description handler returned an empty fragment for '"
                    + effect.Key + "'.");
            return fragment;
        }

        private static CardDescriptionLayout Layout(
            IReadOnlyList<CardTargetKey?> lineTargets,
            IReadOnlyList<StringBuilder> lineTexts,
            DescriptionContext context)
        {
            var lines = new CardDescriptionLine[lineTexts.Count];
            var targets = new List<CardTargetKey>();
            for (var i = 0; i < lineTexts.Count; i++)
            {
                lines[i] = new CardDescriptionLine(lineTargets[i], lineTexts[i].ToString());
                if (lineTargets[i].HasValue && !targets.Contains(lineTargets[i].Value))
                    targets.Add(lineTargets[i].Value);
            }

            ValidateSingleRangePerFaction(targets, context.CardId);
            var entries = targets
                .OrderBy(target => target.Faction == CardTargetFaction.Ally ? 0 : 1)
                .ThenBy(target => (int)target.Range)
                .ToArray();
            var plainText = string.Join(
                "\n",
                lines.Select(line => line.Target.HasValue
                    ? "[" + context.Symbol(line.Target.Value) + "] " + line.Text
                    : line.Text));
            return new CardDescriptionLayout(entries, lines, plainText);
        }

        private static void ValidateSingleRangePerFaction(
            IReadOnlyList<CardTargetKey> targets,
            string cardId)
        {
            foreach (var faction in new[] { CardTargetFaction.Ally, CardTargetFaction.Enemy })
            {
                var ranges = targets
                    .Where(target => target.Faction == faction)
                    .Select(target => target.Range)
                    .Distinct()
                    .OrderBy(range => (int)range)
                    .ToArray();
                if (ranges.Length > 1)
                    throw new InvalidOperationException(
                        "Card '" + cardId + "' declares conflicting " + faction
                        + " target ranges: " + string.Join(", ", ranges) + ".");
            }
        }
    }
}
