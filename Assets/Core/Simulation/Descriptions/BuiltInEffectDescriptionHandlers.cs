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

            var statusName = context.Statuses.Resolve(payload.Key);
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
