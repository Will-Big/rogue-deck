using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class DamageDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
            => context.TargetPrefix(effect) + "피해 " + effectValue;
    }

    public sealed class ApplyStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
                throw new ArgumentException(
                    "Apply-status description requires an ApplyStatusPayload.",
                    nameof(effect));

            var prefix = context.TargetPrefix(effect) + context.StatusTargetPrefix(payload.Target);
            var statusName = context.Statuses.Resolve(payload.Key);

            // 규칙 10: 숫자가 세기인지 지속인지는 카드가 아니라 상태의 StatusContentCatalog 항목만
            // 안다. 세기(Permanent·ThisTurn)는 오늘과 같은 "{상태} {N}"; 지속(Turns·UntilConsumed)은
            // 카드가 준 숫자를 그대로 보여주면 세기로 오해되므로 숫자를 접미사 안에만 넣는다 — 상태의
            // 진짜 세기(예: 취약의 배율)는 카드 텍스트에 애초에 나타나지 않는다(이미 그렇다).
            if (!context.StatusContent.CountIsDuration(payload.Key))
            {
                return prefix + statusName + " " + effectValue;
            }

            var kind = context.StatusContent.LifetimeOf(payload.Key);
            return prefix + statusName + " " + context.LifetimeSuffix(kind, effectValue);
        }
    }

    public sealed class NullifyNextPlayerConditionRewardDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
            => context.TargetPrefix(effect) + "다음 플레이어 조건 보상을 무효화";
    }

    public sealed class GrantNextPlayerDamageCardBonusDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
            => context.TargetPrefix(effect) + "다음 플레이어 피해 카드가 주는 피해 +" + effectValue;
    }

    public sealed class MoveFormationDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.MoveFormation;

        public string Describe(
            EffectData effect,
            int effectValue,
            DescriptionContext context)
        {
            if (effectValue == 0) return "소유자의 대형 위치를 유지";

            var distance = effectValue < 0 ? -(long)effectValue : effectValue;
            var direction = effectValue < 0 ? "전방" : "후방";
            return "소유자를 대형 " + direction + "으로 "
                + distance + "칸 이동";
        }
    }

    public sealed class ConsumeStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.ConsumeStatus;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload))
                throw new ArgumentException(
                    "Consume-status description requires a ConsumeStatusPayload.", nameof(effect));

            var text = context.TargetPrefix(effect)
                + context.Statuses.Resolve(payload.Key) + " 최대 " + payload.MaxAmount + " 소비";
            return payload.DamageBonusPerConsumed > 0
                ? text + " (소비 1당 피해 +" + payload.DamageBonusPerConsumed + ")"
                : text;
        }
    }

    public sealed class TriggerStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.TriggerStatus;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is TriggerStatusPayload payload))
                throw new ArgumentException(
                    "Trigger-status description requires a TriggerStatusPayload.", nameof(effect));

            return context.TargetPrefix(effect)
                + context.Statuses.Resolve(payload.Key) + " 즉시 발동 (이번 턴 종료에는 발동하지 않음)";
        }
    }

    public sealed class GrantNextTurnFateDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.GrantNextTurnFate;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
            => "다음 사용 턴에 운명력 " + effectValue + " 획득";
    }
}
