using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Effects
{
    /// <summary>대상 적의 상태(예: 독)를 최대치까지 소비한다. 소비 0은 취소가 아니라 그냥 무소득
    /// (독성 환원의 첫 사용). 대상 선택은 damage와 같은 규칙: TargetSelector 지정 시 위치 선택,
    /// 아니면 레거시(TargetId → 첫 적).</summary>
    public sealed class ConsumeStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.ConsumeStatus;

        public CardTargetKey? TargetFor(CardDefinition card, EffectData effect)
            => new CardTargetKey(
                CardTargetFaction.Enemy,
                CardTargetSnapshot.RangeFor(effect.TargetSelector ?? TargetSelector.FrontOne));

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (!(ctx.Effect?.Payload is ConsumeStatusPayload payload))
            {
                return;
            }

            if (ctx.Targets != null)
            {
                ApplySnapshotTargets(ctx, payload, TargetFor(ctx.Card.Def, ctx.Effect).Value);
                return;
            }

            var enemy = ctx.Effect?.TargetSelector is TargetSelector selector
                ? EnemyTargeting.Select(ctx.State, selector)
                : EnemyTargeting.ByIdOrFront(ctx.State, ctx.Card.TargetId);
            if (enemy == null)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
                return;
            }

            var status = enemy.Statuses.Get(payload.Key);
            var consumed = status == null ? 0 : Math.Min(status.Magnitude, payload.MaxAmount);
            if (consumed > 0)
            {
                status.Magnitude -= consumed;
                if (status.Magnitude <= 0)
                {
                    enemy.Statuses.Remove(payload.Key);
                }

                ctx.Card.RecordConsumedStatus(consumed);
                if (payload.DamageBonusPerConsumed != 0)
                {
                    ctx.Card.AddPendingDamageBonus(consumed * payload.DamageBonusPerConsumed);
                }
            }

            ctx.TargetId = enemy.Id;
        }

        private static void ApplySnapshotTargets(
            EffectContext ctx,
            ConsumeStatusPayload payload,
            CardTargetKey key)
        {
            var affected = 0;
            string onlyTargetId = null;
            foreach (var enemy in ctx.Targets.EnemyTargets(key))
            {
                if (enemy.Hp <= 0)
                {
                    continue;
                }

                var status = enemy.Statuses.Get(payload.Key);
                var consumed = status == null ? 0 : Math.Min(status.Magnitude, payload.MaxAmount);
                if (consumed > 0)
                {
                    status.Magnitude -= consumed;
                    if (status.Magnitude <= 0)
                    {
                        enemy.Statuses.Remove(payload.Key);
                    }

                    ctx.Card.RecordConsumedStatus(consumed);
                    if (payload.DamageBonusPerConsumed != 0)
                    {
                        ctx.Card.AddPendingDamageBonus(consumed * payload.DamageBonusPerConsumed);
                    }
                }

                onlyTargetId = enemy.Id;
                affected++;
            }

            ctx.TargetId = affected == 1 ? onlyTargetId : null;
            if (affected == 0)
            {
                ctx.Cancel(CardCancellationReason.NoValidTarget);
            }
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

            if (payload.MaxAmount < 1)
            {
                yield return "consume_status MaxAmount must be at least 1.";
            }
        }
    }
}
