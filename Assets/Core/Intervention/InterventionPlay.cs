using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Intervention
{
    public sealed class InterventionPlay
    {
        public InterventionActionData Intervention { get; }
        public ExecutionCardInstance Target { get; }
        public ExecutionCardInstance SecondaryTarget { get; }

        public InterventionPlay(InterventionActionData action, ExecutionCardInstance target)
            : this(action, target, null)
        {
        }

        public InterventionPlay(InterventionActionData action, ExecutionCardInstance target, ExecutionCardInstance secondaryTarget)
        {
            Intervention = action;
            Target = target;
            SecondaryTarget = secondaryTarget;
        }
    }
}
