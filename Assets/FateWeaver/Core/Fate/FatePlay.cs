using FateWeaver.Core.Combat;

namespace FateWeaver.Core.Fate
{
    public sealed class FatePlay
    {
        public FateActionData Action { get; }
        public ActionCardInstance Target { get; }
        public ActionCardInstance SecondaryTarget { get; }

        public FatePlay(FateActionData action, ActionCardInstance target)
            : this(action, target, null)
        {
        }

        public FatePlay(FateActionData action, ActionCardInstance target, ActionCardInstance secondaryTarget)
        {
            Action = action;
            Target = target;
            SecondaryTarget = secondaryTarget;
        }
    }
}
