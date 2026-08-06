using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class ChangeExecutionOrderDescriptionHandler
        : IInterventionDescriptionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.ChangeExecutionOrder;
        public string DisplayName => "실행 순서 변경";

        public string Describe(InterventionActionData action, DescriptionContext context)
        {
            var delta = (action.Payload as ChangeExecutionOrderPayload)?.Delta ?? 0;
            return "한 카드의 실행 순서 " + (delta >= 0 ? "+" + delta : delta.ToString());
        }
    }

    public sealed class SwapExecutionOrderDescriptionHandler
        : IInterventionDescriptionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.SwapExecutionOrder;
        public string DisplayName => "실행 순서 교환";

        public string Describe(InterventionActionData action, DescriptionContext context)
            => "두 카드의 실행 순서를 교환";
    }

    public sealed class LockDescriptionHandler : IInterventionDescriptionHandler
    {
        public InterventionActionKey Key => InterventionActionKeys.Lock;
        public string DisplayName => "고정";

        public string Describe(InterventionActionData action, DescriptionContext context)
            => "한 카드를 고정";
    }
}
