using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation
{
    /// <summary>Drives a MultiTurnScenario: persists player/enemy state (incl. statuses) across turns,
    /// rebuilds the zone and resets fate energy each turn, applies fate plays, resolves, and stops on
    /// win/lose. Wires the StatusRegistry so statuses and their lifetimes are active across turns.</summary>
    public sealed class MultiTurnRunner
    {
        public MultiTurnComparisonResult Compare(MultiTurnScenario scenario)
        {
            var baselineScenario = WithoutInterventionPlays(scenario);
            return new MultiTurnComparisonResult(
                scenario,
                Run(baselineScenario),
                Run(scenario));
        }

        public MultiTurnResult Run(MultiTurnScenario scenario)
        {
            var state = new CombatState();
            state.AddSoloPlayer(scenario.PlayerHp);
            foreach (var enemy in scenario.Enemies)
            {
                state.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            var resolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses());
            var interventionActions = CombatRegistries.InterventionActions();

            var turns = new List<TurnOutcome>();
            var outcome = Outcome.Ongoing;

            for (int i = 0; i < scenario.Turns.Count; i++)
            {
                var script = scenario.Turns[i];
                var cardsById = LoadZone(state, script.ZoneCards);
                state.FateEnergy = script.FateEnergy;

                var initialOrder = Summarize(state.Zone.ResolutionOrder());
                var interventionResult = new InterventionPlayResolver(interventionActions)
                    .Resolve(state, BuildPlays(script.InterventionPlays, cardsById));
                var manipulatedOrder = Summarize(state.Zone.ResolutionOrder());

                var timeline = resolver.Resolve(state, i);
                turns.Add(new TurnOutcome(i, initialOrder, manipulatedOrder, interventionResult, timeline));

                outcome = OutcomeOf(timeline);
                if (outcome != Outcome.Ongoing)
                {
                    break; // combat decided; later turns don't happen
                }
            }

            return new MultiTurnResult(scenario, state, turns, outcome);
        }

        private static MultiTurnScenario WithoutInterventionPlays(MultiTurnScenario scenario)
        {
            var turns = new List<TurnScript>();
            foreach (var turn in scenario.Turns)
            {
                turns.Add(new TurnScript(
                    turn.FateEnergy,
                    turn.ZoneCards,
                    new InterventionPlaySpec[0]));
            }

            return new MultiTurnScenario(
                scenario.Id,
                scenario.Name,
                scenario.PlayerHp,
                scenario.Enemies,
                turns);
        }

        private static Dictionary<string, ExecutionCardInstance> LoadZone(
            CombatState state, IReadOnlyList<ZoneCardSpec> zoneCards)
        {
            state.Zone.Clear();
            var cardsById = new Dictionary<string, ExecutionCardInstance>();
            foreach (var card in zoneCards)
            {
                var def = new CardDefinition(
                    card.Id, card.Name, card.Side, card.ExecutionOrder, card.Effects);
                var instance = new ExecutionCardInstance(def);
                state.Zone.Add(instance);
                cardsById.Add(card.Id, instance);
            }

            return cardsById;
        }

        private static IReadOnlyList<InterventionPlay> BuildPlays(
            IReadOnlyList<InterventionPlaySpec> specs, Dictionary<string, ExecutionCardInstance> cardsById)
        {
            var plays = new List<InterventionPlay>();
            foreach (var spec in specs)
            {
                var target = cardsById[spec.TargetCardId];
                var secondary = spec.SecondaryTargetCardId == null
                    ? null
                    : cardsById[spec.SecondaryTargetCardId];
                plays.Add(new InterventionPlay(spec.Intervention, target, secondary));
            }

            return plays;
        }

        private static IReadOnlyList<OrderCardSummary> Summarize(IReadOnlyList<ExecutionCardInstance> cards)
        {
            var summaries = new List<OrderCardSummary>();
            foreach (var card in cards)
            {
                summaries.Add(new OrderCardSummary(card.Def.Id, card.Def.Side.ToString(), card.ExecutionOrder));
            }

            return summaries;
        }

        private static Outcome OutcomeOf(IReadOnlyList<ResolutionEvent> timeline)
        {
            for (int i = timeline.Count - 1; i >= 0; i--)
            {
                if (timeline[i] is TurnEnded ended)
                {
                    return ended.Outcome;
                }
            }

            return Outcome.Ongoing;
        }
    }
}
