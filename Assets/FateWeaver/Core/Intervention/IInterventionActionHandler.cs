using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionPlayContext
    {
        public CombatState State;
        public ExecutionCardInstance Target;
        public ExecutionCardInstance SecondaryTarget;
        public InterventionActionData Intervention;

        public int FateEnergySpent;
    }

    public interface IInterventionActionHandler
    {
        InterventionActionKey Key { get; }
        bool CanApply(InterventionPlayContext ctx);
        void Apply(InterventionPlayContext ctx);
    }
}
