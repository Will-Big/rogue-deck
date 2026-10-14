using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation.Descriptions
{
    public sealed class ChangeExecutionOrderDescriptionHandler
    {
        public LocalizedTextRequest Request(InterventionActionData action)
        { var delta = (action.Payload as ChangeExecutionOrderPayload)?.Delta ?? 0; return DescriptionRequests.Text("intervention.change_order", ("amount", delta)); }
    }

    public sealed class SwapExecutionOrderDescriptionHandler
    {
        public LocalizedTextRequest Request(InterventionActionData action)
        { return DescriptionRequests.Text("intervention.swap_order"); }
    }

    public sealed class LockDescriptionHandler
    {
        public LocalizedTextRequest Request(InterventionActionData action)
        { return DescriptionRequests.Text("intervention.lock"); }
    }
}
