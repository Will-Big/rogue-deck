using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    public sealed class TriggerStatusHandler : IEffectHandler, IEffectDataValidator
    {
        public EffectKey Key => EffectKeys.TriggerStatus;

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

            if (!(ctx.Effect?.Payload is TriggerStatusPayload payload))
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

        private static void ApplySnapshotTargets(
            EffectContext ctx,
            TriggerStatusPayload payload,
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
                if (status != null
                    && ctx.StatusRegistry != null
                    && ctx.StatusRegistry.TryResolve(payload.Key, out var behavior))
                {
                    var hpBefore = enemy.Hp;
                    behavior.OnTurnEnd(new StatusTickContext
                    {
                        Instance = status,
                        HolderBag = enemy.Statuses,
                        HolderId = enemy.Id,
                        DealDamage = damage => enemy.Hp -= damage,
                        Events = ctx.ExtraEvents
                    });
                    ctx.DamageDealt += hpBefore - enemy.Hp;
                }

                enemy.Statuses.Add(payload.SuppressMarkerKey, StatusLifetime.ThisTurn);
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
