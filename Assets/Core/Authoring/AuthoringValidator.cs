using System.Collections.Generic;
using FateWeaver.Core.Cards;

namespace FateWeaver.Core.Authoring
{
    /// <summary>Walks authored card specs and collects every validation error (returns them instead of
    /// throwing so the editor can show all problems at once; boot/tests assert the list is empty).</summary>
    public static class AuthoringValidator
    {
        public static IReadOnlyList<string> Validate(
            IEnumerable<CardSpec> specs,
            AuthoringContext context)
        {
            var errors = new List<string>();
            foreach (var spec in specs)
            {
                if (spec == null)
                {
                    errors.Add("Card spec list contains a null entry.");
                    continue;
                }

                if (string.IsNullOrEmpty(spec.Id))
                {
                    errors.Add("Card spec requires an id.");
                }

                if (spec is InterventionCardSpec intervention)
                {
                    if (intervention.Intervention.IsEmpty)
                    {
                        errors.Add("Card '" + spec.Id + "': intervention card requires an action key.");
                    }
                    else if (!context.HasIntervention(intervention.Intervention.ToKey()))
                    {
                        errors.Add("Card '" + spec.Id + "': unknown intervention key '"
                            + intervention.Intervention.Id + "'.");
                    }

                    continue;
                }

                var execution = (ExecutionCardSpec)spec;
                foreach (var effect in execution.Effects ?? System.Array.Empty<EffectSpec>())
                {
                    if (effect == null)
                    {
                        errors.Add("Card '" + spec.Id + "': effects contain a null entry.");
                        continue;
                    }

                    if (!context.HasEffect(effect.Key))
                    {
                        errors.Add("Card '" + spec.Id + "': no runtime handler for effect key '" + effect.Key + "'.");
                    }

                    foreach (var error in effect.Validate(context))
                    {
                        errors.Add("Card '" + spec.Id + "': " + error);
                    }
                }
            }

            return errors;
        }
    }
}
