using System.Collections.Generic;
using System.Text;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    /// <summary>Builds a card's description from its effects (or intervention action), substituting numbers from
    /// the data. Pure: all wording comes from the supplied <see cref="IDescriptionVocabulary"/>.
    /// Structure per effect: "{base}." optionally followed by " {condition} {success}." Effects join
    /// with a single space. Intervention cards render their intervention action instead of effects.</summary>
    public static class DescriptionComposer
    {
        public static string Describe(CardDefinition def, IDescriptionVocabulary vocab)
        {
            if (def.Category == CardCategory.Intervention && def.InterventionAction != null)
                return vocab.Intervention(def.InterventionAction) + ".";

            if (def.Effects == null || def.Effects.Count == 0)
                return string.Empty;

            var sentences = new List<string>(def.Effects.Count);
            foreach (var effect in def.Effects)
                sentences.Add(RenderEffect(effect, vocab));

            return string.Join(" ", sentences);
        }

        private static string RenderEffect(EffectData effect, IDescriptionVocabulary vocab)
        {
            var sb = new StringBuilder();
            sb.Append(Fragment(effect, effect.EffectValue, vocab)).Append('.');

            if (effect.Condition != null && effect.SuccessEffectValue.HasValue)
            {
                sb.Append(' ')
                  .Append(vocab.Condition(effect.Condition))
                  .Append(' ')
                  .Append(Fragment(effect, effect.SuccessEffectValue.Value, vocab))
                  .Append('.');
            }

            return sb.ToString();
        }

        // One effect's fragment for a given amount (base or success). No trailing punctuation.
        private static string Fragment(EffectData effect, int amount, IDescriptionVocabulary vocab)
        {
            if (effect.Key == EffectKeys.Damage)
                return vocab.Damage(amount);

            if (effect.Key == EffectKeys.ApplyStatus)
                return vocab.Status(
                    effect.StatusKey.Value,
                    effect.StatusTarget,
                    amount,
                    effect.StatusLifetime ?? Core.Status.StatusLifetime.ThisTurn);

            if (effect.Key == EffectKeys.NullifyNextPlayerConditionReward)
                return vocab.NullifyNextReward();

            if (effect.Key == EffectKeys.GrantNextPlayerAttackDamageBonus)
                return vocab.GrantNextAttackBonus(amount);

            return string.Empty;
        }
    }
}
