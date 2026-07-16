using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Conditions;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
using FateWeaver.Core.Status;

namespace FateWeaver.Simulation.Descriptions
{
    public interface IEffectDescriptionHandler
    {
        EffectKey Key { get; }
        string Describe(EffectData effect, int effectValue, DescriptionContext context);
    }

    public interface IInterventionDescriptionHandler
    {
        InterventionActionKey Key { get; }
        string DisplayName { get; }
        string Describe(InterventionActionData action, DescriptionContext context);
    }

    public interface IDescriptionGrammar
    {
        string Target(TargetSelector selector);
        string Condition(Condition condition);
        string StatusTargetPrefix(StatusApplyTarget target);
        string LifetimeSuffix(StatusLifetime lifetime);
    }

    public sealed class DescriptionContext
    {
        private readonly IDescriptionGrammar _grammar;

        public DescriptionContext(
            IDescriptionGrammar grammar,
            StatusDescriptionRegistry statuses)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            Statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));
        }

        public StatusDescriptionRegistry Statuses { get; }

        public string TargetPrefix(EffectData effect)
            => effect.TargetSelector.HasValue
                ? _grammar.Target(effect.TargetSelector.Value) + " "
                : string.Empty;

        public string Condition(Condition condition) => _grammar.Condition(condition);

        public string StatusTargetPrefix(StatusApplyTarget target)
            => _grammar.StatusTargetPrefix(target);

        public string LifetimeSuffix(StatusLifetime lifetime)
            => _grammar.LifetimeSuffix(lifetime);
    }
}
