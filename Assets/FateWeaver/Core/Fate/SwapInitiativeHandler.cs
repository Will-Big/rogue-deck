namespace FateWeaver.Core.Fate
{
    public sealed class SwapInitiativeHandler : IFateActionHandler
    {
        public FateActionKey Key => FateActionKeys.SwapInitiative;

        public bool CanApply(FatePlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.SecondaryTarget != null
                && ctx.Action != null
                && ctx.Action.Key == Key
                && !ctx.Target.IsLocked
                && !ctx.SecondaryTarget.IsLocked
                && ctx.State.FateEnergy >= ctx.Action.Cost;

        public void Apply(FatePlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Action.Cost;
            ctx.FateEnergySpent = ctx.Action.Cost;

            var initiative = ctx.Target.Initiative;
            ctx.Target.Initiative = ctx.SecondaryTarget.Initiative;
            ctx.SecondaryTarget.Initiative = initiative;
        }
    }
}
