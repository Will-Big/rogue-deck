using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public static class DescriptionCatalogValidator
    {
        public static void ValidateDefault(
            IEnumerable<CardDefinition> cards,
            KoreanDescriptionCatalog descriptions)
        {
            Validate(
                cards,
                descriptions,
                CombatRegistries.Effects(),
                CombatRegistries.Statuses(),
                CombatRegistries.InterventionActions());
        }

        public static void Validate(
            IEnumerable<CardDefinition> cards,
            KoreanDescriptionCatalog descriptions,
            EffectRegistry effects,
            StatusRegistry statuses,
            InterventionActionRegistry interventions)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (descriptions == null) throw new ArgumentNullException(nameof(descriptions));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            if (statuses == null) throw new ArgumentNullException(nameof(statuses));
            if (interventions == null) throw new ArgumentNullException(nameof(interventions));

            foreach (var card in cards)
            {
                if (card == null)
                    throw new ArgumentException("Card catalog cannot contain null cards.", nameof(cards));

                if (card.Category == CardCategory.Intervention)
                {
                    if (card.InterventionAction == null)
                        throw new ArgumentException(
                            "Intervention card requires an intervention action.",
                            nameof(cards));

                    var actionKey = card.InterventionAction.Key;
                    interventions.Resolve(actionKey);
                    descriptions.Interventions.Resolve(actionKey);
                    DescriptionComposer.Compose(card, descriptions);
                    continue;
                }

                if (card.InterventionAction != null)
                    throw new ArgumentException(
                        "Execution card cannot contain an intervention action.",
                        nameof(cards));
                if (card.Effects == null)
                    throw new ArgumentException(
                        "Execution card requires an effects collection.",
                        nameof(cards));

                foreach (var effect in card.Effects)
                {
                    if (effect == null)
                        throw new ArgumentException(
                            "Card effects cannot contain null entries.",
                            nameof(cards));

                    var handler = effects.Resolve(effect.Key);
                    descriptions.Effects.Resolve(effect.Key);

                    if (handler is IEffectDataValidator validator)
                    {
                        foreach (var error in validator.ValidateData(effect))
                            throw new ArgumentException(
                                "Card '" + card.Id + "': " + error, nameof(cards));
                    }

                    if (effect.Payload is ApplyStatusPayload statusPayload)
                    {
                        statuses.Resolve(statusPayload.Key);
                        descriptions.Statuses.Resolve(statusPayload.Key);
                    }
                }

                DescriptionComposer.Compose(card, descriptions);
            }
        }
    }
}
