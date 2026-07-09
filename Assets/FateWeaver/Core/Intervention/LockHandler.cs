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
                && ctx.State.FateEnergy >= ctx.Intervention.Cost;

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.Cost;
            ctx.FateEnergySpent = ctx.Intervention.Cost;
            ctx.Target.IsLocked = true;
        }
    }
}
