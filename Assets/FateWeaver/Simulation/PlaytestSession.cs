using System;
using System.Collections.Generic;
using FateWeaver.Core.Cards;
using FateWeaver.Core.Combat;
using FateWeaver.Core.Events;
using FateWeaver.Core.Fate;

namespace FateWeaver.Simulation
{
    /// <summary>Mutable single-turn session used by the Unity playtest screen.</summary>
    public sealed class PlaytestSession
    {
        private readonly Dictionary<string, ActionCardInstance> _cardsById = new();
        private readonly FatePlayResolver _fateResolver;
        private readonly TurnResolver _turnResolver;
        private IReadOnlyList<ResolutionEvent> _timeline;

        public ScenarioDefinition Scenario { get; }
        public CombatState State { get; }
        public IReadOnlyList<ActionCardInstance> CurrentOrder => State.Zone.ResolutionOrder();
        public bool IsResolved => _timeline != null;

        public PlaytestSession(ScenarioDefinition scenario)
        {
            Scenario = scenario;
            State = BuildState(scenario);
            _fateResolver = new FatePlayResolver(CombatRegistries.FateActions());
            _turnResolver = new TurnResolver(CombatRegistries.Effects(), CombatRegistries.Statuses());
        }

        public FatePlayResult ApplyFateAction(
            FateActionData action,
            string targetCardId,
            string secondaryTargetCardId = null)
        {
            if (IsResolved)
            {
                throw new InvalidOperationException("Cannot manipulate an already resolved turn.");
            }

            var target = Card(targetCardId);
            var secondary = secondaryTargetCardId == null ? null : Card(secondaryTargetCardId);
            return _fateResolver.Resolve(
                State,
                new[] { new FatePlay(action, target, secondary) });
        }

        public IReadOnlyList<ResolutionEvent> Resolve()
        {
            if (_timeline == null)
            {
                _timeline = _turnResolver.Resolve(State, turnIndex: 0);
            }

            return _timeline;
        }

        private ActionCardInstance Card(string id)
            => _cardsById.TryGetValue(id, out var card)
                ? card
                : throw new KeyNotFoundException("No playtest card found for '" + id + "'");

        private CombatState BuildState(ScenarioDefinition scenario)
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

            foreach (var card in scenario.ZoneCards)
            {
                var definition = new CardDefinition(
                    card.Id,
                    card.Name,
                    card.Side,
                    card.Type,
                    card.Initiative,
                    card.Effects);
                var instance = new ActionCardInstance(definition);
                state.Zone.Add(instance);
                _cardsById.Add(card.Id, instance);
            }

            return state;
        }
    }
}
