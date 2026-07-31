using System;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Effects;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class DamageDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.Damage;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(context.OpposingRange(effect.TargetSelector), "피해 " + effectValue);
    }

    public sealed class ApplyStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.ApplyStatus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ApplyStatusPayload payload))
                throw new ArgumentException(
                    "Apply-status description requires an ApplyStatusPayload.",
                    nameof(effect));

            var suffix = context.LifetimeSuffix(payload.Lifetime);
            CardTargetKey? target;
            switch (payload.Target)
            {
                case StatusApplyTarget.Self:
                    target = context.SelfTarget();
                    break;
                case StatusApplyTarget.TargetEnemy:
                    target = context.EnemyRange(effect.TargetSelector);
                    break;
                case StatusApplyTarget.PartyBySelector:
                    target = context.AllyRange(effect.TargetSelector);
                    break;
                case StatusApplyTarget.AllPartyMembers:
                    target = new CardTargetKey(CardTargetFaction.Ally, CardTargetRange.All);
                    break;
                case StatusApplyTarget.PartyMember:
                    throw new InvalidOperationException(
                        "Card '" + context.CardId
                        + "' uses PartyMember, which has no approved card-frame target schema.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(payload.Target));
            }

            return new EffectDescriptionFragment(
                target,
                context.Statuses.Resolve(payload.Key)
                + " " + effectValue
                + (string.IsNullOrEmpty(suffix) ? string.Empty : " " + suffix));
        }
    }

    public sealed class NullifyNextPlayerConditionRewardDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(null, "다음 플레이어 조건 보상을 무효화");
    }

    public sealed class GrantNextPlayerDamageCardBonusDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.GrantNextPlayerDamageCardBonus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(null, "다음 플레이어 피해 카드가 주는 피해 +" + effectValue);
    }

    public sealed class MoveFormationDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.MoveFormation;

        public EffectDescriptionFragment Describe(
            EffectData effect,
            int effectValue,
            DescriptionContext context)
        {
            if (effectValue == 0)
                return new EffectDescriptionFragment(context.SelfTarget(), "대형 위치 유지");

            var distance = effectValue < 0 ? -(long)effectValue : effectValue;
            var direction = effectValue < 0 ? "전방" : "후방";
            return new EffectDescriptionFragment(
                context.SelfTarget(),
                "대형 " + direction + "으로 " + distance + "칸 이동");
        }
    }

    public sealed class ConsumeStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.ConsumeStatus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload))
                throw new ArgumentException(
                    "Consume-status description requires a ConsumeStatusPayload.", nameof(effect));

            var text = context.Statuses.Resolve(payload.Key) + " 최대 " + payload.MaxAmount + " 소비";
            return new EffectDescriptionFragment(
                context.EnemyRange(effect.TargetSelector),
                payload.DamageBonusPerConsumed > 0
                    ? text + " (소비 1당 피해 +" + payload.DamageBonusPerConsumed + ")"
                    : text);
        }
    }

    public sealed class TriggerStatusDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.TriggerStatus;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
        {
            if (!(effect.Payload is TriggerStatusPayload payload))
                throw new ArgumentException(
                    "Trigger-status description requires a TriggerStatusPayload.", nameof(effect));

            return new EffectDescriptionFragment(
                context.EnemyRange(effect.TargetSelector),
                context.Statuses.Resolve(payload.Key) + " 즉시 발동 (이번 턴 종료에는 발동하지 않음)");
        }
    }

    public sealed class GrantNextTurnFateDescriptionHandler : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.GrantNextTurnFate;

        public EffectDescriptionFragment Describe(EffectData effect, int effectValue, DescriptionContext context)
            => new EffectDescriptionFragment(null, "다음 사용 턴에 운명력 " + effectValue + " 획득");
    }
}
