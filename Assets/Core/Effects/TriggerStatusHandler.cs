using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    public sealed class TriggerStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.TriggerStatus;

        public void Apply(EffectContext ctx)
        {
            if (ctx.Card.CancellationReason != null)
            {
                return;
            }

            if (!(ctx.Effect?.Payload is TriggerStatusPayload payload))
            {
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

            if (ctx.StatusRegistry != null && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior))
            {
                var status = enemy.Statuses.Get(payload.Key);
                if (status != null)
                {
                    var target = enemy;
                    var hpBefore = target.Hp;
                    behavior.OnTurnEnd(new StatusTickContext
                    {
                        Instance = status,
                        HolderBag = target.Statuses,
                        HolderId = target.Id,
                        DealDamage = damage => target.Hp -= damage,
                        Events = ctx.ExtraEvents,
                        Content = ctx.State.StatusContent
                    });
                    ctx.DamageDealt = hpBefore - target.Hp;
                }

                // 마커는 상태 존재 여부와 무관하게 심는다 (선점 잠복): 이 카드보다 뒤에 실행되는
                // 다른 카드가 이번 턴에 상태를 새로 부여하더라도 이번 턴 종료 발동은 막아야 한다.
                behavior.SuppressThisTurn(enemy.Statuses);
            }

            ctx.TargetId = enemy.Id;
        }

        public IEnumerable<string> ValidateData(EffectData effect)
        {
            if (!(effect.Payload is TriggerStatusPayload payload))
            {
                yield return "trigger_status effect requires a TriggerStatusPayload.";
                yield break;
            }

            if (string.IsNullOrEmpty(payload.Key.Id))
            {
                yield return "trigger_status payload requires a status key.";
            }
        }
    }
}
