namespace FateWeaver.Core.Intervention
{
    public sealed class SwapInitiativeHandler : IInterventionActionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.SwapInitiative;

        public bool CanApply(InterventionPlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.SecondaryTarget != null
                && ctx.Intervention != null
                && ctx.Intervention.Key == Key
                && !ctx.Target.IsLocked
                && !ctx.SecondaryTarget.IsLocked
                && ctx.State.FateEnergy >= ctx.Intervention.Cost;

        public void Apply(InterventionPlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Intervention.Cost;
            ctx.FateEnergySpent = ctx.Intervention.Cost;

            var initiative = ctx.Target.Initiative;
            ctx.Target.Initiative = ctx.SecondaryTarget.Initiative;
            ctx.SecondaryTarget.Initiative = initiative;
        }
    }
}
