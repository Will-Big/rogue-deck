using System;
using System.Collections.Generic;
using System.Linq;
using FateWeaver.Core.Cards;
using static FateWeaver.Simulation.Descriptions.DescriptionRequests;
namespace FateWeaver.Simulation.Descriptions
{
    public static partial class DescriptionComposer
    {
        public static LocalizedCardDescription Compose(CardDefinition card, DescriptionRequestCatalog catalog)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var context = new DescriptionRequestContext(card, catalog.StatusContent);
            var targets = new List<CardTargetKey?>();
            var sentences = new List<List<LocalizedTextRequest>>();
            void Append(CardTargetKey? target, LocalizedTextRequest body, LocalizedTextRequest condition = null)
            {
                var sentence = condition == null ? Text("sentence", ("body", body)) : Text("sentence.conditional", ("condition", condition), ("body", body));
                var index = targets.FindIndex(t => Nullable.Equals(t, target));
                if (index < 0) { index = targets.Count; targets.Add(target); sentences.Add(new List<LocalizedTextRequest>()); }
                sentences[index].Add(sentence);
            }
            if (card.Category == CardCategory.Intervention)
            {
                if (card.InterventionAction == null) throw new ArgumentException("Intervention action required.");
                Append(null, catalog.Interventions.Resolve(card.InterventionAction.Key)(card.InterventionAction));
            }
            else
            {
                if (card.Category != CardCategory.Execution || card.InterventionAction != null || card.Effects == null) throw new ArgumentException("Invalid execution card.");
                foreach (var effect in card.Effects)
                {
                    if (effect == null) throw new ArgumentException("Null effect.");
                    var handler = catalog.Effects.Resolve(effect.Key);
                    void Add(int value, LocalizedTextRequest condition)
                    {
                        var fragment = handler(effect, value, context) ?? throw new InvalidOperationException("Empty description request.");
                        var body = effect.Scaling == null ? fragment.Text : Text("effect.scaled", ("body", fragment.Text), ("amount", effect.Scaling.PerConsumed));
                        if (condition == null && effect.Requirement != null)
                        {
                            var req = effect.Requirement.MinimumConsumed <= 1 ? Text("condition.consumed") : Text("condition.consumed_min", ("amount", effect.Requirement.MinimumConsumed));
                            body = Text("effect.required", ("condition", req), ("body", body));
                        }
                        Append(fragment.Target, body, condition);
                    }
                    var conditional = card.StartCondition != null && effect.SuccessEffectValue.HasValue;
                    if (!(conditional && effect.SkipOnBasic)) Add(effect.EffectValue, null);
                    if (conditional) Add(effect.SuccessEffectValue.Value, ConditionDescriptionRequests.Create(card.StartCondition));
                }
            }
            var entries = targets.Where(t => t.HasValue).Select(t => t.Value).Distinct()
                .OrderBy(t => t.Faction == CardTargetFaction.Ally ? 0 : 1).ThenBy(t => (int)t.Range).ToArray();
            return new LocalizedCardDescription(entries, targets.Select((t, i) => new LocalizedDescriptionLine(t, sentences[i])).ToArray());
        }
    }
}
