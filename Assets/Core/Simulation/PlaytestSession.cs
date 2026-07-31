using FateWeaver.Core;
using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Intervention;

namespace FateWeaver.Simulation
{
    /// <summary>Mutable single-turn session used by the Unity playtest screen.</summary>
    public sealed class PlaytestSession
    {
        private readonly Dictionary<string, ExecutionCardInstance> _cardsById = new();
        private readonly InterventionPlayResolver _interventionResolver;
        private readonly TurnResolver _turnResolver;
        private IReadOnlyList<ResolutionEvent> _timeline;

        public ScenarioDefinition Scenario { get; }
        public CombatState State { get; }
        public IReadOnlyList<ExecutionCardInstance> CurrentOrder => State.Zone.ResolutionOrder();
        public bool IsResolved => _timeline != null;

        public PlaytestSession(ScenarioDefinition scenario)
        {
            Scenario = scenario;
            State = BuildState(scenario);
            _interventionResolver = new InterventionPlayResolver(CombatRegistries.InterventionActions());
            _turnResolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses());
        }

        public InterventionPlayResult ApplyInterventionAction(
            InterventionActionData action,
            string targetCardId,
            string secondaryTargetCardId = null)
        {
            if (IsResolved)
            {
                throw new InvalidOperationException("Cannot manipulate an already resolved turn.");
            }

            var target = Card(targetCardId);
            var secondary = secondaryTargetCardId == null ? null : Card(secondaryTargetCardId);
            return _interventionResolver.Resolve(
                State,
                new[] { new InterventionPlay(action, target, secondary) });
        }

        public IReadOnlyList<ResolutionEvent> Resolve()
        {
            if (_timeline == null)
            {
                _timeline = _turnResolver.Resolve(State, turnIndex: 0);
            }

            return _timeline;
        }

        private ExecutionCardInstance Card(string id)
            => _cardsById.TryGetValue(id, out var card)
                ? card
                : throw new KeyNotFoundException("No playtest card found for '" + id + "'");

        private CombatState BuildState(ScenarioDefinition scenario)
        {
            var state = new CombatState { FateEnergy = scenario.FateEnergy };
            state.AddSoloPlayer(scenario.PlayerHp);

            foreach (var enemy in scenario.Enemies)
            {
                state.Enemies.Add(new Enemy(enemy.Id, enemy.Hp));
            }

            foreach (var card in scenario.ZoneCards)
            {
                var definition = new CardDefinition(
                    card.Id,
                    card.Name,
                    card.Side,
                    card.ExecutionOrder,
                    card.Effects);
                var instance = new ExecutionCardInstance(definition);
                state.Zone.Add(instance);
                _cardsById.Add(card.Id, instance);
            }

            return state;
        }
    }
}
