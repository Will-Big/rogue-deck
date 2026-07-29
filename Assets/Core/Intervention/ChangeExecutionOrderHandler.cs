namespace FateWeaver.Core.Intervention
{
    public sealed class ChangeExecutionOrderHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;

        public TargetingRequirement Targeting => TargetingRequirement.RailCards(1);

        public bool CanApply(InterventionPlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                && !ctx.Target.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost
                && (ctx.Intervention.TargetSide == null
                    || ctx.Target.Def.Side == ctx.Intervention.TargetSide);

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.InterventionCost;
            ctx.FateEnergySpent = ctx.Intervention.InterventionCost;
            ctx.Target.ExecutionOrder += ctx.Intervention.EffectValue;
        }
    }
}
