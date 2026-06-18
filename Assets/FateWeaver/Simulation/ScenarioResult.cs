using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;

namespace FateWeaver.Simulation
{
    public sealed class ScenarioResult
    {
        public ScenarioDefinition Scenario { get; }
        public CombatState FinalState { get; }
        public IReadOnlyList<OrderCardSummary> InitialOrder { get; }
        public IReadOnlyList<OrderCardSummary> ManipulatedOrder { get; }
        public FatePlayResult FatePlayResult { get; }
        public IReadOnlyList<ResolutionEvent> Timeline { get; }

        public ScenarioResult(
            ScenarioDefinition scenario,
            CombatState finalState,
            IReadOnlyList<OrderCardSummary> initialOrder,
            IReadOnlyList<OrderCardSummary> manipulatedOrder,
            FatePlayResult fatePlayResult,
            IReadOnlyList<ResolutionEvent> timeline)
        {
            Scenario = scenario;
            FinalState = finalState;
            InitialOrder = initialOrder;
            ManipulatedOrder = manipulatedOrder;
            FatePlayResult = fatePlayResult;
            Timeline = timeline;
        }
    }

    public sealed class OrderCardSummary
    {
        public string CardId { get; }
        public string Side { get; }
        public int Initiative { get; }

        public OrderCardSummary(string cardId, string side, int initiative)
        {
            CardId = cardId;
            Side = side;
            Initiative = initiative;
        }
    }
}
