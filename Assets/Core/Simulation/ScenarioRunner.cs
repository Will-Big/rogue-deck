using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation
{
    public sealed class ScenarioRunner
    {
        public ScenarioComparisonResult Compare(ScenarioDefinition scenario)
        {
            var baseline = Run(WithoutInterventionPlays(scenario));
            var manipulated = Run(scenario);
            return new ScenarioComparisonResult(scenario, baseline, manipulated);
        }

        public ScenarioResult Run(ScenarioDefinition scenario)
        {
            var state = BuildState(scenario, out var cardsById);
            var initialOrder = SummarizeOrder(state.Zone.ResolutionOrder());
            var interventionResult = ApplyInterventionPlays(state, scenario.InterventionPlays, cardsById);
            var manipulatedOrder = SummarizeOrder(state.Zone.ResolutionOrder());
            var timeline = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses())
                .Resolve(state, turnIndex: 0);

            return new ScenarioResult(
                scenario,
                state,
                initialOrder,
                manipulatedOrder,
                interventionResult,
                timeline);
        }

        private static ScenarioDefinition WithoutInterventionPlays(ScenarioDefinition scenario)
            => new ScenarioDefinition(
                scenario.Id,
                scenario.Name,
                scenario.PlayerHp,
                scenario.FateEnergy,
                scenario.Enemies,
                scenario.ZoneCards,
                new InterventionPlaySpec[0]);

        private static CombatState BuildState(
            ScenarioDefinition scenario,
            out Dictionary<string, ExecutionCardInstance> cardsById)
        {
            var state = new CombatState
            {
                PlayerHp = scenario.PlayerHp,
                FateEnergy = scenario.FateEnergy
            };

            foreach (var enemy in scenario.Enemies)
            {
                state.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            cardsById = new Dictionary<string, ExecutionCardInstance>();
            foreach (var card in scenario.ZoneCards)
            {
                var def = new CardDefinition(
                    card.Id,
                    card.Name,
                    card.Side,
                    card.ExecutionOrder,
                    card.Effects);
                var instance = new ExecutionCardInstance(def);
                state.Zone.Add(instance);
                cardsById.Add(card.Id, instance);
            }

            return state;
        }

        private static InterventionPlayResult ApplyInterventionPlays(
            CombatState state,
            IReadOnlyList<InterventionPlaySpec> specs,
            Dictionary<string, ExecutionCardInstance> cardsById)
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

            return new InterventionPlayResolver(CombatRegistries.InterventionActions()).Resolve(state, plays);
        }

        private static IReadOnlyList<OrderCardSummary> SummarizeOrder(IReadOnlyList<ExecutionCardInstance> cards)
        {
            var summaries = new List<OrderCardSummary>();
            foreach (var card in cards)
            {
                summaries.Add(new OrderCardSummary(
                    card.Def.Id,
                    card.Def.Side.ToString(),
                    card.ExecutionOrder));
            }

            return summaries;
        }
    }
}
