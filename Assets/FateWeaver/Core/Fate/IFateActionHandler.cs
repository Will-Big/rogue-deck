using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Fate
{
    public sealed class FatePlayContext
    {
        public CombatState State;
        public ActionCardInstance Target;
        public ActionCardInstance SecondaryTarget;
        public FateActionData Action;

        public int FateEnergySpent;
    }

    public interface IFateActionHandler
    {
        FateActionKey Key { get; }
        bool CanApply(FatePlayContext ctx);
        void Apply(FatePlayContext ctx);
    }
}
