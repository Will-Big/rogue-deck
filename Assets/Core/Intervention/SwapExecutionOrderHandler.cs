namespace FateWeaver.Core.Intervention
{
    public sealed class SwapExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;

        public bool CanApply(InterventionPlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.SecondaryTarget != null
                && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                && !ctx.Target.IsLocked
                && !ctx.SecondaryTarget.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost;

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
    }
}
