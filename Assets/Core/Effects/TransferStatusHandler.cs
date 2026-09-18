using FateWeaver.Core.Events;
using FateWeaver.Core.Status;

namespace FateWeaver.Core.Effects
{
    /// <summary>transfer_status 입력: 옮길 상태 키.</summary>
    public sealed record TransferStatusPayload(StatusKey Key) : IEffectPayload;

    /// <summary>행위자(반응 능력의 보유자)가 가진 상태를 수치째 대상에게 옮기고 행위자에게서 지운다. 사후 전염이
    /// 사망 사건 반응으로 쓴다(ContagionReaction). 받는 쪽은 수치를 합산하고 원래 수명 종류를 유지한다.</summary>
    public sealed class TransferStatusHandler : IEffectHandler
    {
        public EffectKey Key => EffectKeys.TransferStatus;

        public void Apply(EffectContext ctx)
        {
            if (!(ctx.Effect?.Payload is TransferStatusPayload payload) || ctx.ActorStatuses == null)
            {
                return;
            }

            var from = ctx.ActorStatuses.Get(payload.Key);
            if (from == null || from.Magnitude <= 0)
            {
                return;
            }

            var lifetime = StatusLifetime.Of(from.Kind, from.Count);
            var targets = ctx.RequireTargets();
            foreach (var member in targets.Party)
            {
                Give(ctx, member.Statuses, member.Id, payload.Key, lifetime, from.Magnitude);
            }

            foreach (var enemy in targets.Enemies)
            {
                Give(ctx, enemy.Statuses, enemy.Id, payload.Key, lifetime, from.Magnitude);
            }

            ctx.ActorStatuses.Remove(payload.Key);
        }

        private static void Give(
            EffectContext ctx, StatusBag bag, string holderId, StatusKey key, StatusLifetime lifetime, int magnitude)
        {
            bag.Stack(key, lifetime, magnitude);
            ctx.ExtraEvents.Add(new StatusTransferred(ctx.ActorId, holderId, key.Id, magnitude));
            ctx.Signals.Add(new CombatSignal(CombatSignalKeys.StatusGained, ctx.ActorId, holderId, magnitude)
            {
                Detail = key.Id
            });
        }
    }
}
