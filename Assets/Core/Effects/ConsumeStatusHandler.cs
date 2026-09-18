using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>이 효과가 고른 적의 상태(예: 독)를 소비 방식(ConsumptionMode)대로 소비한다. 소비 0은 취소가
    /// 아니라 그냥 무소득(독성 환원의 첫 사용). 위치는 effect.TargetSelector(없으면 FrontOne)다.</summary>
    public sealed class ConsumeStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.ConsumeStatus;

        public CardTargetKey? TargetFor(CardDefinition card, EffectData effect)
            => new CardTargetKey(
                CardTargetFaction.Enemy,
                EffectTargetResolver.RangeFor(effect.TargetSelector ?? TargetSelector.FrontOne));

        public void Apply(EffectContext ctx)
        {
            if (!(ctx.Effect?.Payload is ConsumeStatusPayload payload))
            {
                return;
            }

            foreach (var enemy in ctx.Targets.Enemies)
            {
                ctx.ConsumedAmount += ConsumeFrom(ctx, enemy, payload);
            }
        }

        /// <summary>대상 하나에서 규칙대로 차감하고 실제 소비량을 돌려준다. 여러 대상을 소비하는 카드의
        /// 집계 방식은 아직 정하지 않았다(스펙 §5) — 지금은 합산하며, 저작 콘텐츠는 단일 대상뿐이다.</summary>
        private static int ConsumeFrom(EffectContext ctx, Enemy enemy, ConsumeStatusPayload payload)
        {
            var status = enemy.Statuses.Get(payload.Key);
            var available = status == null ? 0 : status.Magnitude;
            var consumed = ConsumptionRule.Take(available, payload.Amount, payload.Mode);
            if (consumed <= 0)
            {
                return 0;
            }

            status.Magnitude -= consumed;
            if (status.Magnitude <= 0)
            {
                enemy.Statuses.Remove(payload.Key);
            }

            ctx.ExtraEvents.Add(new Events.StatusConsumed(enemy.Id, payload.Key.Id, consumed));
            return consumed;
        }

        public IEnumerable<string> ValidateData(EffectData effect)
        {
            if (!(effect.Payload is ConsumeStatusPayload payload))
            {
                yield return "consume_status effect requires a ConsumeStatusPayload.";
                yield break;
            }

            if (string.IsNullOrEmpty(payload.Key.Id))
            {
                yield return "consume_status payload requires a status key.";
            }

            if (payload.Amount < 1)
            {
                yield return "consume_status Amount must be at least 1.";
            }
        }
    }
}
