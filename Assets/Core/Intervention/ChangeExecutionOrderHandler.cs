namespace FateWeaver.Core.Intervention
{
    public sealed class ChangeExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public TargetingRequirement Targeting => TargetingRequirement.RailCards(1);

        public bool CanApply(InterventionPlayContext ctx)
        {
            var payload = PayloadOf(ctx);
            return payload != null
                && ctx.Target != null
                && IsOnLine(ctx, ctx.Target)
                && !ctx.Target.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost
                && (payload.TargetSide == null || ctx.Target.Def.Side == payload.TargetSide);
        }

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.InterventionCost;
            ctx.FateEnergySpent = ctx.Intervention.InterventionCost;
            ctx.State.Zone.MoveTo(ctx.Target, ctx.Target.ExecutionOrder + PayloadOf(ctx).Delta);
        }

        /// <summary>번호 변경은 실행선 위의 카드에만 뜻이 있다(FutureZone.MoveTo).</summary>
        private static bool IsOnLine(InterventionPlayContext ctx, Combat.ExecutionCardInstance card)
            => ctx.State.Zone.Contains(card);

        /// <summary>봉투가 이 핸들러의 것이고 페이로드 타입까지 맞을 때만 값을 준다. 잘못 배선된
        /// 개입은 예외가 아니라 CanApply 실패로 떨어진다 — 기존 방어 순서를 그대로 유지한다.</summary>
        private ChangeExecutionOrderPayload PayloadOf(InterventionPlayContext ctx)
            => ctx != null && ctx.State != null && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                    ? ctx.Intervention.Payload as ChangeExecutionOrderPayload
                    : null;
    }
}
