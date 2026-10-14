using System;
using System.Collections.Generic;
using FateWeaver.Core.Authoring.Statuses;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;
using FateWeaver.Core.Intervention;
namespace FateWeaver.Simulation.Descriptions
{
    public sealed class RequestHandlerRegistry<TKey, THandler> where THandler : class
    {
        private readonly Dictionary<TKey, THandler> _handlers = new Dictionary<TKey, THandler>();
        public void Register(TKey key, THandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_handlers.ContainsKey(key)) throw new ArgumentException("Duplicate description handler: " + key);
            _handlers.Add(key, handler);
        }
        public THandler Resolve(TKey key) => _handlers.TryGetValue(key, out var handler) ? handler
            : throw new KeyNotFoundException("No description request handler: " + key);
    }
    public sealed class DescriptionRequestContext
    {
        public CardDefinition Card { get; }
        public StatusContentCatalog StatusContent { get; }
        public DescriptionRequestContext(CardDefinition card, StatusContentCatalog statuses)
        { Card = card ?? throw new ArgumentNullException(nameof(card)); StatusContent = statuses ?? throw new ArgumentNullException(nameof(statuses)); }
    }
    public sealed class EffectDescriptionRequest
    {
        public CardTargetKey? Target { get; }
        public LocalizedTextRequest Text { get; }
        public EffectDescriptionRequest(CardTargetKey? target, LocalizedTextRequest text)
        { Target = target; Text = text ?? throw new ArgumentNullException(nameof(text)); }
    }
    public sealed class DescriptionRequestCatalog
    {
        public RequestHandlerRegistry<EffectKey, Func<EffectData, int, DescriptionRequestContext, EffectDescriptionRequest>> Effects { get; }
            = new RequestHandlerRegistry<EffectKey, Func<EffectData, int, DescriptionRequestContext, EffectDescriptionRequest>>();
        public RequestHandlerRegistry<InterventionActionKey, Func<InterventionActionData, LocalizedTextRequest>> Interventions { get; }
            = new RequestHandlerRegistry<InterventionActionKey, Func<InterventionActionData, LocalizedTextRequest>>();
        public StatusContentCatalog StatusContent { get; }
        public DescriptionRequestCatalog(StatusContentCatalog statuses)
            => StatusContent = statuses ?? throw new ArgumentNullException(nameof(statuses));
        public static DescriptionRequestCatalog CreateDefault(StatusContentCatalog statuses)
        {
            var result = new DescriptionRequestCatalog(statuses);
            result.Effects.Register(EffectKeys.Damage, new DamageDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.ApplyStatus, new ApplyStatusDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.NullifyNextPlayerConditionReward, new NullifyNextPlayerConditionRewardDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.GrantNextPlayerDamageCardBonus, new GrantNextPlayerDamageCardBonusDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.MoveFormation, new MoveFormationDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.ConsumeStatus, new ConsumeStatusDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.TriggerStatus, new TriggerStatusDescriptionHandler().Request);
            result.Effects.Register(EffectKeys.GrantNextTurnFate, new GrantNextTurnFateDescriptionHandler().Request);
            result.Interventions.Register(InterventionActionKeys.ChangeExecutionOrder, new ChangeExecutionOrderDescriptionHandler().Request);
            result.Interventions.Register(InterventionActionKeys.SwapExecutionOrder, new SwapExecutionOrderDescriptionHandler().Request);
            result.Interventions.Register(InterventionActionKeys.Lock, new LockDescriptionHandler().Request);
            return result;
        }
    }
    public static class DescriptionRequests
    {
        public static LocalizedTextRequest Text(string key, params (string Name, object Value)[] arguments)
        {
            var values = new Dictionary<string, object>();
            foreach (var argument in arguments) values.Add(argument.Name, argument.Value);
            return new LocalizedTextRequest(TranslationTable.Descriptions, key, values);
        }
        public static LocalizedTextRequest Name(string key) => new LocalizedTextRequest(TranslationTable.Content, key);
        public static LocalizedTextRequest Empty => Text("text.empty");
    }
}
