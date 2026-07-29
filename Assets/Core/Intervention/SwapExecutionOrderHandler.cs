namespace FateWeaver.Core.Intervention
{
    public sealed class SwapExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public TargetingRequirement Targeting => TargetingRequirement.RailCards(2);

        public bool CanApply(InterventionPlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.SecondaryTarget != null
                && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                && !ctx.Target.IsLocked
                && !ctx.SecondaryTarget.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost
                && (ctx.Intervention.TargetSide == null
                    || (ctx.Target.Def.Side == ctx.Intervention.TargetSide
                        && ctx.SecondaryTarget.Def.Side == ctx.Intervention.TargetSide))
                && AreAdjacentIfRequired(ctx);

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

        private static bool AreAdjacentIfRequired(InterventionPlayContext ctx)
        {
            if (!ctx.Intervention.RequireAdjacentTargets)
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
