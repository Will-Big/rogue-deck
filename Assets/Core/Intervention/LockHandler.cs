namespace FateWeaver.Core.Intervention
{
    public sealed class LockHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.Lock;

        public bool CanApply(InterventionPlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                && ctx.State.FateEnergy >= ctx.Intervention.InterventionCost;

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.InterventionCost;
            ctx.FateEnergySpent = ctx.Intervention.InterventionCost;
            ctx.Target.IsLocked = true;
        }
    }
}
