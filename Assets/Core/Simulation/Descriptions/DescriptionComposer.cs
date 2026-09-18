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

            // 카드 시작 조건은 카드 단위지만, 문장은 그 조건으로 수치가 바뀌는 효과마다 붙인다(스펙 §4).
            // 앞 효과 결과를 요구하는 효과는 요건 머리("소비했다면")를, 소비량 비례 가산은 꼬리를 단다.
            var condition = def.StartCondition;
            var lineTargets = new List<CardTargetKey?>();
            var lineTexts = new List<StringBuilder>();
            foreach (var effect in def.Effects)
            {
                var handler = catalog.Effects.Resolve(effect.Key);
                var requirement = effect.Requirement == null ? null : context.Requirement(effect.Requirement);
                var scaling = effect.Scaling == null ? string.Empty : context.ScalingSuffix(effect.Scaling);
                var conditional = condition != null && effect.SuccessEffectValue.HasValue;
                if (!(conditional && effect.SkipOnBasic))
                {
                    AppendSentence(
                        lineTargets,
                        lineTexts,
                        Fragment(handler, effect, effect.EffectValue, context),
                        requirement,
                        scaling);
                }

                if (conditional)
                {
                    AppendSentence(
                        lineTargets,
                        lineTexts,
                        Fragment(handler, effect, effect.SuccessEffectValue.Value, context),
                        context.Condition(condition),
                        scaling);
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
            string condition,
            string suffix)
        {
            var body = fragment.Text + suffix + ".";
            var sentence = string.IsNullOrEmpty(condition)
                ? body
                : condition + " " + body;
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

            // 진영마다 범위가 하나인 것은 카드 축이 보장한다(효과 대상 = 효과 진영 + 카드의 그 진영 축).
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
    }
}
