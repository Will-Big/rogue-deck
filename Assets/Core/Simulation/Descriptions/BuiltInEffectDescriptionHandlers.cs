using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class DamageDescriptionHandler : IEffectDescriptionHandler
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

        public EffectKey Key => EffectKeys.Damage;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(
                context.TargetOf(effect),
                "피해 " + effectValue + context.DamageTraitsSuffix((effect.Payload as DamagePayload)?.Traits));
    }

    public sealed class ApplyStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            if (!(effect.Payload is ApplyStatusPayload payload)) throw new ArgumentException("Apply-status payload required.");
            var status = DescriptionRequests.Name("status." + payload.Key.Id + ".name");
            var duration = context.StatusContent.CountIsDuration(payload.Key);
            var key = duration ? "effect.apply_status." + context.StatusContent.LifetimeOf(payload.Key).ToString() : "effect.apply_status";
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text(key, ("status", status), ("amount", value)));
        }

        public EffectKey Key => EffectKeys.ApplyStatus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
                throw new ArgumentException(
                    "Apply-status description requires an ApplyStatusPayload.",
                    nameof(effect));

            var statusName = context.Statuses.Resolve(payload.Key);
            var target = context.TargetOf(effect);

            // 숫자가 세기인지 지속인지는 카드가 아니라 상태 콘텐츠가 결정한다. 구조화 대상과
            // JSON 기반 상태 메타데이터를 함께 보존해, UI가 문장을 다시 해석하지 않게 한다.
            var text = statusName;
            if (context.StatusContent.CountIsDuration(payload.Key))
            {
                var kind = context.StatusContent.LifetimeOf(payload.Key);
                text += " " + context.LifetimeSuffix(kind, effectValue);
            }
            else
            {
                text += " " + effectValue;
            }

            return new EffectDescriptionFragment(
                target,
                text);
        }
    }

    public sealed class NullifyNextPlayerConditionRewardDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.nullify_reward"));
        }

        public EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(null, "다음 플레이어 조건 보상을 무효화");
    }

    public sealed class GrantNextPlayerDamageCardBonusDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.damage_bonus", ("amount", value)));
        }

        public EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(null, "다음 플레이어 피해 카드가 주는 피해 +" + effectValue);
    }

    public sealed class MoveFormationDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), value == 0 ? DescriptionRequests.Text("effect.move.stay") : DescriptionRequests.Text(value < 0 ? "effect.move.forward" : "effect.move.backward", ("amount", value < 0 ? -(long)value : (long)value)));
        }

        public EffectKey Key => EffectKeys.MoveFormation;

        public EffectDescriptionFragment Describe(
            EffectData effect,
            int effectValue,
            DescriptionContext context)
        {
            if (effectValue == 0)
                return new EffectDescriptionFragment(context.TargetOf(effect), "대형 위치 유지");

            var distance = effectValue < 0 ? -(long)effectValue : effectValue;
            var direction = effectValue < 0 ? "전방" : "후방";
            return new EffectDescriptionFragment(
                context.TargetOf(effect),
                "대형 " + direction + "으로 " + distance + "칸 이동");
        }
    }

    public sealed class ConsumeStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload)) throw new ArgumentException("Consume-status payload required.");
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text(payload.Mode == ConsumptionMode.UpTo ? "effect.consume_status.up_to" : "effect.consume_status", ("status", DescriptionRequests.Name("status." + payload.Key.Id + ".name")), ("amount", payload.Amount)));
        }

        public EffectKey Key => EffectKeys.ConsumeStatus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload))
                throw new ArgumentException(
                    "Consume-status description requires a ConsumeStatusPayload.", nameof(effect));

            var amount = payload.Mode == ConsumptionMode.UpTo
                ? "최대 " + payload.Amount
                : payload.Amount.ToString();
            return new EffectDescriptionFragment(
                context.TargetOf(effect),
                context.Statuses.Resolve(payload.Key) + " " + amount + " 소비");
        }
    }

    public sealed class TriggerStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            if (!(effect.Payload is TriggerStatusPayload payload)) throw new ArgumentException("Trigger-status payload required.");
            return new EffectDescriptionRequest(context.Card.TargetOf(effect), DescriptionRequests.Text("effect.trigger_status", ("status", DescriptionRequests.Name("status." + payload.Key.Id + ".name"))));
        }

        public EffectKey Key => EffectKeys.TriggerStatus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is TriggerStatusPayload payload))
                throw new ArgumentException(
                    "Trigger-status description requires a TriggerStatusPayload.", nameof(effect));

            return new EffectDescriptionFragment(
                context.TargetOf(effect),
                context.Statuses.Resolve(payload.Key) + " 즉시 발동 (이번 턴 종료에는 발동하지 않음)");
        }
    }

    public sealed class GrantNextTurnFateDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectDescriptionRequest Request(EffectData effect, int value, DescriptionRequestContext context)
        {
            return new EffectDescriptionRequest(null, DescriptionRequests.Text("effect.next_turn_fate", ("amount", value)));
        }

        public EffectKey Key => EffectKeys.GrantNextTurnFate;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(null, "다음 사용 턴에 운명력 " + effectValue + " 획득");
    }
}
