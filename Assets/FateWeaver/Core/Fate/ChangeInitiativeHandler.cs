namespace FateWeaver.Core.Fate
{
    public sealed class ChangeInitiativeHandler : IFateActionHandler
    {
        public FateActionKey Key => FateActionKeys.ChangeInitiative;

        public bool CanApply(FatePlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.Action != null
                && ctx.Action.Key == Key
                && !ctx.Target.IsLocked
                && ctx.State.FateEnergy >= ctx.Action.Cost;

        public void Apply(FatePlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Action.Cost;
            ctx.FateEnergySpent = ctx.Action.Cost;
            ctx.Target.Initiative += ctx.Action.Amount;
        }
    }
}
