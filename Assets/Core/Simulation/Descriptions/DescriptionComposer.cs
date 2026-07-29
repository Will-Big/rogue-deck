using System;
using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Cards;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Builds a card's description from its effects (or intervention action), substituting numbers from
    /// the data. Pure: all wording comes from registered description handlers.
    /// Structure per effect: "{base}." optionally followed by " {condition} {success}." Effects join
    /// with a single space. Intervention cards render their intervention action instead of effects.</summary>
    public static class DescriptionComposer
    {
        public static string Describe(CardDefinition def, KoreanDescriptionCatalog catalog)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            if (def.Category == CardCategory.Intervention)
            {
                if (def.InterventionAction == null)
                    throw new ArgumentException(
                        "Intervention card requires an intervention action.",
                        nameof(def));

                var action = def.InterventionAction;
                var handler = catalog.Interventions.Resolve(action.Key);
                var fragment = handler.Describe(action, catalog.Context);
                if (string.IsNullOrWhiteSpace(fragment))
                    throw new InvalidOperationException(
                        "Intervention description handler returned an empty fragment for '"
                        + action.Key + "'.");
                return fragment + ".";
            }

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
                return string.Empty;

            var sentences = new List<string>(def.Effects.Count);
            foreach (var effect in def.Effects)
                sentences.Add(RenderEffect(effect, catalog));

            return string.Join(" ", sentences);
        }

        private static string RenderEffect(EffectData effect, KoreanDescriptionCatalog catalog)
        {
            var handler = catalog.Effects.Resolve(effect.Key);
            var sb = new StringBuilder();

            // SkipOnBasic effects never fire on the basic tier ('~했다면 X' — no unconditional
            // baseline), so the basic fragment is omitted entirely; only the condition + success
            // sentence renders. Without this, the basic and success fragments both print with
            // identical wording (e.g. "방어 4. 소비했다면 방어 4.").
            var skipBasic = effect.SkipOnBasic && effect.Condition != null && effect.SuccessEffectValue.HasValue;
            if (!skipBasic)
            {
                sb.Append(Fragment(handler, effect, effect.EffectValue, catalog.Context)).Append('.');
            }

            if (effect.Condition != null && effect.SuccessEffectValue.HasValue)
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(catalog.Context.Condition(effect.Condition))
                  .Append(' ')
                  .Append(Fragment(
                      handler,
                      effect,
                      effect.SuccessEffectValue.Value,
                      catalog.Context))
                  .Append('.');
            }

            return sb.ToString();
        }

        private static string Fragment(
            IEffectDescriptionHandler handler,
            EffectData effect,
            int amount,
            DescriptionContext context)
        {
            var fragment = handler.Describe(effect, amount, context);
            if (string.IsNullOrWhiteSpace(fragment))
                throw new InvalidOperationException(
                    "Effect description handler returned an empty fragment for '"
                    + effect.Key + "'.");
            return fragment;
        }
    }
}
