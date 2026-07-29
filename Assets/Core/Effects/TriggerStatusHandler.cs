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

            var status = enemy.Statuses.Get(payload.Key);
            if (status != null
                && ctx.StatusRegistry != null
                && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior))
            {
                var target = enemy;
                var hpBefore = target.Hp;
                behavior.OnTurnEnd(new StatusTickContext
                {
                    Instance = status,
                    HolderBag = target.Statuses,
                    HolderId = target.Id,
                    DealDamage = damage => target.Hp -= damage,
                    Events = ctx.ExtraEvents
                });
                ctx.DamageDealt = hpBefore - target.Hp;
            }

            enemy.Statuses.Add(payload.SuppressMarkerKey, StatusLifetime.ThisTurn);
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

            if (string.IsNullOrEmpty(payload.SuppressMarkerKey.Id))
            {
                yield return "trigger_status payload requires a suppress-marker key.";
            }
        }
    }
}
