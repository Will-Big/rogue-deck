using System.Collections.Generic;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;

namespace FateWeaver.Simulation
{
    /// <summary>Result of one resolved turn within a multi-turn run.</summary>
    public sealed class TurnOutcome
    {
        public int TurnIndex { get; }
        public IReadOnlyList<OrderCardSummary> InitialOrder { get; }
        public IReadOnlyList<OrderCardSummary> ManipulatedOrder { get; }
        public FatePlayResult FatePlayResult { get; }
        public IReadOnlyList<ResolutionEvent> Timeline { get; }

        public TurnOutcome(
            int turnIndex,
            IReadOnlyList<OrderCardSummary> initialOrder,
            IReadOnlyList<OrderCardSummary> manipulatedOrder,
            FatePlayResult fatePlayResult,
            IReadOnlyList<ResolutionEvent> timeline)
        {
            TurnIndex = turnIndex;
            InitialOrder = initialOrder;
            ManipulatedOrder = manipulatedOrder;
            FatePlayResult = fatePlayResult;
            Timeline = timeline;
        }
    }

    /// <summary>Result of a whole multi-turn run: per-turn outcomes, the final state, and the final outcome.</summary>
    public sealed class MultiTurnResult
    {
        public MultiTurnScenario Scenario { get; }
        public CombatState FinalState { get; }
        public IReadOnlyList<TurnOutcome> Turns { get; }
        public Outcome Outcome { get; }

        public MultiTurnResult(
            MultiTurnScenario scenario,
            CombatState finalState,
            IReadOnlyList<TurnOutcome> turns,
            Outcome outcome)
        {
            Scenario = scenario;
            FinalState = finalState;
            Turns = turns;
            Outcome = outcome;
        }
    }
}
