using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class DamageDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            var traits = (effect.Payload as DamagePayload)?.Traits;
            var names = DescriptionRequests.Empty;
            if (traits != null) foreach (var trait in traits.All)
            {
                var name = DescriptionRequests.Name("damage_trait." + trait.ToString() + ".name");
                names = names.Key == "text.empty" ? name : DescriptionRequests.Text("list.comma", ("first", names), ("next", name));
            }
            var suffix = names.Key == "text.empty" ? names : DescriptionRequests.Text("damage.traits", ("traits", names));
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text("effect.damage", ("amount", value), ("traits", suffix)));
        }
    }

    public sealed class ApplyStatusDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            if (!(effect.Payload is ApplyStatusPayload payload)) throw new ArgumentException("Apply-status payload required.");
            var status = DescriptionRequests.Name("status." + payload.Key.Id + ".name");
            var duration = context.StatusContent.CountIsDuration(payload.Key);
            var key = duration ? "effect.apply_status." + context.StatusContent.LifetimeOf(payload.Key).ToString() : "effect.apply_status";
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text(key, ("status", status), ("amount", value)));
        }
    }

    public sealed class NullifyNextPlayerConditionRewardDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.nullify_reward"));
        }
    }

    public sealed class GrantNextPlayerDamageCardBonusDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.damage_bonus", ("amount", value)));
        }
    }

    public sealed class MoveFormationDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), value == 0 ? DescriptionRequests.Text("effect.move.stay") : DescriptionRequests.Text(value < 0 ? "effect.move.forward" : "effect.move.backward", ("amount", value < 0 ? -(long)value : (long)value)));
        }
    }

    public sealed class ConsumeStatusDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload)) throw new ArgumentException("Consume-status payload required.");
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text(payload.Mode == ConsumptionMode.UpTo ? "effect.consume_status.up_to" : "effect.consume_status", ("status", DescriptionRequests.Name("status." + payload.Key.Id + ".name")), ("amount", payload.Amount)));
        }
    }

    public sealed class TriggerStatusDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            if (!(effect.Payload is TriggerStatusPayload payload)) throw new ArgumentException("Trigger-status payload required.");
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text("effect.trigger_status", ("status", DescriptionRequests.Name("status." + payload.Key.Id + ".name"))));
        }
    }

    public sealed class GrantNextTurnFateDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.next_turn_fate", ("amount", value)));
        }
    }
}
