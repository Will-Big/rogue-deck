namespace FateWeaver.Core.Intervention
{
    public sealed class SwapExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public TargetingRequirement Targeting => TargetingRequirement.RailCards(2);

        public bool CanApply(InterventionPlayContext ctx)
        {
            var payload = PayloadOf(ctx);
            return payload != null
                && ctx.Target != null
                && ctx.SecondaryTarget != null
                && !ctx.Target.IsLocked
                && !ctx.SecondaryTarget.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost
                && (payload.TargetSide == null
                    || (ctx.Target.Def.Side == payload.TargetSide
                        && ctx.SecondaryTarget.Def.Side == payload.TargetSide))
                && AreAdjacentIfRequired(ctx, payload);
        }

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.InterventionCost;
            ctx.FateEnergySpent = ctx.Intervention.InterventionCost;

            var executionOrder = ctx.Target.ExecutionOrder;
            ctx.Target.ExecutionOrder = ctx.SecondaryTarget.ExecutionOrder;
            ctx.SecondaryTarget.ExecutionOrder = executionOrder;
        }

        private SwapExecutionOrderPayload PayloadOf(InterventionPlayContext ctx)
            => ctx != null && ctx.State != null && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                    ? ctx.Intervention.Payload as SwapExecutionOrderPayload
                    : null;

        private static bool AreAdjacentIfRequired(
            InterventionPlayContext ctx, SwapExecutionOrderPayload payload)
        {
            if (!payload.RequireAdjacent)
            {
                return true;
            }

            var order = ctx.State.Zone.ResolutionOrder();
            var first = IndexOf(order, ctx.Target);
            var second = IndexOf(order, ctx.SecondaryTarget);
            return first >= 0 && second >= 0 && (first - second == 1 || second - first == 1);
        }

        private static int IndexOf(
            System.Collections.Generic.IReadOnlyList<Combat.ExecutionCardInstance> order,
            Combat.ExecutionCardInstance card)
        {
            for (int i = 0; i < order.Count; i++)
            {
                if (ReferenceEquals(order[i], card))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
