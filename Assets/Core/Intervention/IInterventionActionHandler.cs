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

        /// <summary>Target demand the UI must satisfy before play. Single source of truth —
        /// mirrors what CanApply checks (e.g. swap requires Target and SecondaryTarget).</summary>
        TargetingRequirement Targeting { get; }

        bool CanApply(InterventionPlayContext ctx);
        void Apply(InterventionPlayContext ctx);
    }
}
