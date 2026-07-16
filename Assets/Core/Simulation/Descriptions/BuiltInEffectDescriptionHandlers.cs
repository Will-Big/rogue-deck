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
            if (!effect.StatusKey.HasValue || !effect.StatusLifetime.HasValue)
                throw new ArgumentException(
                    "Apply-status description requires a status key and lifetime.",
                    nameof(effect));

            var suffix = context.LifetimeSuffix(effect.StatusLifetime.Value);
            return context.TargetPrefix(effect)
                + context.StatusTargetPrefix(effect.StatusTarget)
                + context.Statuses.Resolve(effect.StatusKey.Value)
                + " " + effectValue
                + (string.IsNullOrEmpty(suffix) ? string.Empty : " " + suffix);
        }
    }

    public sealed class NullifyNextPlayerConditionRewardDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.NullifyNextPlayerConditionReward;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
            => context.TargetPrefix(effect) + "다음 플레이어 조건 보상을 무효화";
    }

    public sealed class GrantNextPlayerAttackDamageBonusDescriptionHandler
        : IEffectDescriptionHandler
    {
        public EffectKey Key => EffectKeys.GrantNextPlayerAttackDamageBonus;

        public string Describe(EffectData effect, int effectValue, DescriptionContext context)
            => context.TargetPrefix(effect) + "다음 플레이어 공격 피해 +" + effectValue;
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
}
