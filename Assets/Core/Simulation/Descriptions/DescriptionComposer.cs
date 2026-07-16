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

            if (def.Effects == null || def.Effects.Count == 0)
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
            sb.Append(Fragment(handler, effect, effect.EffectValue, catalog.Context)).Append('.');

            if (effect.Condition != null && effect.SuccessEffectValue.HasValue)
            {
                sb.Append(' ')
                  .Append(catalog.Context.Condition(effect.Condition))
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
