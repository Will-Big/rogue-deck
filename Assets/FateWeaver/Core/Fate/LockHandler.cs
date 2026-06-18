namespace FateWeaver.Core.Fate
{
    public sealed class LockHandler : IFateActionHandler
    {
        public FateActionKey Key => FateActionKeys.Lock;

        public bool CanApply(FatePlayContext ctx)
            => ctx != null
                && ctx.State != null
                && ctx.Target != null
                && ctx.Action != null
                && ctx.Action.Key == Key
                && ctx.State.FateEnergy >= ctx.Action.Cost;

        public void Apply(FatePlayContext ctx)
        {
            if (!CanApply(ctx))
            {
                return;
            }

            ctx.State.FateEnergy -= ctx.Action.Cost;
            ctx.FateEnergySpent = ctx.Action.Cost;
            ctx.Target.IsLocked = true;
        }
    }
}
